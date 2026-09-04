using FinancialAssistant.Monitoring.Contracts;

namespace FinancialAssistant.Monitoring.Application;

public sealed class MonitoringSnapshotService(
    IMonitoringDependencyProbe dependencyProbe,
    IMonitoringMetricStore metricStore)
{
    public async Task<MonitoringDashboardResponse> GetAsync(CancellationToken cancellationToken)
    {
        var probes = await dependencyProbe.ProbeAsync(cancellationToken);
        var metrics = metricStore.GetSnapshot();
        var services = probes.Services
            .OrderBy(service => service.Service, StringComparer.Ordinal)
            .Select(service => new MonitoringServiceHealthResponse(
                service.Service,
                service.Status,
                service.LatencyMilliseconds,
                service.CheckedAtUtc,
                service.ErrorCategory))
            .ToArray();
        var statuses = services.Select(service => service.Status)
            .Append(probes.RabbitMq.Status)
            .Append(probes.Elasticsearch.Status)
            .ToArray();
        var readiness = MonitoringStatusPolicy.Summarize(statuses);
        var overallStatus = MonitoringStatusPolicy.GetOverallStatus(readiness);

        return new MonitoringDashboardResponse(
            DateTimeOffset.UtcNow,
            overallStatus,
            readiness,
            services,
            new MonitoringRabbitMqResponse(
                probes.RabbitMq.Status,
                probes.RabbitMq.LatencyMilliseconds,
                probes.RabbitMq.QueueDepth,
                probes.RabbitMq.ConsumerCount,
                probes.RabbitMq.CheckedAtUtc,
                probes.RabbitMq.ErrorCategory),
            new MonitoringElasticsearchResponse(
                probes.Elasticsearch.Status,
                probes.Elasticsearch.LatencyMilliseconds,
                probes.Elasticsearch.ClusterStatus,
                probes.Elasticsearch.NodeCount,
                probes.Elasticsearch.ActiveShardCount,
                probes.Elasticsearch.CheckedAtUtc,
                probes.Elasticsearch.ErrorCategory),
            new MonitoringOperationalMetricsResponse(
                metrics.AiUsage,
                metrics.ParsingQuality,
                metrics.UiFunnel),
            "aggregate-operational-only");
    }
}
