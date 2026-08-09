namespace FinancialAssistant.RecommendationsNotifications.Domain;

public static class NotificationDeliveryFailureCodes
{
    public const string ChannelDisabled = "channel-disabled";
    public const string ProviderNotConfigured = "provider-not-configured";
    public const string ProviderAdapterPlaceholder = "provider-adapter-placeholder";
    public const string ProviderUnavailable = "provider-unavailable";
}

public sealed record NotificationDeliveryAdapterResult(
    string Status,
    bool IsTransientFailure,
    string? FailureCode)
{
    public static NotificationDeliveryAdapterResult Delivered() =>
        new(NotificationDeliveryStatuses.Delivered, false, null);

    public static NotificationDeliveryAdapterResult Failed(
        string failureCode,
        bool isTransientFailure) =>
        new(NotificationDeliveryStatuses.Failed, isTransientFailure, failureCode);

    public static NotificationDeliveryAdapterResult Suppressed(string failureCode) =>
        new(NotificationDeliveryStatuses.Suppressed, false, failureCode);
}

public sealed record NotificationDeliveryAttempt(
    string NotificationId,
    string Channel,
    int AttemptNumber,
    string Status,
    bool IsRetryable,
    string? FailureCode,
    DateTimeOffset AttemptedAtUtc,
    DateTimeOffset? RetryAtUtc);

public sealed record NotificationRetryDecision(
    bool ShouldRetry,
    int NextAttemptNumber,
    DateTimeOffset? RetryAtUtc)
{
    public static NotificationRetryDecision NoRetry(int currentAttemptNumber) =>
        new(false, currentAttemptNumber, null);
}
