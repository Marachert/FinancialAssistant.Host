using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Messaging;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Identity.Infrastructure.Persistence;

public sealed class PostgresIdentityEventOutbox(IdentityPersistenceContext context) : IIdentityEventOutbox
{
    private const string Owner = "identity";

    public async Task EnqueueAsync(IdentityIntegrationEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        Validate(envelope);
        await context.WithRecordsAsync(async (records, _) =>
        {
            if (!await records.InsertAsync("outbox", Owner, envelope.EventId,
                IdentityPersistenceContext.Encode(new IdentityOutboxMessage(envelope, 0, envelope.OccurredAt))))
            { throw new StorageConflictException(); }
            return true;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<IdentityOutboxMessage>> ReadPendingAsync(int batchSize, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 100) { throw new ArgumentOutOfRangeException(nameof(batchSize)); }
        return context.WithRecordsAsync<IReadOnlyList<IdentityOutboxMessage>>(async (records, _) =>
        {
            var cursor = await IdentityPersistenceContext.ReadAsync<ScanCursor>(records, "outbox_scan", Owner, "pending");
            var after = cursor?.After;
            var pending = new List<IdentityOutboxMessage>();
            // Persist a bounded round-robin cursor so old published rows cannot starve new messages.
            for (var page = 0; page < 10 && pending.Count < batchSize; page++)
            {
                var items = await records.ReadPageAsync("outbox", Owner, after);
                if (items.Count == 0) { after = null; break; }
                foreach (var item in items)
                {
                    var message = IdentityPersistenceContext.Decode<IdentityOutboxMessage>(new DurableRecord(item.Revision, item.Payload));
                    Validate(message.Envelope);
                    if (message.AttemptCount < 0) { throw new StorageUnavailableException(); }
                    after = item.Cursor;
                    if (message.PublishedAtUtc is null && message.NextAttemptAtUtc <= nowUtc) { pending.Add(message); }
                    if (pending.Count == batchSize) { break; }
                }
            }
            await IdentityPersistenceContext.WriteAsync(records, "outbox_scan", Owner, "pending", new ScanCursor(after));
            return pending.OrderBy(message => message.Envelope.OccurredAt).ToArray();
        }, cancellationToken);
    }

    public Task MarkPublishedAsync(string eventId, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken = default) =>
        ChangeAsync(eventId, message => message.PublishedAtUtc is not null ? message : message with
        {
            Envelope = message.Envelope with { PublishedAt = publishedAtUtc },
            PublishedAtUtc = publishedAtUtc,
            LastErrorCode = null
        }, cancellationToken);

    public Task MarkFailedAsync(string eventId, string errorCode, DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        // Transport errors are reduced to codes; never persist raw exception/provider content.
        if (string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > 64
            || errorCode.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        { throw new ArgumentException("Invalid outbox error code.", nameof(errorCode)); }
        return ChangeAsync(eventId, message => message.PublishedAtUtc is not null ? message : message with
        {
            AttemptCount = checked(message.AttemptCount + 1),
            NextAttemptAtUtc = nextAttemptAtUtc,
            LastErrorCode = errorCode
        }, cancellationToken);
    }

    private async Task ChangeAsync(string eventId, Func<IdentityOutboxMessage, IdentityOutboxMessage> change, CancellationToken token)
    {
        if (!Guid.TryParseExact(eventId, "D", out _)) { throw new ArgumentException("Invalid event ID.", nameof(eventId)); }
        await context.WithRecordsAsync(async (records, _) =>
        {
            var message = await IdentityPersistenceContext.ReadAsync<IdentityOutboxMessage>(records, "outbox", Owner, eventId);
            if (message is not null)
            {
                Validate(message.Envelope);
                if (message.Envelope.EventId != eventId) { throw new StorageUnavailableException(); }
                await IdentityPersistenceContext.WriteAsync(records, "outbox", Owner, eventId, change(message));
            }
            return true;
        }, token);
    }

    private static void Validate(IdentityIntegrationEventEnvelope envelope)
    {
        if (envelope is null || !Guid.TryParseExact(envelope.EventId, "D", out _) || envelope.SchemaVersion < 1
            || string.IsNullOrWhiteSpace(envelope.EventType) || envelope.EventType.Length > 128
            || !envelope.EventType.EndsWith($".v{envelope.SchemaVersion}", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(envelope.Producer) || envelope.Producer.Length > 128
            || string.IsNullOrWhiteSpace(envelope.CorrelationId) || envelope.CorrelationId.Length > 256
            || string.IsNullOrWhiteSpace(envelope.CausationId) || envelope.CausationId.Length > 256
            || envelope.Payload is null || envelope.Payload.Count > 32
            || envelope.Payload.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 64 || pair.Value is null || pair.Value.Length > 512))
        { throw new StorageUnavailableException(); }
    }

    private sealed record ScanCursor(string? After);
}
