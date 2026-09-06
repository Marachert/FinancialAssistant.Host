using FinancialAssistant.Monitoring.Contracts;

namespace FinancialAssistant.Monitoring.Application;

public sealed class MonitoringJobStore(MonitoringSignalPolicy policy, TimeProvider timeProvider)
{
    public const int Capacity = 200;
    public const int RetentionHours = 24;
    private readonly object sync = new();
    private readonly Dictionary<(string Service, Guid Id), MonitoringJobResponse> jobs = [];

    public void Record(MonitoringJobSignalRequest signal)
    {
        if (signal.OperationId == Guid.Empty || !policy.AllowsSource(signal.SourceService)
            || signal.Revision is < 1 or > 1_000_000
            || signal.Kind is not ("ai" or "ocr" or "notification")
            || signal.State is not ("queued" or "running" or "succeeded" or "failed")
            || signal.ErrorCategory is not (null or "timeout" or "transport" or "provider_unavailable"
                or "invalid_result" or "policy_rejected")
            || (signal.State == "failed") != (signal.ErrorCategory is not null))
        {
            throw new ArgumentException("Job signal is outside the operational contract.");
        }

        var service = signal.SourceService.Trim().ToLowerInvariant();
        var expectedSource = signal.Kind switch
        {
            "ai" => "ai-orchestration",
            "ocr" => "receipt-processing",
            _ => "recommendations-notifications"
        };
        if (service != expectedSource)
        {
            throw new ArgumentException("Job kind does not match its owning service.");
        }

        lock (sync)
        {
            var now = timeProvider.GetUtcNow();
            Prune(now);
            var key = (service, signal.OperationId);
            if (jobs.TryGetValue(key, out var existing))
            {
                if (signal.Revision < existing.Revision)
                {
                    return;
                }

                if (signal.Revision == existing.Revision)
                {
                    if (existing.Kind != signal.Kind || existing.State != signal.State
                        || existing.ErrorCategory != signal.ErrorCategory)
                    {
                        throw new InvalidOperationException("Job revision conflicts with existing evidence.");
                    }

                    return;
                }
            }

            jobs[key] = new MonitoringJobResponse(signal.OperationId, service, signal.Kind,
                signal.State, signal.Revision, now, signal.ErrorCategory);
            if (jobs.Count > Capacity)
            {
                var oldest = jobs.MinBy(item => item.Value.ObservedAtUtc).Key;
                jobs.Remove(oldest);
            }
        }
    }

    public MonitoringJobsResponse GetSnapshot()
    {
        lock (sync)
        {
            var now = timeProvider.GetUtcNow();
            Prune(now);
            return new MonitoringJobsResponse(now,
                jobs.Values.OrderByDescending(job => job.ObservedAtUtc)
                    .ThenBy(job => job.OperationId).ToArray(),
                Capacity, RetentionHours, "bounded-operational-metadata-only");
        }
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var key in jobs.Where(item => item.Value.ObservedAtUtc <= now.AddHours(-RetentionHours))
                     .Select(item => item.Key).ToArray())
        {
            jobs.Remove(key);
        }
    }
}
