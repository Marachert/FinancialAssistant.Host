namespace FinancialAssistant.FinancialScore.Infrastructure;

public sealed class FinancialScoreServiceOptions
{
    public const string SectionName = "FinancialScore";

    public FinancialScoreEventOptions Events { get; set; } = new();
}

public sealed class FinancialScoreEventOptions
{
    public string Mode { get; set; } = "InMemoryDevelopment";

    public string ConnectionString { get; set; } = string.Empty;

    public string Exchange { get; set; } = "fa.events";

    public string DeadLetterExchange { get; set; } = "fa.dead-letter";

    public string Queue { get; set; } = "fa.financial-score.financial-events.v1";
}
