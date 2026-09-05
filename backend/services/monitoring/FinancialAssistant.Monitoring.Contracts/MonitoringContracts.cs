namespace FinancialAssistant.Monitoring.Contracts;

public static class MonitoringApiRoutes
{
    public const string Dashboard = "/admin/monitoring";
    public const string ServiceDashboard = "/api/v1/admin/monitoring";
    public const string AiUsageSignals = "/internal/monitoring/signals/ai-usage";
    public const string ParsingQualitySignals = "/internal/monitoring/signals/parsing-quality";
    public const string UiFunnelSignals = "/internal/monitoring/signals/ui-funnel";
}

public static class MonitoringHeaders
{
    public const string GatewayAuthentication = "X-Gateway-Authentication";
    public const string GatewayRoles = "X-Gateway-Roles";
    public const string SignalAuthentication = "X-Monitoring-Authentication";
}

public static class MonitoringStatuses
{
    public const string Healthy = "healthy";
    public const string Degraded = "degraded";
    public const string Unavailable = "unavailable";
    public const string NotConfigured = "not_configured";
}

public sealed record MonitoringServiceHealthResponse(
    string Service,
    string Status,
    long LatencyMilliseconds,
    DateTimeOffset CheckedAtUtc,
    string? ErrorCategory);

public sealed record MonitoringRabbitMqResponse(
    string Status,
    long LatencyMilliseconds,
    long QueueDepth,
    int ConsumerCount,
    DateTimeOffset CheckedAtUtc,
    string? ErrorCategory);

public sealed record MonitoringElasticsearchResponse(
    string Status,
    long LatencyMilliseconds,
    string ClusterStatus,
    int NodeCount,
    int ActiveShardCount,
    DateTimeOffset CheckedAtUtc,
    string? ErrorCategory);

public sealed record MonitoringAiUsageResponse(
    long RequestCount,
    long SuccessfulRequestCount,
    long InputTokenCount,
    long OutputTokenCount,
    long EstimatedCostMicros);

public sealed record MonitoringParsingQualityResponse(
    long ProcessedCount,
    long SuccessfulCount,
    long ReviewRequiredCount,
    long FailedCount,
    decimal SuccessPercent);

public sealed record MonitoringUiFunnelResponse(
    string Stage,
    long EnteredCount,
    long CompletedCount,
    decimal CompletionPercent);

public sealed record MonitoringOperationalMetricsResponse(
    MonitoringAiUsageResponse AiUsage,
    MonitoringParsingQualityResponse ParsingQuality,
    IReadOnlyList<MonitoringUiFunnelResponse> UiFunnel);

public sealed record MonitoringReadinessSummaryResponse(
    int ComponentCount,
    int HealthyCount,
    int DegradedCount,
    int UnavailableCount,
    int NotConfiguredCount);

public sealed record MonitoringDashboardResponse(
    DateTimeOffset GeneratedAtUtc,
    string OverallStatus,
    MonitoringReadinessSummaryResponse Readiness,
    IReadOnlyList<MonitoringServiceHealthResponse> Services,
    MonitoringRabbitMqResponse RabbitMq,
    MonitoringElasticsearchResponse Elasticsearch,
    MonitoringOperationalMetricsResponse Metrics,
    string DataClassification);

public sealed record MonitoringAiUsageSignalRequest(
    string SourceService,
    long RequestCount,
    long SuccessfulRequestCount,
    long InputTokenCount,
    long OutputTokenCount,
    long EstimatedCostMicros);

public sealed record MonitoringParsingQualitySignalRequest(
    string SourceService,
    long ProcessedCount,
    long SuccessfulCount,
    long ReviewRequiredCount,
    long FailedCount);

public sealed record MonitoringUiFunnelSignalRequest(
    string SourceService,
    string Stage,
    long EnteredCount,
    long CompletedCount);

public sealed record MonitoringSignalAcceptedResponse(string Status);

public sealed record MonitoringApiErrorResponse(
    string? Title,
    string? Detail,
    int? Status,
    string? Code,
    string? TraceId);

public sealed record MonitoringServiceInfoResponse(
    string Service,
    string Status,
    string Environment,
    string DataClassification);
