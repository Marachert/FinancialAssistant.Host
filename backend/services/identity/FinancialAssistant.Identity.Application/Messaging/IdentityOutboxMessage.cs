namespace FinancialAssistant.Identity.Application.Messaging;

public sealed record IdentityOutboxMessage(
    IdentityIntegrationEventEnvelope Envelope,
    int AttemptCount,
    DateTimeOffset NextAttemptAtUtc,
    DateTimeOffset? PublishedAtUtc = null,
    string? LastErrorCode = null);
