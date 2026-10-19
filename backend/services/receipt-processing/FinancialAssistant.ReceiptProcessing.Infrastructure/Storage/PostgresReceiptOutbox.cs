using System.Text.Json.Serialization;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

public sealed class PostgresReceiptOutbox(ReceiptPersistenceContext context) : IReceiptUploadedPublisher, IOcrCompletedPublisher
{
    public bool ConfirmsDelivery => false;

    public Task PublishAsync(ReceiptUploadedIntegrationEvent value, CancellationToken token)
    {
        ReceiptRecordCodec.Uploaded(value);
        return context.WithRecordsAsync(value.UserId, async (records, _) =>
        {
            var durable = await records.GetAsync("receipts", value.UserId, value.ReceiptId) ?? throw new StorageUnavailableException();
            var file = ReceiptRecordCodec.Decode<ReceiptFileMetadata>(durable, ReceiptRecordCodec.File);
            var expected = ReceiptOutboxMessage.For(file);
            if (!Same(expected, new ReceiptOutboxMessage(value, null, null))) { throw new StorageConflictException(); }
            await EnqueueAsync(records, expected); return true;
        }, token);
    }

    public Task PublishAsync(OcrCompletedIntegrationEvent value, CancellationToken token) =>
        context.WithRecordsAsync(value.UserId, async (records, _) =>
        {
            var durable = await records.GetAsync("ocr_results", value.UserId, value.ReceiptId) ?? throw new StorageUnavailableException();
            var result = ReceiptRecordCodec.Decode<StoredOcrProcessing>(durable, ReceiptRecordCodec.Processing);
            if (result.Metadata.UserId != value.UserId || result.Metadata.ReceiptId != value.ReceiptId || result.IntegrationEvent is null ||
                !Same(ReceiptOutboxMessage.For(result.IntegrationEvent), ReceiptOutboxMessage.For(value)))
            { throw new StorageConflictException(); }
            await EnqueueAsync(records, ReceiptOutboxMessage.For(value)); return true;
        }, token);

    internal static async Task EnqueueAsync(OwnedRecordTransaction records, ReceiptOutboxMessage value)
    {
        Validate(value);
        var existing = await records.GetAsync("receipt_outbox", "receipt", value.Key);
        if (existing is not null)
        {
            if (!Same(Decode(existing), value)) { throw new StorageConflictException(); }
            return;
        }
        await records.InsertAsync("receipt_outbox", "receipt", value.Key, OwnedRecordCodec.Encode(1, value));
    }

    public Task<IReadOnlyList<ReceiptOutboxMessage>> ReadPendingAsync(CancellationToken token = default) =>
        context.WithRecordsAsync<IReadOnlyList<ReceiptOutboxMessage>>("outbox-maintenance", async (records, _) =>
        {
            var scan = await records.GetAsync("receipt_scan", "receipt", "pending");
            var after = scan is null ? null : OwnedRecordCodec.Decode<ScanCursor>(1, scan).After;
            var result = new List<ReceiptOutboxMessage>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var wrapped = false;
            for (var page = 0; page < 10 && result.Count < 25; page++)
            {
                var items = await records.ReadPageAsync("receipt_outbox", "receipt", after);
                if (items.Count == 0)
                {
                    after = null; if (wrapped) { break; }
                    wrapped = true; continue;
                }
                foreach (var item in items)
                {
                    var value = Decode(new DurableRecord(item.Revision, item.Payload));
                    after = item.Cursor;
                    if (value.PublishedAtUtc is null && seen.Add(value.Key)) { result.Add(value); }
                    if (result.Count == 25) { break; }
                }
            }
            var encoded = OwnedRecordCodec.Encode(1, new ScanCursor(after));
            if (scan is null) { await records.InsertAsync("receipt_scan", "receipt", "pending", encoded); }
            else { await records.ReplaceAsync("receipt_scan", "receipt", "pending", scan.Revision, encoded); }
            return result;
        }, token);

