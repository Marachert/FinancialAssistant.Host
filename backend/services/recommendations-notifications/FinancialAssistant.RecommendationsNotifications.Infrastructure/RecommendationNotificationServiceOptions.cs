namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed class RecommendationNotificationServiceOptions
{
    public const string SectionName = "RecommendationsNotifications";

    public RecommendationNotificationEventOptions Events { get; set; } = new();
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
