namespace FinancialAssistant.Mcp.Infrastructure;

public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    public MonitoringOptions Monitoring { get; init; } = new();

    public AuditOptions Audit { get; init; } = new();

    public PromptEvaluationOptions PromptEvaluations { get; init; } = new();

    public sealed class MonitoringOptions
    {
        public string BaseAddress { get; init; } = string.Empty;

        public string SharedSecret { get; init; } = string.Empty;

        public int TimeoutSeconds { get; init; } = 3;
    }

    public sealed class AuditOptions
    {
        public string Mode { get; init; } = "InMemory";

        public string BaseAddress { get; init; } = string.Empty;

        public string SharedSecret { get; init; } = string.Empty;

        public int TimeoutSeconds { get; init; } = 3;
    }

    public sealed class PromptEvaluationOptions
    {
        public string Status { get; init; } = "not_configured";

        public long EvaluatedCount { get; init; }

        public long PassedCount { get; init; }

        public long FailedCount { get; init; }

        public string EvaluationSetVersion { get; init; } = "none";
    }
}
