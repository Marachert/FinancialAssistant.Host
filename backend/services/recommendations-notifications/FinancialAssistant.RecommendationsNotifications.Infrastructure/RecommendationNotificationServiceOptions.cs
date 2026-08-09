namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed class RecommendationNotificationServiceOptions
{
    public const string SectionName = "RecommendationsNotifications";

    public RecommendationNotificationEventOptions Events { get; set; } = new();

    public NotificationDeliveryOptions Delivery { get; set; } = new();
}

public sealed class RecommendationNotificationEventOptions
{
    public string Mode { get; set; } = "InMemoryDevelopment";

    public string ConnectionString { get; set; } = string.Empty;

    public string Exchange { get; set; } = "fa.events";

    public string RetryExchange { get; set; } = "fa.retry";

    public string DeadLetterExchange { get; set; } = "fa.dead-letter";

    public string Queue { get; set; } = "fa.recommendations-notifications.insight-events.v1";
}

public sealed class NotificationDeliveryOptions
{
    public NotificationDeliveryProviderOptions Push { get; set; } = new();

    public NotificationDeliveryProviderOptions Web { get; set; } = new();

    public NotificationDeliveryRetryOptions Retry { get; set; } = new();
}

public sealed class NotificationDeliveryProviderOptions
{
    public bool Enabled { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string Credential { get; set; } = string.Empty;
}

public sealed class NotificationDeliveryRetryOptions
{
    public int MaxAttempts { get; set; } = 3;

    public int DelaySeconds { get; set; } = 30;
}
