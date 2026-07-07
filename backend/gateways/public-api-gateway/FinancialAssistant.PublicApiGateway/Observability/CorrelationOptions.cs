namespace FinancialAssistant.PublicApiGateway.Observability;

public sealed class CorrelationOptions
{
    public string PrimaryHeaderName { get; init; } = CorrelationHeaders.CorrelationId;

    public string CompatibilityHeaderName { get; init; } = CorrelationHeaders.XCorrelationId;

    public int MaxCorrelationIdLength { get; init; } = 128;
}
