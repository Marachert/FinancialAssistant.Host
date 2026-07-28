namespace FinancialAssistant.AiOrchestration.Contracts;

public static class AiParsingFailureCategories
{
    public const string ProviderTimeout = "provider_timeout";
    public const string ProviderUnavailable = "provider_unavailable";
    public const string RateLimited = "rate_limited";
    public const string TransportFailure = "transport_failure";
    public const string InvalidRequest = "invalid_request";
    public const string InvalidProviderResponse = "invalid_provider_response";
    public const string UnsafeProviderOutput = "unsafe_provider_output";
    public const string ProviderDisabled = "provider_disabled";
    public const string ProviderFailure = "provider_failure";

    public static bool IsRetryable(string failureCategory) =>
        failureCategory is
            ProviderTimeout or
            ProviderUnavailable or
            RateLimited or
            TransportFailure;
}

public static class AiProcessingUserMessageCodes
{
    public const string ProcessingDelayed = "processing_temporarily_delayed";
    public const string ProcessingFailed = "processing_failed";
    public const string ProviderDisabled = "processing_provider_disabled";
}

public static class AiParsingJobRetryPolicy
{
    public const int MaximumAttempts = 3;

    public static bool ShouldRetry(
        string failureCategory,
        bool isTransient,
        int failedAttempt) =>
        isTransient &&
        failedAttempt is >= 1 and < MaximumAttempts &&
        AiParsingFailureCategories.IsRetryable(failureCategory);

    public static TimeSpan GetBackoffBeforeAttempt(int nextAttempt) =>
        nextAttempt switch
        {
            2 => TimeSpan.FromSeconds(30),
            3 => TimeSpan.FromMinutes(2),
            _ => throw new ArgumentOutOfRangeException(
                nameof(nextAttempt),
                "Only the second and third attempts can be scheduled.")
        };
}

public sealed record AiParsingRetryScheduledIntegrationEvent(
    string EventId,
    string JobId,
    string DraftId,
    string UserId,
    string NextCommandId,
    string FailureCategory,
    int FailedAttempt,
    int NextAttempt,
    DateTimeOffset RetryAtUtc,
    string UserMessageCode,
    string ProviderName,
    string ModelKey,
    string TraceId,
    DateTimeOffset OccurredAtUtc)
{
    public const string Name = "ai.parsing-retry-scheduled.v1";

    public string EventType => Name;
}

public sealed record AiParsingPermanentlyFailedIntegrationEvent(
    string EventId,
    string JobId,
    string DraftId,
    string UserId,
    string FailureCategory,
    int AttemptCount,
    string UserMessageCode,
    string ProviderName,
    string ModelKey,
    string TraceId,
    DateTimeOffset OccurredAtUtc)
{
    public const string Name = "ai.parsing-permanently-failed.v1";

    public string EventType => Name;
}

public interface IAiParsingRetryScheduledConsumer
{
    Task ConsumeAsync(
        AiParsingRetryScheduledIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}

public interface IAiParsingPermanentlyFailedConsumer
{
    Task ConsumeAsync(
        AiParsingPermanentlyFailedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}