    public Task MarkPublishedAsync(ReceiptOutboxMessage expected, DateTimeOffset acknowledgedAtUtc, CancellationToken token = default)
    {
        Validate(expected);
        if (acknowledgedAtUtc.Offset != TimeSpan.Zero || acknowledgedAtUtc < expected.OccurredAtUtc)
        { throw new ArgumentException("Invalid Receipt transport acknowledgement time."); }
        return context.WithRecordsAsync(expected.UserId, async (records, _) =>
        {
            var durable = await records.GetAsync("receipt_outbox", "receipt", expected.Key) ?? throw new StorageUnavailableException();
            var value = Decode(durable);
            if (!Same(value, expected)) { throw new StorageConflictException(); }
            if (value.PublishedAtUtc is null)
            {
                await records.ReplaceAsync("receipt_outbox", "receipt", value.Key,
                durable.Revision, OwnedRecordCodec.Encode(1, value with { PublishedAtUtc = acknowledgedAtUtc }));
            }
            return true;
        }, token);
    }

    internal static async Task VerifyAcknowledgedAsync(OwnedRecordTransaction records, string key, string owner, string receiptId)
    {
        var durable = await records.GetAsync("receipt_outbox", "receipt", key) ?? throw new StorageUnavailableException();
        var value = Decode(durable);
        if (value.PublishedAtUtc is null || value.UserId != owner || value.ReceiptId != receiptId) { throw new StorageUnavailableException(); }
    }

    private static bool Same(ReceiptOutboxMessage a, ReceiptOutboxMessage b) =>
        OwnedRecordCodec.Encode(1, a with { PublishedAtUtc = null }).AsSpan().SequenceEqual(
            OwnedRecordCodec.Encode(1, b with { PublishedAtUtc = null }));

    private static ReceiptOutboxMessage Decode(DurableRecord durable) => ReceiptRecordCodec.Decode<ReceiptOutboxMessage>(durable, Validate);

    private static void Validate(ReceiptOutboxMessage value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Uploaded is not null && value.Completed is null) { ReceiptRecordCodec.Uploaded(value.Uploaded); }
        else if (value.Uploaded is null && value.Completed is not null)
        {
            ReceiptRecordCodec.Completed(value.Completed);
        }
        else { throw new ArgumentException("Invalid Receipt publication kind."); }
        if (value.PublishedAtUtc is not null && (value.PublishedAtUtc.Value.Offset != TimeSpan.Zero || value.PublishedAtUtc < value.OccurredAtUtc))
        { throw new ArgumentException("Invalid Receipt publication acknowledgement."); }
    }

    private sealed record ScanCursor([property: JsonRequired] string? After);
}

public sealed record ReceiptOutboxMessage([property: JsonRequired] ReceiptUploadedIntegrationEvent? Uploaded,
    [property: JsonRequired] OcrCompletedIntegrationEvent? Completed, [property: JsonRequired] DateTimeOffset? PublishedAtUtc)
{
    [JsonIgnore] public string Key => Uploaded is not null ? "uploaded:" + Uploaded.EventId : "completed:" + Completed?.EventId;
    [JsonIgnore] public string UserId => Uploaded?.UserId ?? Completed?.UserId ?? "";
    [JsonIgnore] public string ReceiptId => Uploaded?.ReceiptId ?? Completed?.ReceiptId ?? "";
    [JsonIgnore] public DateTimeOffset OccurredAtUtc => Uploaded?.OccurredAtUtc ?? Completed?.OccurredAtUtc ?? default;
    public static ReceiptOutboxMessage For(ReceiptFileMetadata value) => new(new ReceiptUploadedIntegrationEvent(
        ReceiptUploadedIntegrationEvent.Name, value.ReceiptUploadedEventId, value.ReceiptId, value.UserId, value.ContentType, value.UploadedAtUtc), null, null);
    public static ReceiptOutboxMessage For(OcrCompletedIntegrationEvent value) => new(null, value, null);
}
