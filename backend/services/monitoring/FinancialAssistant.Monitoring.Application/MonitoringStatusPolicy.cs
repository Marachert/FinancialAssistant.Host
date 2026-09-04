using FinancialAssistant.Monitoring.Contracts;

namespace FinancialAssistant.Monitoring.Application;

public static class MonitoringStatusPolicy
{
    public static MonitoringReadinessSummaryResponse Summarize(IEnumerable<string> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        var normalized = statuses.Select(Normalize).ToArray();
        return new MonitoringReadinessSummaryResponse(
            normalized.Length,
            normalized.Count(status => status == MonitoringStatuses.Healthy),
            normalized.Count(status => status == MonitoringStatuses.Degraded),
            normalized.Count(status => status == MonitoringStatuses.Unavailable),
            normalized.Count(status => status == MonitoringStatuses.NotConfigured));
    }

    public static string GetOverallStatus(MonitoringReadinessSummaryResponse summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (summary.ComponentCount == 0 || summary.UnavailableCount > 0)
        {
            return MonitoringStatuses.Unavailable;
        }

        return summary.DegradedCount > 0 || summary.NotConfiguredCount > 0
            ? MonitoringStatuses.Degraded
            : MonitoringStatuses.Healthy;
    }

    private static string Normalize(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        MonitoringStatuses.Healthy => MonitoringStatuses.Healthy,
        MonitoringStatuses.Degraded => MonitoringStatuses.Degraded,
        MonitoringStatuses.NotConfigured => MonitoringStatuses.NotConfigured,
        _ => MonitoringStatuses.Unavailable
    };
}
