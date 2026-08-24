namespace FinancialAssistant.Monitoring.Infrastructure;

public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    public int ProbeTimeoutSeconds { get; init; } = 3;

    public MonitoringServiceTargetOptions[] Services { get; init; } = [];

    public MonitoringRabbitMqOptions RabbitMq { get; init; } = new();

    public MonitoringElasticsearchOptions Elasticsearch { get; init; } = new();

    public MonitoringSignalPolicyOptions SignalPolicy { get; init; } = new();
}

public sealed class MonitoringServiceTargetOptions
{
    public string Name { get; init; } = string.Empty;

    public string BaseAddress { get; init; } = string.Empty;
}

public sealed class MonitoringRabbitMqOptions
{
    public string ManagementBaseAddress { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed class MonitoringElasticsearchOptions
{
    public string BaseAddress { get; init; } = string.Empty;
}

public sealed class MonitoringSignalPolicyOptions
{
    public string[] AllowedSourceServices { get; init; } = [];

    public string[] AllowedUiStages { get; init; } = [];
}
