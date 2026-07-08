namespace FinancialAssistant.PublicApiGateway.Observability;

public sealed class CorrelationOptions
{
    public string PrimaryHeaderName { get; init; } = CorrelationHeaders.CorrelationId;

    public string CompatibilityHeaderName { get; init; } = CorrelationHeaders.XCorrelationId;

    public string TraceIdHeaderName { get; init; } = "X-Trace-Id";

    public string RequestDurationHeaderName { get; init; } = "Server-Timing";

    public int MaxCorrelationIdLength { get; init; } = 128;
}
