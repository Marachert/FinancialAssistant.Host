using System.Text.Json.Serialization;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Infrastructure.Storage;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Events;

public sealed class PostgresIntakeOutbox(IntakePersistenceContext context) : ITransactionDraftCreatedPublisher, ITransactionConfirmedPublisher
{
    public Task PublishAsync(TransactionDraftCreatedIntegrationEvent value, CancellationToken token)
    {
        IntakeRecordCodec.Creation(value);
        return EnqueueAsync(value.UserId, new IntakeOutboxMessage("draft-created", value, null, null), token);
    }

    public Task PublishAsync(TransactionConfirmedIntegrationEvent value, CancellationToken token)
    {
        IntakeRecordCodec.Confirmation(value);
        return EnqueueAsync(value.UserId, new IntakeOutboxMessage("confirmed", null, value, null), token);
    }

    private Task EnqueueAsync(string owner, IntakeOutboxMessage value, CancellationToken token) =>
        context.WithRecordsAsync(owner, async (records, _) =>
        {
            var existing = await records.GetAsync("intake_outbox", "intake", Key(value));
            if (existing is not null)
            {
                var message = Decode(existing);
                if (message.Creation != value.Creation || message.Confirmation != value.Confirmation)
                { throw new StorageConflictException(); }
                return true;
            }
            await records.InsertAsync("intake_outbox", "intake", Key(value), OwnedRecordCodec.Encode(1, value));
            return true;
        }, token);

    public Task<IReadOnlyList<IntakeOutboxMessage>> ReadPendingAsync(CancellationToken cancellationToken = default) =>
        context.WithRecordsAsync<IReadOnlyList<IntakeOutboxMessage>>("outbox-maintenance", async (records, _) =>
        {
            var scan = await records.GetAsync("outbox_scan", "intake", "pending");
            var after = scan is null ? null : OwnedRecordCodec.Decode<ScanCursor>(1, scan).After;
            var pending = new List<IntakeOutboxMessage>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var wrapped = false;
            for (var page = 0; page < 10 && pending.Count < 25; page++)
            {
                var items = await records.ReadPageAsync("intake_outbox", "intake", after);
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
                    if (message.PublishedAtUtc is null && seen.Add(Key(message))) { pending.Add(message); }
                    if (pending.Count == 25) { break; }
                }
            }
            var encoded = OwnedRecordCodec.Encode(1, new ScanCursor(after));
            if (scan is null) { await records.InsertAsync("outbox_scan", "intake", "pending", encoded); }
            else { await records.ReplaceAsync("outbox_scan", "intake", "pending", scan.Revision, encoded); }
            return pending;
        }, cancellationToken);

    public async Task MarkPublishedAsync(IntakeOutboxMessage expected, DateTimeOffset publishedAtUtc, CancellationToken token = default)
    {
        Validate(expected);
        if (publishedAtUtc == default || publishedAtUtc.Offset != TimeSpan.Zero || publishedAtUtc < expected.OccurredAtUtc)
        { throw new ArgumentException("Invalid Intake outbox acknowledgement time."); }
        await context.WithRecordsAsync("outbox-maintenance", async (records, _) =>
        {
            var existing = await records.GetAsync("intake_outbox", "intake", Key(expected)) ?? throw new StorageUnavailableException();
            var message = Decode(existing);
            if (message.Creation != expected.Creation || message.Confirmation != expected.Confirmation)
            { throw new StorageUnavailableException(); }
            if (message.PublishedAtUtc is null)
            {
                await records.ReplaceAsync("intake_outbox", "intake", Key(expected), existing.Revision,
                OwnedRecordCodec.Encode(1, message with { PublishedAtUtc = publishedAtUtc }));
            }
            return true;
        }, token);
    }

    public Task<int> CleanupPublishedBatchAsync(DateTimeOffset now, CancellationToken token = default) =>
        context.WithRecordsAsync("outbox-maintenance", async (records, _) =>
        {
            var scan = await records.GetAsync("outbox_scan", "intake", "retention");
            var after = scan is null ? null : OwnedRecordCodec.Decode<ScanCursor>(1, scan).After;
            var page = await records.ReadPageAsync("intake_outbox", "intake", after, 25);
            var count = 0;
            foreach (var item in page)
            {
                var message = Decode(new DurableRecord(item.Revision, item.Payload));
                if (message.PublishedAtUtc is not null && now >= message.PublishedAtUtc.Value.AddDays(30))
                { await records.DeletePageItemAsync("intake_outbox", "intake", item.Cursor, item.Revision); count++; }
            }
            var cursor = new ScanCursor(page.Count < 25 ? null : page[^1].Cursor);
            if (scan is null) { await records.InsertAsync("outbox_scan", "intake", "retention", OwnedRecordCodec.Encode(1, cursor)); }
            else { await records.ReplaceAsync("outbox_scan", "intake", "retention", scan.Revision, OwnedRecordCodec.Encode(1, cursor)); }
            return count;
        }, token);

    internal static async Task VerifyQueuedAsync(OwnedRecordTransaction records, IntakeOutboxMessage expected)
    {
        var durable = await records.GetAsync("intake_outbox", "intake", Key(expected)) ?? throw new StorageUnavailableException();
        var message = Decode(durable);
        if (message.Creation != expected.Creation || message.Confirmation != expected.Confirmation)
        { throw new StorageUnavailableException(); }
    }

    internal static string Key(IntakeOutboxMessage value) => value.Kind + ":" + value.EventId;
    private static IntakeOutboxMessage Decode(DurableRecord durable)
    {
        try
        {
            var message = OwnedRecordCodec.Decode<IntakeOutboxMessage>(1, durable);
            Validate(message);
            if (message.PublishedAtUtc is not null && (message.PublishedAtUtc.Value.Offset != TimeSpan.Zero || message.PublishedAtUtc < message.OccurredAtUtc))
            { throw new StorageUnavailableException(); }
            return message;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private static void Validate(IntakeOutboxMessage value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind == "draft-created" && value.Creation is not null && value.Confirmation is null) { IntakeRecordCodec.Creation(value.Creation); }
        else if (value.Kind == "confirmed" && value.Creation is null && value.Confirmation is not null) { IntakeRecordCodec.Confirmation(value.Confirmation); }
        else { throw new ArgumentException("Invalid Intake outbox kind or payload."); }
    }
    private sealed record ScanCursor([property: JsonRequired] string? After);
}

public sealed record IntakeOutboxMessage([property: JsonRequired] string Kind,
    [property: JsonRequired] TransactionDraftCreatedIntegrationEvent? Creation,
    [property: JsonRequired] TransactionConfirmedIntegrationEvent? Confirmation,
    [property: JsonRequired] DateTimeOffset? PublishedAtUtc)
{
    [JsonIgnore] public string EventId => Creation?.EventId ?? Confirmation?.EventId ?? "";
    [JsonIgnore] public DateTimeOffset OccurredAtUtc => Creation?.OccurredAtUtc ?? Confirmation?.ConfirmedAtUtc ?? default;
}
