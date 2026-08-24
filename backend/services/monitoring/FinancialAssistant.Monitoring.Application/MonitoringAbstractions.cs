using FinancialAssistant.Monitoring.Contracts;

namespace FinancialAssistant.Monitoring.Application;

public sealed record MonitoringServiceProbe(
    string Service,
    string Status,
    long LatencyMilliseconds,
    DateTimeOffset CheckedAtUtc,
    string? ErrorCategory);

public sealed record MonitoringRabbitMqProbe(
    string Status,
    long LatencyMilliseconds,
    long QueueDepth,
    int ConsumerCount,
    DateTimeOffset CheckedAtUtc,
    string? ErrorCategory);

public sealed record MonitoringElasticsearchProbe(
    string Status,
    long LatencyMilliseconds,
    string ClusterStatus,
    int NodeCount,
    int ActiveShardCount,
    DateTimeOffset CheckedAtUtc,
    string? ErrorCategory);

public sealed record MonitoringProbeSnapshot(
    IReadOnlyList<MonitoringServiceProbe> Services,
    MonitoringRabbitMqProbe RabbitMq,
    MonitoringElasticsearchProbe Elasticsearch);

public sealed record MonitoringMetricSnapshot(
    MonitoringAiUsageResponse AiUsage,
    MonitoringParsingQualityResponse ParsingQuality,
    IReadOnlyList<MonitoringUiFunnelResponse> UiFunnel);

public interface IMonitoringDependencyProbe
{
    Task<MonitoringProbeSnapshot> ProbeAsync(CancellationToken cancellationToken);
}

public interface IMonitoringMetricStore
{
    void Record(MonitoringAiUsageSignalRequest signal);

    void Record(MonitoringParsingQualitySignalRequest signal);

    void Record(MonitoringUiFunnelSignalRequest signal);

    MonitoringMetricSnapshot GetSnapshot();
}

public sealed class MonitoringSignalPolicy
{
    private readonly HashSet<string> sourceServices;
    private readonly HashSet<string> uiStages;

    public MonitoringSignalPolicy(IEnumerable<string> sourceServices, IEnumerable<string> uiStages)
    {
        this.sourceServices = Normalize(sourceServices);
        this.uiStages = Normalize(uiStages);
        if (this.sourceServices.Count == 0 || this.uiStages.Count == 0)
        {
            throw new InvalidOperationException(
                "Monitoring signal source services and UI stages must be allowlisted.");
        }
    }

    public bool AllowsSource(string value) => Allows(value, sourceServices);

    public bool AllowsUiStage(string value) => Allows(value, uiStages);

    private static bool Allows(string? value, HashSet<string> allowlist) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 64
        && allowlist.Contains(Normalize(value));

    private static HashSet<string> Normalize(IEnumerable<string> values) =>
        values
            .Select(Normalize)
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

    private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}
