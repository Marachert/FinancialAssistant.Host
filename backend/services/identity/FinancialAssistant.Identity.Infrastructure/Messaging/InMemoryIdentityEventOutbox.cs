using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Messaging;

namespace FinancialAssistant.Identity.Infrastructure.Messaging;

public sealed class InMemoryIdentityEventOutbox : IIdentityEventOutbox
{
    private readonly object gate = new();
    private readonly Dictionary<string, IdentityOutboxMessage> messages = new(StringComparer.Ordinal);

    public Task EnqueueAsync(
        IdentityIntegrationEventEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!messages.TryAdd(
                    envelope.EventId,
                    new IdentityOutboxMessage(envelope, 0, envelope.OccurredAt)))
            {
                throw new InvalidOperationException("The identity event already exists in the outbox.");
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IdentityOutboxMessage>> ReadPendingAsync(
        int batchSize,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<IdentityOutboxMessage> result = messages.Values
                .Where(message => message.PublishedAtUtc is null && message.NextAttemptAtUtc <= nowUtc)
                .OrderBy(message => message.Envelope.OccurredAt)
                .Take(Math.Max(1, batchSize))
                .ToArray();
            return Task.FromResult(result);
        }
    }

    public Task MarkPublishedAsync(
        string eventId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (messages.TryGetValue(eventId, out var current))
            {
                messages[eventId] = current with
                {
                    Envelope = current.Envelope with { PublishedAt = publishedAtUtc },
                    PublishedAtUtc = publishedAtUtc,
                    LastErrorCode = null
                };
            }
        }

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(
        string eventId,
        string errorCode,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (messages.TryGetValue(eventId, out var current))
            {
                messages[eventId] = current with
                {
                    AttemptCount = current.AttemptCount + 1,
                    NextAttemptAtUtc = nextAttemptAtUtc,
                    LastErrorCode = errorCode
                };
            }
        }

        return Task.CompletedTask;
    }
}
