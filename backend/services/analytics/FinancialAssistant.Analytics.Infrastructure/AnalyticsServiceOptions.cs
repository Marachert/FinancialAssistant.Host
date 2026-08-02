namespace FinancialAssistant.Analytics.Infrastructure;

public sealed class AnalyticsServiceOptions
{
    public const string SectionName = "Analytics";

    public AnalyticsEventConsumerOptions Events { get; set; } = new();
}

public sealed class AnalyticsEventConsumerOptions
{
    public string Mode { get; set; } = "InMemoryDevelopment";

    public string ConnectionString { get; set; } = string.Empty;

    public string Exchange { get; set; } = "fa.events";

    public string DeadLetterExchange { get; set; } = "fa.dead-letter";

    public string Queue { get; set; } = "fa.analytics.financial-events.v1";
}
