using System.Text.Json.Serialization;
using FinancialAssistant.FinancialSummary.Application;
using FinancialAssistant.FinancialSummary.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.FinancialSummary.Infrastructure.Persistence;

public sealed partial class PostgresFinancialSummaryReadModelStore : IFinancialSummaryReadModelStore
{
    public const int MaximumRecordsPerOwner = 1000;
    public const int MaximumEventReceiptsPerOwner = 10000;
    private readonly OwnedPostgresStore database;

    public PostgresFinancialSummaryReadModelStore(OwnedPostgresStore database)
    {
        if (database.ServiceId != "financial-summary")
        { throw new ArgumentException("Summary requires its independent module-owned storage role."); }
        this.database = database;
    }

    public async Task UpsertIfNewerAsync(FinancialRecordProjection projection, CancellationToken cancellationToken)
    {
        SummaryProjectionCodec.Validate(projection);
        var owner = projection.UserIdHash;
        var key = SummaryProjectionCodec.RecordKey(projection);
        await database.ExecuteAsync(new[] { "owner:" + owner, "record:" + key, "event:" + projection.EventId }, async (records, _) =>
        {
            await VerifyOwnerAsync(records, "record_owners", key, owner);
            await VerifyOwnerAsync(records, "event_owners", projection.EventId, owner);
            var (countersRecord, counters) = await ReadActiveAsync(records, owner);
            var receipt = await records.GetAsync("event_receipts", owner, projection.EventId);
            var fingerprint = SummaryProjectionCodec.Fingerprint(projection);
            if (receipt is not null)
            {
                var original = OwnedRecordCodec.Decode<EventReceipt>(1, receipt);
                if (receipt.Revision != 0 || original.Owner != owner || original.EventId != projection.EventId ||
                    original.Fingerprint != fingerprint)
                { throw new StorageConflictException(); }
                return false;
            }

            var recordOwner = GenerationOwner(owner, counters.Generation);
            var currentRecord = await records.GetAsync("projection_records", recordOwner, key);
            var current = currentRecord is null ? null : SummaryProjectionCodec.Decode(currentRecord, owner, key);
            var headRecord = await records.GetAsync("projection_heads", owner, key);
            var head = headRecord is null ? null : SummaryProjectionCodec.Decode(headRecord, owner, key);
            if (current is not null && current != head) { throw new StorageUnavailableException(); }
            var replace = SummaryProjectionCodec.ShouldReplace(head, projection);
            if (counters.Events >= MaximumEventReceiptsPerOwner || replace && current is null && counters.Records >= MaximumRecordsPerOwner)
            { throw new StorageUnavailableException(); }
            await InsertOwnerAsync(records, "record_owners", key, owner);
            await InsertOwnerAsync(records, "event_owners", projection.EventId, owner);
            await records.InsertAsync("event_receipts", owner, projection.EventId,
                OwnedRecordCodec.Encode(1, new EventReceipt(owner, projection.EventId, fingerprint)));
            if (replace)
            {
                var payload = SummaryProjectionCodec.Encode(projection);
                if (currentRecord is null) { await records.InsertAsync("projection_records", recordOwner, key, payload); }
                else { await records.ReplaceAsync("projection_records", recordOwner, key, currentRecord.Revision, payload); }
                if (headRecord is null) { await records.InsertAsync("projection_heads", owner, key, payload); }
                else { await records.ReplaceAsync("projection_heads", owner, key, headRecord.Revision, payload); }
            }
            var next = new Counts(counters.Records + (replace && current is null ? 1 : 0), counters.Events + 1, counters.Generation);
            await WriteActiveAsync(records, owner, countersRecord, next);
            return replace;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<FinancialRecordProjection>> ListAsync(string userIdHash, string currency,
        CancellationToken cancellationToken)
    {
        SummaryProjectionCodec.Owner(userIdHash); SummaryProjectionCodec.Currency(currency);
        return database.ExecuteAsync<IReadOnlyList<FinancialRecordProjection>>(new[] { "owner:" + userIdHash }, async (records, _) =>
        {
            var (_, counters) = await ReadActiveAsync(records, userIdHash);
            var values = await ReadGenerationAsync(records, userIdHash, counters.Generation, counters.Records);
            return values.Where(value => value.Currency == currency).OrderBy(value => value.RecordType, StringComparer.Ordinal)
                .ThenBy(value => value.RecordId, StringComparer.Ordinal).ToArray();
        }, cancellationToken);
    }

    public Task ResetAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Native Summary requires a verified scoped rebuild; global reset is forbidden.");

    private static Counts ReadCounters(DurableRecord? durable)
    {
        if (durable is null) { return new Counts(0, 0, "initial"); }
        var value = OwnedRecordCodec.Decode<Counts>(1, durable);
        if (value.Records is < 0 or > MaximumRecordsPerOwner || value.Events is < 0 or > MaximumEventReceiptsPerOwner ||
            value.Events < value.Records || !ValidGeneration(value.Generation))
        { throw new StorageUnavailableException(); }
        return value;
    }

    private static async Task<(DurableRecord? Durable, Counts Counts)> ReadActiveAsync(OwnedRecordTransaction records, string owner)
    {
        var durable = await records.GetAsync("projection_counts", owner, "active");
        var marker = await records.GetAsync("projection_owner", owner, "v1");
        if ((durable is null) != (marker is null) || marker is not null &&
            (marker.Revision != 0 || OwnedRecordCodec.Decode<OwnerMarker>(1, marker).Owner != owner) ||
            durable is null && await records.GetAsync("projection_rebuild", owner, RebuildSlot) is not null)
        { throw new StorageUnavailableException(); }
        return (durable, ReadCounters(durable));
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

    private static async Task VerifyOwnerAsync(OwnedRecordTransaction records, string bucket, string id, string owner)
    {
        var marker = await records.GetAsync(bucket, "summary", id);
        if (marker is not null && (marker.Revision != 0 || OwnedRecordCodec.Decode<OwnerMarker>(1, marker).Owner != owner))
        { throw new StorageConflictException(); }
    }

    private static async Task InsertOwnerAsync(OwnedRecordTransaction records, string bucket, string id, string owner)
    {
        if (await records.GetAsync(bucket, "summary", id) is null)
        { await records.InsertAsync(bucket, "summary", id, OwnedRecordCodec.Encode(1, new OwnerMarker(owner))); }
    }

    private static string GenerationOwner(string owner, string generation) => owner + "|" + generation;
    private static bool ValidGeneration(string value) => value == "initial" ||
        value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static async Task<IReadOnlyList<FinancialRecordProjection>> ReadGenerationAsync(OwnedRecordTransaction records,
        string owner, string generation, int expectedCount)
    {
        var values = new List<FinancialRecordProjection>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await records.ReadPageAsync("projection_records", GenerationOwner(owner, generation), cursor);
            foreach (var item in page)
            {
                var value = SummaryProjectionCodec.Decode(new DurableRecord(item.Revision, item.Payload), owner);
                if (!keys.Add(SummaryProjectionCodec.RecordKey(value)) || keys.Count > expectedCount)
                { throw new StorageUnavailableException(); }
                values.Add(value);
            }
            cursor = page.Count < 100 ? null : page[^1].Cursor;
        } while (cursor is not null);
        if (keys.Count != expectedCount) { throw new StorageUnavailableException(); }
        return values;
    }

    private sealed record Counts([property: JsonRequired] int Records, [property: JsonRequired] int Events,
        [property: JsonRequired] string Generation);
    private sealed record OwnerMarker([property: JsonRequired] string Owner);
    private sealed record EventReceipt([property: JsonRequired] string Owner, [property: JsonRequired] string EventId,
        [property: JsonRequired] string Fingerprint);
}
