using System.Text.Json.Serialization;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Analytics.Infrastructure.Persistence;

public sealed partial class PostgresAnalyticsReadModelStore(AnalyticsPersistenceContext context) : IAnalyticsReadModelStore
{
    public const int MaximumRecordsPerOwner = 1000;
    public const int MaximumEventReceiptsPerOwner = 10000;

    public Task<AnalyticsProjectionWriteOutcome> UpsertIfNewerAsync(AnalyticsRecordProjection projection, CancellationToken cancellationToken)
    {
        AnalyticsProjectionCodec.Validate(projection);
        context.ValidateProjectionScope(projection);
        var owner = projection.UserIdHash;
        var key = AnalyticsProjectionCodec.RecordKey(projection);
        return context.WithRecordsAsync(owner, async (records, _) =>
        {
            await VerifyOwnerAsync(records, "record_owners", key, owner);
            await VerifyOwnerAsync(records, "event_owners", projection.EventId, owner);
            var (countsRecord, counts) = await ReadActiveAsync(records, owner);
            var fingerprint = AnalyticsProjectionCodec.Fingerprint(projection);
            var receipt = await records.GetAsync("event_receipts", owner, projection.EventId);
            if (receipt is not null)
            {
                var original = ReadReceipt(receipt, owner, projection.EventId);
                if (original.Fingerprint != fingerprint) { throw new StorageConflictException(); }
                return new AnalyticsProjectionWriteOutcome(false, original.PendingCurrencies);
            }
            var recordOwner = GenerationOwner(owner, counts.Generation);
            var currentRecord = await records.GetAsync("projection_records", recordOwner, key);
            var current = currentRecord is null ? null : AnalyticsProjectionCodec.Decode(currentRecord, owner, key);
            var headRecord = await records.GetAsync("projection_heads", owner, key);
            var head = headRecord is null ? null : AnalyticsProjectionCodec.Decode(headRecord, owner, key);
            if (current is not null && current != head) { throw new StorageUnavailableException(); }
            var replace = AnalyticsProjectionCodec.ShouldReplace(head, projection);
            if (counts.Events >= MaximumEventReceiptsPerOwner || replace && current is null && counts.Records >= MaximumRecordsPerOwner)
            { throw new StorageUnavailableException(); }
            await InsertOwnerAsync(records, "record_owners", key, owner);
            await InsertOwnerAsync(records, "event_owners", projection.EventId, owner);
            var currencies = replace ? new[] { current?.Currency, projection.Currency }.OfType<string>()
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() : Array.Empty<string>();
            await records.InsertAsync("event_receipts", owner, projection.EventId, OwnedRecordCodec.Encode(1,
                new EventReceipt(owner, projection.EventId, fingerprint, replace, currencies, currencies)));
            if (replace)
            {
                var payload = AnalyticsProjectionCodec.Encode(projection);
                if (currentRecord is null) { await records.InsertAsync("projection_records", recordOwner, key, payload); }
                else { await records.ReplaceAsync("projection_records", recordOwner, key, currentRecord.Revision, payload); }
                if (headRecord is null) { await records.InsertAsync("projection_heads", owner, key, payload); }
                else { await records.ReplaceAsync("projection_heads", owner, key, headRecord.Revision, payload); }
            }
            await WriteActiveAsync(records, owner, countsRecord, new Counts(counts.Records + (replace && current is null ? 1 : 0),
                counts.Events + 1, counts.Generation));
            return new AnalyticsProjectionWriteOutcome(replace, currencies);
        }, cancellationToken, new[] { "record:" + key, "event:" + projection.EventId });
    }

