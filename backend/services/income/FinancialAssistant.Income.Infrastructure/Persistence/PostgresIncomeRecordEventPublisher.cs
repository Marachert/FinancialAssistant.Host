using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Domain;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Income.Infrastructure.Persistence;

public sealed class PostgresIncomeRecordEventPublisher(IncomePersistenceContext context) : IIncomeRecordEventPublisher
{
    public Task PublishAsync(string eventType, IncomeRecord record, string correlationId, string causationId,
        CancellationToken cancellationToken)
    {
        IncomeRecordSnapshot.Validate(record);
        var envelope = CreateEnvelope(eventType, record, correlationId, causationId);
        return context.WithRecordsAsync(record.UserId, async (records, _) =>
        {
            var existing = await records.GetAsync("financial_outbox", "income", envelope.EventId);
            if (existing is not null)
            {
                var message = Decode(existing);
                if (message.Event.Payload != envelope.Payload || message.Event.UserIdHash != envelope.UserIdHash
                    || message.Event.EventType != envelope.EventType || message.Event.OccurredAtUtc != envelope.OccurredAtUtc)
                { throw new FinancialConfirmationConflictException(); }
                return true;
            }
            await records.InsertAsync("financial_outbox", "income", envelope.EventId,
                OwnedRecordCodec.Encode(1, new IncomeOutboxMessage(envelope, null)));
            return true;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<IncomeOutboxMessage>> ReadPendingAsync(CancellationToken cancellationToken = default) =>
        context.WithRecordsAsync<IReadOnlyList<IncomeOutboxMessage>>("outbox-maintenance", async (records, _) =>
        {
            var scan = await records.GetAsync("outbox_scan", "income", "pending");
            var after = scan is null ? null : OwnedRecordCodec.Decode<ScanCursor>(1, scan).After;
            var pending = new List<IncomeOutboxMessage>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var wrapped = false;
            for (var page = 0; page < 10 && pending.Count < 25; page++)
            {
                var items = await records.ReadPageAsync("financial_outbox", "income", after);
                if (items.Count == 0)
                {
                    after = null;
                    if (wrapped) { break; }
                    wrapped = true;
                    continue;
                }
                foreach (var item in items)
                {
                    var message = Decode(new DurableRecord(item.Revision, item.Payload));
                    after = item.Cursor;
                    if (message.PublishedAtUtc is null && seen.Add(message.Event.EventId)) { pending.Add(message); }
                    if (pending.Count == 25) { break; }
                }
            }
            var encoded = OwnedRecordCodec.Encode(1, new ScanCursor(after));
            if (scan is null) { await records.InsertAsync("outbox_scan", "income", "pending", encoded); }
            else { await records.ReplaceAsync("outbox_scan", "income", "pending", scan.Revision, encoded); }
            return pending;
        }, cancellationToken);

    public async Task MarkPublishedAsync(IncomeOutboxMessage expected, DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (expected is null || publishedAtUtc == default || publishedAtUtc.Offset != TimeSpan.Zero)
        { throw new ArgumentException("Invalid financial outbox acknowledgement."); }
        ValidateEnvelope(expected.Event);
        await context.WithRecordsAsync("outbox-maintenance", async (records, _) =>
        {
            var existing = await records.GetAsync("financial_outbox", "income", expected.Event.EventId) ?? throw new StorageUnavailableException();
            var message = Decode(existing);
            if (message.Event != expected.Event || publishedAtUtc < message.Event.OccurredAtUtc) { throw new StorageUnavailableException(); }
            if (message.PublishedAtUtc is null)
            {
                await records.ReplaceAsync("financial_outbox", "income", expected.Event.EventId, existing.Revision,
                    OwnedRecordCodec.Encode(1, message with { PublishedAtUtc = publishedAtUtc }));
            }
            return true;
        }, cancellationToken);
    }

    public Task<int> CleanupPublishedBatchAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
        context.WithRecordsAsync("outbox-maintenance", async (records, _) =>
        {
            var scan = await records.GetAsync("outbox_scan", "income", "retention");
            var after = scan is null ? null : OwnedRecordCodec.Decode<ScanCursor>(1, scan).After;
            var items = await records.ReadPageAsync("financial_outbox", "income", after, 25);
            var count = 0;
            foreach (var item in items)
            {
                var message = Decode(new DurableRecord(item.Revision, item.Payload));
                if (message.PublishedAtUtc is not null && nowUtc >= message.PublishedAtUtc.Value.AddDays(30))
                { await records.DeletePageItemAsync("financial_outbox", "income", item.Cursor, item.Revision); count++; }
            }
            var cursor = new ScanCursor(items.Count < 25 ? null : items[^1].Cursor);
            if (scan is null) { await records.InsertAsync("outbox_scan", "income", "retention", OwnedRecordCodec.Encode(1, cursor)); }
            else { await records.ReplaceAsync("outbox_scan", "income", "retention", scan.Revision, OwnedRecordCodec.Encode(1, cursor)); }
            return count;
        }, cancellationToken);

    private static IntegrationEventEnvelope<FinancialRecordChangedV1> CreateEnvelope(string eventType, IncomeRecord record,
        string correlationId, string causationId)
    {
        var eventId = HashIdentifier($"{eventType}|{record.TransactionId}|{record.Revision}");
        var changedAt = record.UpdatedAtUtc ?? record.ConfirmedAtUtc;
        var envelope = new IntegrationEventEnvelope<FinancialRecordChangedV1>(eventId, eventId, eventType, changedAt,
            "income-service", FinancialRecordEventTypes.SchemaVersion, correlationId, causationId, HashIdentifier(record.UserId),
            new FinancialRecordChangedV1(record.TransactionId, record.Amount, record.Currency, record.CategoryId, record.Date,
                record.Status, record.Revision, record.Origin, changedAt));
        ValidateEnvelope(envelope);
        return envelope;
    }

    private static IncomeOutboxMessage Decode(DurableRecord record)
    {
        try
        {
            var message = OwnedRecordCodec.Decode<IncomeOutboxMessage>(1, record);
            ValidateEnvelope(message.Event);
            if (message.PublishedAtUtc is not null && (message.PublishedAtUtc.Value.Offset != TimeSpan.Zero
                || message.PublishedAtUtc < message.Event.OccurredAtUtc)) { throw new StorageUnavailableException(); }
            return message;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private static void ValidateEnvelope(IntegrationEventEnvelope<FinancialRecordChangedV1> value)
    {
        if (value is null || value.Payload is null) { throw new ArgumentException("Invalid financial event."); }
        var payload = value.Payload;
        IncomeRecordSnapshot.ValidateIdentifier(payload.RecordId);
        if (value.EventType is not (FinancialRecordEventTypes.IncomeCreated or FinancialRecordEventTypes.IncomeUpdated
                or FinancialRecordEventTypes.IncomeArchived or FinancialRecordEventTypes.IncomeRestored)
            || value.EventId != HashIdentifier($"{value.EventType}|{payload.RecordId}|{payload.Revision}")
            || value.OccurrenceId != value.EventId || value.Producer != "income-service" || value.SchemaVersion != 1
            || value.UserIdHash is null || value.UserIdHash.Length != 64 || !value.UserIdHash.All(char.IsAsciiHexDigit)
            || payload.Amount <= 0 || payload.Amount > 999_999_999_999.99m || decimal.Round(payload.Amount, 2) != payload.Amount
            || payload.Currency is not ("USD" or "EUR" or "GBP" or "UAH")
            || payload.CategoryId is null || !payload.CategoryId.StartsWith("income.", StringComparison.Ordinal)
            || payload.Date == default || payload.Revision < 0
            || payload.Status is not (IncomeRecordStatuses.Active or IncomeRecordStatuses.Archived)
            || payload.Origin is not (IncomeRecordOrigins.Manual or IncomeRecordOrigins.ConfirmedTransaction)
            || payload.ChangedAtUtc != value.OccurredAtUtc)
        { throw new ArgumentException("Invalid financial event."); }
    }

    private static string HashIdentifier(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private sealed record ScanCursor([property: JsonRequired] string? After);
}

public sealed record IncomeOutboxMessage([property: JsonRequired] IntegrationEventEnvelope<FinancialRecordChangedV1> Event,
    [property: JsonRequired] DateTimeOffset? PublishedAtUtc);
