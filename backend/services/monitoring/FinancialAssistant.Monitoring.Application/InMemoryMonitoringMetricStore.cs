using FinancialAssistant.Monitoring.Contracts;

namespace FinancialAssistant.Monitoring.Application;

public sealed class InMemoryMonitoringMetricStore(MonitoringSignalPolicy policy) : IMonitoringMetricStore
{
    private const long MaximumSignalValue = 1_000_000_000_000;
    private readonly object sync = new();
    private readonly Dictionary<string, (long Entered, long Completed)> uiFunnel =
        new(StringComparer.Ordinal);
    private long aiRequests;
    private long aiSuccessfulRequests;
    private long inputTokens;
    private long outputTokens;
    private long estimatedCostMicros;
    private long processed;
    private long parsingSuccessful;
    private long reviewRequired;
    private long parsingFailed;

    public void Record(MonitoringAiUsageSignalRequest signal)
    {
        EnsureAllowedSource(signal.SourceService);
        EnsureValues(
            signal.RequestCount,
            signal.SuccessfulRequestCount,
            signal.InputTokenCount,
            signal.OutputTokenCount,
            signal.EstimatedCostMicros);
        if (signal.SuccessfulRequestCount > signal.RequestCount)
        {
            throw new ArgumentException("Successful request count cannot exceed request count.");
        }

        lock (sync)
        {
            aiRequests = Add(aiRequests, signal.RequestCount);
            aiSuccessfulRequests = Add(aiSuccessfulRequests, signal.SuccessfulRequestCount);
            inputTokens = Add(inputTokens, signal.InputTokenCount);
            outputTokens = Add(outputTokens, signal.OutputTokenCount);
            estimatedCostMicros = Add(estimatedCostMicros, signal.EstimatedCostMicros);
        }
    }

    public void Record(MonitoringParsingQualitySignalRequest signal)
    {
        EnsureAllowedSource(signal.SourceService);
        EnsureValues(
            signal.ProcessedCount,
            signal.SuccessfulCount,
            signal.ReviewRequiredCount,
            signal.FailedCount);
        if (signal.SuccessfulCount + signal.ReviewRequiredCount + signal.FailedCount >
            signal.ProcessedCount)
        {
            throw new ArgumentException("Parsing outcomes cannot exceed processed count.");
        }

        lock (sync)
        {
            processed = Add(processed, signal.ProcessedCount);
            parsingSuccessful = Add(parsingSuccessful, signal.SuccessfulCount);
            reviewRequired = Add(reviewRequired, signal.ReviewRequiredCount);
            parsingFailed = Add(parsingFailed, signal.FailedCount);
        }
    }

    public void Record(MonitoringUiFunnelSignalRequest signal)
    {
        EnsureAllowedSource(signal.SourceService);
        var stage = signal.Stage.Trim().ToLowerInvariant();
        if (!policy.AllowsUiStage(stage))
        {
            throw new ArgumentException("UI funnel stage is not allowlisted.");
        }

        EnsureValues(signal.EnteredCount, signal.CompletedCount);
        if (signal.CompletedCount > signal.EnteredCount)
        {
            throw new ArgumentException("Completed count cannot exceed entered count.");
        }

        lock (sync)
        {
            uiFunnel.TryGetValue(stage, out var current);
            uiFunnel[stage] = (
                Add(current.Entered, signal.EnteredCount),
                Add(current.Completed, signal.CompletedCount));
        }
    }

    public MonitoringMetricSnapshot GetSnapshot()
    {
        lock (sync)
        {
            return new MonitoringMetricSnapshot(
                new MonitoringAiUsageResponse(
                    aiRequests,
                    aiSuccessfulRequests,
                    inputTokens,
                    outputTokens,
                    estimatedCostMicros),
                new MonitoringParsingQualityResponse(
                    processed,
                    parsingSuccessful,
                    reviewRequired,
                    parsingFailed,
                    Percentage(parsingSuccessful, processed)),
                uiFunnel
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new MonitoringUiFunnelResponse(
                        item.Key,
                        item.Value.Entered,
                        item.Value.Completed,
                        Percentage(item.Value.Completed, item.Value.Entered)))
                    .ToArray());
        }
    }

    private void EnsureAllowedSource(string sourceService)
    {
        if (!policy.AllowsSource(sourceService))
        {
            throw new ArgumentException("Signal source service is not allowlisted.");
        }
    }

    private static void EnsureValues(params long[] values)
    {
        if (values.Any(value => value < 0 || value > MaximumSignalValue))
        {
            throw new ArgumentException("Signal values are outside the allowed range.");
        }
    }

    private static long Add(long current, long increment) =>
        checked(current + increment);

    private static decimal Percentage(long numerator, long denominator) =>
        denominator == 0
            ? 0m
            : decimal.Round(numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);
}
