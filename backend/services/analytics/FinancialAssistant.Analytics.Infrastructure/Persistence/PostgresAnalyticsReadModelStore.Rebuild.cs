using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Contracts;
using FinancialAssistant.Analytics.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Analytics.Infrastructure.Persistence;

internal sealed record NativeAnalyticsRebuildSource(
    [property: JsonRequired] string OwnerScopeHash,
    [property: JsonRequired] DateOnly PeriodStart,
    [property: JsonRequired] DateOnly PeriodEnd,
    [property: JsonRequired] string SourceSnapshotVersion,
    [property: JsonRequired] DateTimeOffset RequestedAtUtc,
    [property: JsonRequired] int ExpectedRecords,
    [property: JsonRequired] string ExpectedDigest);

public sealed record NativeAnalyticsRebuildProgress(string GenerationId, string JobKey, string Stage, string Status,
    int StagedRecords, int TotalRecords, bool CleanupPending, bool RefreshPending);

public sealed record AnalyticsRebuildRefresh(string OwnerHash, string GenerationId, string JobKey,
    string SourceSnapshotVersion, string SourceDigest, DateOnly PeriodStart, DateOnly PeriodEnd,
    long ObservedOwnedRevision, IReadOnlyList<string> Currencies);

public sealed partial class PostgresAnalyticsReadModelStore
{
    public const int RebuildPageSize = 100;
    private const string RebuildSlot = "current";
    private static readonly string EmptyDigest = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>()));

    public static string CalculateSourceDigest(IEnumerable<AnalyticsRecordProjection> projections)
    {
        ArgumentNullException.ThrowIfNull(projections);
        var values = projections.Take(MaximumRecordsPerOwner + 1).OrderBy(AnalyticsProjectionCodec.RecordKey, StringComparer.Ordinal).ToArray();
        if (values.Length > MaximumRecordsPerOwner || values.Select(AnalyticsProjectionCodec.RecordKey).Distinct(StringComparer.Ordinal).Count() != values.Length)
        { throw new ArgumentException("Invalid bounded Analytics source snapshot."); }
        var digest = EmptyDigest;
        foreach (var page in values.Chunk(RebuildPageSize)) { digest = ChainDigest(digest, PageDigest(page)); }
        return digest;
    }

    public Task<NativeAnalyticsRebuildProgress> BeginRebuildAsync(AnalyticsRebuildRequest request, int expectedRecords,
        string expectedDigest, CancellationToken token = default)
    {
        var plan = new AnalyticsRebuildPlanner().Create(request);
        return BeginRebuildCoreAsync(new NativeAnalyticsRebuildSource(request.OwnerScopeHash.Trim().ToLowerInvariant(),
            plan.Scope.PeriodStart, plan.Scope.PeriodEnd, plan.SourceSnapshotVersion, request.RequestedAtUtc, expectedRecords, expectedDigest), token);
    }

    private Task<NativeAnalyticsRebuildProgress> BeginRebuildCoreAsync(NativeAnalyticsRebuildSource request, CancellationToken token)
    {
        ValidateRequest(request);
        var key = JobKey(request);
        return context.WithRecordsAsync(request.OwnerScopeHash, async (records, _) =>
        {
            var previous = await records.GetAsync("projection_rebuild", request.OwnerScopeHash, RebuildSlot);
            if (previous is not null)
            {
                var existing = ReadJob(previous, request.OwnerScopeHash);
                if (existing.Key == key)
                {
                    if (existing.Request with { RequestedAtUtc = request.RequestedAtUtc } != request)
                    { throw new StorageConflictException(); }
                    return Progress(existing);
                }
                if (existing.Status == "running" || existing.CleanupGeneration is not null || existing.RefreshPending)
                { throw new StorageConflictException(); }
            }
            var (active, counts) = await ReadActiveAsync(records, request.OwnerScopeHash);
            if (counts.Generation == key) { throw new StorageConflictException(); }
            if (active is null) { await WriteActiveAsync(records, request.OwnerScopeHash, null, counts); }
            var job = new RebuildJob(key, request, active?.Revision ?? 0, counts.Generation,
                0, EmptyDigest, null, Array.Empty<string>(), "running", null, Array.Empty<string>(), false);
            await WriteJobAsync(records, request.OwnerScopeHash, previous, job);
            return Progress(job);
        }, token);
    }

    public Task<NativeAnalyticsRebuildProgress> StageRebuildPageAsync(string owner, string jobKey, int pageNumber,
        IReadOnlyList<AnalyticsRecordProjection> page, CancellationToken token = default)
    {
        AnalyticsProjectionCodec.Owner(owner);
        if (!ValidGeneration(jobKey) || jobKey == "initial" || pageNumber is < 0 or >= MaximumRecordsPerOwner / RebuildPageSize ||
            page is null || page.Count is < 1 or > RebuildPageSize)
        { throw new ArgumentException("Invalid Analytics rebuild page."); }
        var values = page.ToArray();
        var pageDigest = PageDigest(values);
        return context.WithRecordsAsync(owner, async (records, _) =>
        {
            var durable = await records.GetAsync("projection_rebuild", owner, RebuildSlot) ?? throw new StorageConflictException();
            var job = ReadJob(durable, owner);
            if (job.Key != jobKey) { throw new StorageConflictException(); }
            if (pageNumber < job.PageDigests.Length)
            {
                if (job.PageDigests[pageNumber] != pageDigest) { throw new StorageConflictException(); }
                return Progress(job);
            }
            if (job.Status != "running" || pageNumber != job.PageDigests.Length ||
                values.Length != Math.Min(RebuildPageSize, job.Request.ExpectedRecords - job.StagedRecords))
            { throw new StorageConflictException(); }
            var lastKey = job.LastKey;
            foreach (var value in values)
            {
                var key = AnalyticsProjectionCodec.RecordKey(value);
                if (value.UserIdHash != owner || value.Date < job.Request.PeriodStart || value.Date > job.Request.PeriodEnd ||
                    lastKey is not null && string.CompareOrdinal(lastKey, key) >= 0)
                { throw new StorageConflictException(); }
                await records.InsertAsync("projection_records", GenerationOwner(owner, jobKey), key, AnalyticsProjectionCodec.Encode(value));
                lastKey = key;
            }
            var next = job with
            {
                StagedRecords = job.StagedRecords + values.Length,
                LastKey = lastKey,
                Digest = ChainDigest(job.Digest, pageDigest),
                PageDigests = job.PageDigests.Append(pageDigest).ToArray()
            };
            await WriteJobAsync(records, owner, durable, next);
            return Progress(next);
        }, token);
    }

    public Task<NativeAnalyticsRebuildProgress?> GetRebuildAsync(string owner, CancellationToken token = default)
    {
        AnalyticsProjectionCodec.Owner(owner);
        return context.WithRecordsAsync<NativeAnalyticsRebuildProgress?>(owner, async (records, _) =>
        {
            var durable = await records.GetAsync("projection_rebuild", owner, RebuildSlot);
            return durable is null ? null : Progress(ReadJob(durable, owner));
        }, token);
    }

    public Task<NativeAnalyticsRebuildProgress> CommitRebuildAsync(string owner, string jobKey, CancellationToken token = default)
    {
        AnalyticsProjectionCodec.Owner(owner);
        return context.WithRecordsAsync(owner, async (records, _) =>
        {
            var durable = await records.GetAsync("projection_rebuild", owner, RebuildSlot) ?? throw new StorageConflictException();
            var job = ReadJob(durable, owner);
            if (job.Key != jobKey) { throw new StorageConflictException(); }
            var (active, counts) = await ReadActiveAsync(records, owner);
            if (job.Status == "succeeded")
            {
                if (counts.Generation != jobKey) { throw new StorageUnavailableException(); }
                return Progress(job);
            }
            if (job.Status != "running" || job.StagedRecords != job.Request.ExpectedRecords ||
                job.Digest != job.Request.ExpectedDigest || (active?.Revision ?? -1) != job.BaseRevision || counts.Generation != job.BaseGeneration)
            { throw new StorageConflictException(); }
            var source = await ReadGenerationAsync(records, owner, jobKey, job.StagedRecords);
            if (CalculateSourceDigest(source) != job.Request.ExpectedDigest) { throw new StorageUnavailableException(); }
            var current = await ReadGenerationAsync(records, owner, counts.Generation, counts.Records);
            var byKey = current.ToDictionary(AnalyticsProjectionCodec.RecordKey, StringComparer.Ordinal);
            var sourceKeys = source.Select(AnalyticsProjectionCodec.RecordKey).ToHashSet(StringComparer.Ordinal);
            var preserved = current.Where(value => !sourceKeys.Contains(AnalyticsProjectionCodec.RecordKey(value)) &&
                (value.Date < job.Request.PeriodStart || value.Date > job.Request.PeriodEnd)).ToArray();
            if (source.Count + preserved.Length > MaximumRecordsPerOwner) { throw new StorageUnavailableException(); }
            foreach (var group in source.Concat(preserved).Where(value => value.Status == "active").GroupBy(value => (value.Currency, value.RecordType)))
            {
                try { group.Sum(value => value.Amount); }
                catch (OverflowException) { throw new StorageUnavailableException(); }
            }
            var events = counts.Events;
            foreach (var value in source.OrderBy(AnalyticsProjectionCodec.RecordKey, StringComparer.Ordinal))
            {
                var key = AnalyticsProjectionCodec.RecordKey(value);
                if (byKey.TryGetValue(key, out var existing))
                {
                    if (existing.Revision > value.Revision) { throw new StorageConflictException(); }
                    AnalyticsProjectionCodec.ShouldReplace(existing, value);
                }
                var headRecord = await records.GetAsync("projection_heads", owner, key);
                var head = headRecord is null ? null : AnalyticsProjectionCodec.Decode(headRecord, owner, key);
                if (byKey.TryGetValue(key, out existing) && existing != head) { throw new StorageUnavailableException(); }
                if (head is not null && head.Revision > value.Revision) { throw new StorageConflictException(); }
                if (AnalyticsProjectionCodec.ShouldReplace(head, value))
                {
                    if (headRecord is null) { await records.InsertAsync("projection_heads", owner, key, AnalyticsProjectionCodec.Encode(value)); }
                    else { await records.ReplaceAsync("projection_heads", owner, key, headRecord.Revision, AnalyticsProjectionCodec.Encode(value)); }
                }
                await VerifyOwnerAsync(records, "record_owners", key, owner);
                await VerifyOwnerAsync(records, "event_owners", value.EventId, owner);
                await InsertOwnerAsync(records, "record_owners", key, owner);
                await InsertOwnerAsync(records, "event_owners", value.EventId, owner);
                var receipt = await records.GetAsync("event_receipts", owner, value.EventId);
                var fingerprint = AnalyticsProjectionCodec.Fingerprint(value);
                if (receipt is null)
                {
                    if (++events > MaximumEventReceiptsPerOwner) { throw new StorageUnavailableException(); }
                    await records.InsertAsync("event_receipts", owner, value.EventId,
                        OwnedRecordCodec.Encode(1, new EventReceipt(owner, value.EventId, fingerprint, false, Array.Empty<string>(), Array.Empty<string>())));
                }
                else
                {
                    var original = ReadReceipt(receipt, owner, value.EventId);
                    if (original.Fingerprint != fingerprint)
                    { throw new StorageConflictException(); }
                }
            }
            foreach (var value in preserved)
            {
                await records.InsertAsync("projection_records", GenerationOwner(owner, jobKey),
                    AnalyticsProjectionCodec.RecordKey(value), AnalyticsProjectionCodec.Encode(value));
            }
            var nextCounts = new Counts(source.Count + preserved.Length, events, jobKey);
            await WriteActiveAsync(records, owner, active, nextCounts);
            var refreshCurrencies = source.Concat(current.Where(value => value.Date >= job.Request.PeriodStart && value.Date <= job.Request.PeriodEnd))
                .Select(value => value.Currency).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var next = job with
            {
                Status = "succeeded",
                CleanupGeneration = counts.Generation,
                RefreshCurrencies = refreshCurrencies,
                RefreshPending = refreshCurrencies.Length > 0
            };
            await WriteJobAsync(records, owner, durable, next);
            return Progress(next);
        }, token);
    }

    public Task<NativeAnalyticsRebuildProgress> CancelRebuildAsync(string owner, string jobKey, CancellationToken token = default)
    {
        AnalyticsProjectionCodec.Owner(owner);
        return context.WithRecordsAsync(owner, async (records, _) =>
        {
            var durable = await records.GetAsync("projection_rebuild", owner, RebuildSlot) ?? throw new StorageConflictException();
            var job = ReadJob(durable, owner);
            if (job.Key != jobKey || job.Status == "succeeded") { throw new StorageConflictException(); }
            if (job.Status == "cancelled") { return Progress(job); }
            var next = job with { Status = "cancelled", CleanupGeneration = jobKey };
            await WriteJobAsync(records, owner, durable, next);
            return Progress(next);
        }, token);
    }

    public Task<NativeAnalyticsRebuildProgress> CleanupRebuildAsync(string owner, string jobKey, CancellationToken token = default)
    {
        AnalyticsProjectionCodec.Owner(owner);
        return context.WithRecordsAsync(owner, async (records, _) =>
        {
            var durable = await records.GetAsync("projection_rebuild", owner, RebuildSlot) ?? throw new StorageConflictException();
            var job = ReadJob(durable, owner);
            if (job.Key != jobKey || job.Status == "running") { throw new StorageConflictException(); }
            if (job.CleanupGeneration is null) { return Progress(job); }
            var (_, counts) = await ReadActiveAsync(records, owner);
            if (counts.Generation == job.CleanupGeneration) { throw new StorageUnavailableException(); }
            var generationOwner = GenerationOwner(owner, job.CleanupGeneration);
            var page = await records.ReadPageAsync("projection_records", generationOwner, pageSize: 25);
            foreach (var item in page)
            { await records.DeletePageItemAsync("projection_records", generationOwner, item.Cursor, item.Revision); }
            var next = page.Count < 25 ? job with { CleanupGeneration = null } : job;
            await WriteJobAsync(records, owner, durable, next);
            return Progress(next);
        }, token);
    }

    public Task<AnalyticsRebuildRefresh?> ReadPendingRebuildRefreshAsync(string owner, CancellationToken token = default)
    {
        AnalyticsProjectionCodec.Owner(owner);
        return context.WithRecordsAsync<AnalyticsRebuildRefresh?>(owner, async (records, _) =>
        {
            var durable = await records.GetAsync("projection_rebuild", owner, RebuildSlot);
            if (durable is null) { return null; }
            var job = ReadJob(durable, owner);
            if (!job.RefreshPending) { return null; }
            var (active, counts) = await ReadActiveAsync(records, owner);
            if (active is null || counts.Generation != job.Key) { throw new StorageUnavailableException(); }
            return Refresh(job, active.Revision);
        }, token);
    }

    public Task MarkRebuildRefreshCompletedAsync(AnalyticsRebuildRefresh expected, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        AnalyticsProjectionCodec.Owner(expected.OwnerHash);
        return context.WithRecordsAsync(expected.OwnerHash, async (records, _) =>
        {
            var durable = await records.GetAsync("projection_rebuild", expected.OwnerHash, RebuildSlot) ?? throw new StorageConflictException();
            var job = ReadJob(durable, expected.OwnerHash);
            var (active, counts) = await ReadActiveAsync(records, expected.OwnerHash);
            if (active is null || counts.Generation != job.Key || expected.Currencies is null ||
                expected != Refresh(job, active.Revision) with { Currencies = expected.Currencies } ||
                !expected.Currencies.SequenceEqual(job.RefreshCurrencies))
            { throw new StorageConflictException(); }
            if (job.RefreshPending) { await WriteJobAsync(records, expected.OwnerHash, durable, job with { RefreshPending = false }); }
            return true;
        }, token);
    }

    private static void ValidateRequest(NativeAnalyticsRebuildSource request)
    {
        ArgumentNullException.ThrowIfNull(request);
        AnalyticsProjectionCodec.Owner(request.OwnerScopeHash);
        if (request.PeriodStart == default || request.PeriodEnd == default || request.PeriodEnd < request.PeriodStart ||
            request.PeriodEnd.DayNumber - request.PeriodStart.DayNumber >= 3650 ||
            string.IsNullOrWhiteSpace(request.SourceSnapshotVersion) || request.SourceSnapshotVersion.Length > 128 ||
            request.SourceSnapshotVersion != request.SourceSnapshotVersion.Trim() || request.SourceSnapshotVersion.Any(char.IsControl) ||
            request.RequestedAtUtc == default || request.RequestedAtUtc.Offset != TimeSpan.Zero ||
            request.ExpectedRecords is < 0 or > MaximumRecordsPerOwner || !ValidDigest(request.ExpectedDigest) ||
            request.ExpectedRecords == 0 && request.ExpectedDigest != EmptyDigest)
        { throw new ArgumentException("Invalid Analytics rebuild manifest."); }
    }

    private static RebuildJob ReadJob(DurableRecord durable, string owner)
    {
        try
        {
            var job = OwnedRecordCodec.Decode<RebuildJob>(1, durable);
            ValidateRequest(job.Request);
            if (job.Request.OwnerScopeHash != owner || job.Key != JobKey(job.Request) ||
                job.BaseRevision < -1 || !ValidGeneration(job.BaseGeneration) || job.Key == job.BaseGeneration ||
                job.StagedRecords < 0 || job.StagedRecords > job.Request.ExpectedRecords || !ValidDigest(job.Digest) ||
                job.PageDigests is null || job.PageDigests.Length != (job.StagedRecords + RebuildPageSize - 1) / RebuildPageSize ||
                job.PageDigests.Any(value => !ValidDigest(value)) ||
                (job.StagedRecords == 0) != (job.LastKey is null) ||
                job.Status is not ("running" or "succeeded" or "cancelled") ||
                job.RefreshCurrencies is null || job.RefreshCurrencies.Length > MaximumRecordsPerOwner * 2 ||
                job.RefreshCurrencies.Distinct(StringComparer.Ordinal).Count() != job.RefreshCurrencies.Length ||
                job.RefreshPending && (job.Status != "succeeded" || job.RefreshCurrencies.Length == 0) ||
                job.Status != "succeeded" && job.RefreshCurrencies.Length != 0 ||
                job.CleanupGeneration is not null && (!ValidGeneration(job.CleanupGeneration) ||
                    job.Status == "running" || job.Status == "succeeded" && job.CleanupGeneration != job.BaseGeneration ||
                    job.Status == "cancelled" && job.CleanupGeneration != job.Key) ||
                job.Status == "succeeded" && (job.StagedRecords != job.Request.ExpectedRecords || job.Digest != job.Request.ExpectedDigest))
            { throw new StorageUnavailableException(); }
            foreach (var currency in job.RefreshCurrencies) { AnalyticsProjectionCodec.Currency(currency); }
            return job;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private static Task<bool> WriteJobAsync(OwnedRecordTransaction records, string owner, DurableRecord? previous, RebuildJob job) =>
        previous is null ? records.InsertAsync("projection_rebuild", owner, RebuildSlot, OwnedRecordCodec.Encode(1, job)) :
            records.ReplaceAsync("projection_rebuild", owner, RebuildSlot, previous.Revision, OwnedRecordCodec.Encode(1, job));
    private static NativeAnalyticsRebuildProgress Progress(RebuildJob job) =>
        new(job.Key, Plan(job.Request).JobKey, AnalyticsRebuildStages.RebuildAnalytics, job.Status, job.StagedRecords,
            job.Request.ExpectedRecords, job.CleanupGeneration is not null, job.RefreshPending);
    private static AnalyticsRebuildPlanResponse Plan(NativeAnalyticsRebuildSource request) => new AnalyticsRebuildPlanner().Create(
        new AnalyticsRebuildRequest(request.OwnerScopeHash, request.PeriodStart, request.PeriodEnd, request.SourceSnapshotVersion, request.RequestedAtUtc));
    private static string JobKey(NativeAnalyticsRebuildSource request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Plan(request).JobKey))).ToLowerInvariant();
    private static AnalyticsRebuildRefresh Refresh(RebuildJob job, long observedRevision) => new(job.Request.OwnerScopeHash,
        job.Key, Plan(job.Request).JobKey, job.Request.SourceSnapshotVersion, job.Request.ExpectedDigest, job.Request.PeriodStart,
        job.Request.PeriodEnd, observedRevision, job.RefreshCurrencies);
    private static string PageDigest(IEnumerable<AnalyticsRecordProjection> page) =>
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(string.Concat(page.Select(AnalyticsProjectionCodec.Fingerprint)))));
    private static string ChainDigest(string previous, string page) =>
        Convert.ToHexString(SHA256.HashData(Convert.FromHexString(previous).Concat(Convert.FromHexString(page)).ToArray()));

    private sealed record RebuildJob([property: JsonRequired] string Key, [property: JsonRequired] NativeAnalyticsRebuildSource Request,
        [property: JsonRequired] long BaseRevision, [property: JsonRequired] string BaseGeneration,
        [property: JsonRequired] int StagedRecords, [property: JsonRequired] string Digest,
        [property: JsonRequired] string? LastKey, [property: JsonRequired] string[] PageDigests,
        [property: JsonRequired] string Status, [property: JsonRequired] string? CleanupGeneration,
        [property: JsonRequired] string[] RefreshCurrencies, [property: JsonRequired] bool RefreshPending);
}
