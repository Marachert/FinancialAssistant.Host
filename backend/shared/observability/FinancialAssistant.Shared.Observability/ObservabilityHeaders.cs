namespace FinancialAssistant.Shared.Observability;

public static class ObservabilityHeaders
{
    public const string CorrelationId = "correlationId";
    public const string CompatibilityCorrelationId = "X-Correlation-Id";
    public const string TraceId = "X-Trace-Id";
    public const string ContextItemKey = "FinancialAssistant.CorrelationId";
    public const int MaximumCorrelationIdLength = 128;

    public static bool IsSafeCorrelationId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaximumCorrelationIdLength
        && value.All(character => !char.IsControl(character) && !char.IsWhiteSpace(character));
}
