using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.Shared.Observability;

public sealed class FinancialAssistantCorrelationMiddleware(
    RequestDelegate next,
    ILogger<FinancialAssistantCorrelationMiddleware> logger,
    ObservabilityRuntimeIdentity runtimeIdentity)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        var traceId = ResolveTraceId();

        context.TraceIdentifier = correlationId;
        context.Items[ObservabilityHeaders.ContextItemKey] = correlationId;
        context.Request.Headers[ObservabilityHeaders.CorrelationId] = correlationId;
        context.Request.Headers[ObservabilityHeaders.CompatibilityCorrelationId] = correlationId;

        Activity.Current?.SetTag("correlation.id", correlationId);
        Activity.Current?.AddBaggage(ObservabilityHeaders.CorrelationId, correlationId);

        context.Response.Headers[ObservabilityHeaders.CorrelationId] = correlationId;
        context.Response.Headers[ObservabilityHeaders.CompatibilityCorrelationId] = correlationId;
        context.Response.Headers[ObservabilityHeaders.TraceId] = traceId;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = traceId,
            ["ServiceName"] = runtimeIdentity.ServiceName,
            ["Environment"] = runtimeIdentity.Environment,
            ["RequestMethod"] = context.Request.Method
        });

        await next(context);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var primary = context.Request.Headers[ObservabilityHeaders.CorrelationId]
            .FirstOrDefault()?.Trim();
        if (ObservabilityHeaders.IsSafeCorrelationId(primary))
        {
            return primary!;
        }

        var compatibility = context.Request
            .Headers[ObservabilityHeaders.CompatibilityCorrelationId]
            .FirstOrDefault()?.Trim();
        return ObservabilityHeaders.IsSafeCorrelationId(compatibility)
            ? compatibility!
            : Guid.NewGuid().ToString("N");
    }

    private static string ResolveTraceId() =>
        Activity.Current?.TraceId.ToString() ?? ActivityTraceId.CreateRandom().ToString();
}