    public Task MarkPublicationCompletedAsync(string sourceEventId, string userIdHash, string currency, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Native Analytics completion requires an exact durable outbox acknowledgement.");

    public Task<AnalyticsProjectionSnapshot> GetAsync(string userIdHash, string currency, CancellationToken cancellationToken)
    {
        AnalyticsProjectionCodec.Owner(userIdHash); AnalyticsProjectionCodec.Currency(currency);
        return context.WithRecordsAsync(userIdHash, async (records, _) =>
        {
            var (_, counts) = await ReadActiveAsync(records, userIdHash);
            var values = await ReadGenerationAsync(records, userIdHash, counts.Generation, counts.Records);
            try { return AnalyticsSnapshotBuilder.Build(userIdHash, currency, values); }
            catch (OverflowException) { throw new StorageUnavailableException(); }
        }, cancellationToken);
    }

    public Task ResetAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Native Analytics requires a verified scoped rebuild; global reset is forbidden.");

    internal static async Task CompletePublicationAsync(OwnedRecordTransaction records, string owner, string eventId, string currency)
    {
        var durable = await records.GetAsync("event_receipts", owner, eventId) ?? throw new StorageUnavailableException();
        var receipt = ReadReceipt(durable, owner, eventId);
        if (!receipt.OriginalCurrencies.Contains(currency, StringComparer.Ordinal)) { throw new StorageConflictException(); }
        if (!receipt.PendingCurrencies.Contains(currency, StringComparer.Ordinal)) { return; }
        await records.ReplaceAsync("event_receipts", owner, eventId, durable.Revision, OwnedRecordCodec.Encode(1,
            receipt with { PendingCurrencies = receipt.PendingCurrencies.Where(value => value != currency).ToArray() }));
    }

    internal static async Task RequirePendingPublicationAsync(OwnedRecordTransaction records, string owner, string eventId, string currency)
    {
        var durable = await records.GetAsync("event_receipts", owner, eventId) ?? throw new StorageUnavailableException();
        if (!ReadReceipt(durable, owner, eventId).PendingCurrencies.Contains(currency, StringComparer.Ordinal))
        { throw new StorageConflictException(); }
    }

    internal static async Task<IReadOnlyList<string>> ReadPendingCurrenciesAsync(OwnedRecordTransaction records, string owner, string eventId)
    {
        var durable = await records.GetAsync("event_receipts", owner, eventId) ?? throw new StorageUnavailableException();
        return ReadReceipt(durable, owner, eventId).PendingCurrencies;
    }

    private static EventReceipt ReadReceipt(DurableRecord durable, string owner, string eventId)
    {
        try
        {
            var value = OwnedRecordCodec.Decode<EventReceipt>(1, durable);
            if (value.Owner != owner || value.EventId != eventId || !ValidDigest(value.Fingerprint) ||
                value.OriginalCurrencies is null || value.PendingCurrencies is null || value.OriginalCurrencies.Length > 2 ||
                (value.OriginalCurrencies.Length > 0) != value.Accepted ||
                value.OriginalCurrencies.Distinct(StringComparer.Ordinal).Count() != value.OriginalCurrencies.Length ||
                value.PendingCurrencies.Distinct(StringComparer.Ordinal).Count() != value.PendingCurrencies.Length ||
                value.PendingCurrencies.Any(currency => !value.OriginalCurrencies.Contains(currency, StringComparer.Ordinal)) ||
                durable.Revision != value.OriginalCurrencies.Length - value.PendingCurrencies.Length)
            { throw new StorageUnavailableException(); }
            foreach (var currency in value.OriginalCurrencies) { AnalyticsProjectionCodec.Currency(currency); }
            return value;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private static async Task<(DurableRecord? Durable, Counts Counts)> ReadActiveAsync(OwnedRecordTransaction records, string owner)
    {
        var durable = await records.GetAsync("projection_counts", owner, "active");
        var marker = await records.GetAsync("projection_owner", owner, "v1");
        if ((durable is null) != (marker is null) || marker is not null &&
            (marker.Revision != 0 || OwnedRecordCodec.Decode<OwnerMarker>(1, marker).Owner != owner) ||
            durable is null && await records.GetAsync("projection_rebuild", owner, RebuildSlot) is not null)
        { throw new StorageUnavailableException(); }
        if (durable is null) { return (null, new Counts(0, 0, "initial")); }
        var value = OwnedRecordCodec.Decode<Counts>(1, durable);
        if (value.Records is < 0 or > MaximumRecordsPerOwner || value.Events is < 0 or > MaximumEventReceiptsPerOwner ||
            value.Events < value.Records || !ValidGeneration(value.Generation))
        { throw new StorageUnavailableException(); }
        return (durable, value);
    }

    private static async Task WriteActiveAsync(OwnedRecordTransaction records, string owner, DurableRecord? previous, Counts value)
    {
        if (previous is null)
        {
            await records.InsertAsync("projection_owner", owner, "v1", OwnedRecordCodec.Encode(1, new OwnerMarker(owner)));
            await records.InsertAsync("projection_counts", owner, "active", OwnedRecordCodec.Encode(1, value));
        }
        else { await records.ReplaceAsync("projection_counts", owner, "active", previous.Revision, OwnedRecordCodec.Encode(1, value)); }
    }

    private static async Task<IReadOnlyList<AnalyticsRecordProjection>> ReadGenerationAsync(OwnedRecordTransaction records,
        string owner, string generation, int expectedCount)
    {
        var values = new List<AnalyticsRecordProjection>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await records.ReadPageAsync("projection_records", GenerationOwner(owner, generation), cursor);
            foreach (var item in page)
            {
                var value = AnalyticsProjectionCodec.Decode(new DurableRecord(item.Revision, item.Payload), owner);
                if (!keys.Add(AnalyticsProjectionCodec.RecordKey(value)) || keys.Count > expectedCount)
                { throw new StorageUnavailableException(); }
                values.Add(value);
            }
            cursor = page.Count < 100 ? null : page[^1].Cursor;
        } while (cursor is not null);
        if (keys.Count != expectedCount) { throw new StorageUnavailableException(); }
        return values;
    }

    private static async Task VerifyOwnerAsync(OwnedRecordTransaction records, string bucket, string id, string owner)
    {
        var marker = await records.GetAsync(bucket, "analytics", id);
        if (marker is not null && (marker.Revision != 0 || OwnedRecordCodec.Decode<OwnerMarker>(1, marker).Owner != owner))
        { throw new StorageConflictException(); }
    }
    private static async Task InsertOwnerAsync(OwnedRecordTransaction records, string bucket, string id, string owner)
    {
        if (await records.GetAsync(bucket, "analytics", id) is null)
        { await records.InsertAsync(bucket, "analytics", id, OwnedRecordCodec.Encode(1, new OwnerMarker(owner))); }
    }

    private static string GenerationOwner(string owner, string generation) => owner + "|" + generation;
    private static bool ValidGeneration(string value) => value == "initial" ||
        value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool ValidDigest(string value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private sealed record Counts([property: JsonRequired] int Records, [property: JsonRequired] int Events,
        [property: JsonRequired] string Generation);
    private sealed record OwnerMarker([property: JsonRequired] string Owner);
    private sealed record EventReceipt([property: JsonRequired] string Owner, [property: JsonRequired] string EventId,
        [property: JsonRequired] string Fingerprint, [property: JsonRequired] bool Accepted,
        [property: JsonRequired] string[] OriginalCurrencies, [property: JsonRequired] string[] PendingCurrencies);
}
