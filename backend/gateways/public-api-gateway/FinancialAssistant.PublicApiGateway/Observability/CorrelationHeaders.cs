namespace FinancialAssistant.PublicApiGateway.Observability;

public static class CorrelationHeaders
{
    public const string CorrelationId = "correlationId";
    public const string XCorrelationId = "X-Correlation-Id";
    public const string TraceParent = "traceparent";
    public const string ContextItemKey = "FinancialAssistant.CorrelationId";

    public static string? GetCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(ContextItemKey, out var value) && value is string correlationId && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return null;
    }
}
