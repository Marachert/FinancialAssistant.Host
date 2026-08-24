namespace FinancialAssistant.Audit.Infrastructure;

public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    public string[] AllowedProducers { get; init; } = [];

    public Dictionary<string, int> RetentionDays { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public AuditEventConsumerOptions Events { get; init; } = new();
}

public sealed class AuditEventConsumerOptions
{
    public string Mode { get; init; } = "InMemory";

    public string ConnectionString { get; init; } = string.Empty;

    public string Exchange { get; init; } = "fa.events";

    public string DeadLetterExchange { get; init; } = "fa.dead-letter";

    public string Queue { get; init; } = "fa.audit.events.v1";

    public string RoutingKey { get; init; } = "audit.recorded.v1";
}
