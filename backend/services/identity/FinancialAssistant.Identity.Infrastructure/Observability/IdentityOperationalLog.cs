using Microsoft.Extensions.Logging;

namespace FinancialAssistant.Identity.Infrastructure.Observability;

public static partial class IdentityOperationalLog
{
    [LoggerMessage(
        EventId = 2000,
        EventName = "IdentityOutboxPublishFailed",
        Level = LogLevel.Warning,
        Message = "Identity outbox publish failed. EventId: {EventId}; EventType: {EventType}; CorrelationId: {CorrelationId}; CausationId: {CausationId}; FailureType: {FailureType}; AttemptCount: {AttemptCount}; RetryDelaySeconds: {RetryDelaySeconds}.")]
    public static partial void OutboxPublishFailed(
        ILogger logger,
        string eventId,
        string eventType,
        string correlationId,
        string causationId,
        string failureType,
        int attemptCount,
        double retryDelaySeconds);
}
