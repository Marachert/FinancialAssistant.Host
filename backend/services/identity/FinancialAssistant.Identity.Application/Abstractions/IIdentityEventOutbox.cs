using FinancialAssistant.Identity.Application.Messaging;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IIdentityEventOutbox
{
    Task EnqueueAsync(
        IdentityIntegrationEventEnvelope envelope,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IdentityOutboxMessage>> ReadPendingAsync(
        int batchSize,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task MarkPublishedAsync(
        string eventId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        string eventId,
        string errorCode,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default);
}
