using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace FinancialAssistant.Shared.Observability;

public sealed class CorrelationPropagationHandler(IHttpContextAccessor httpContextAccessor)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext;
        var correlationId = ResolveCorrelationId(context);
        if (correlationId is not null)
        {
            SetHeader(request, ObservabilityHeaders.CorrelationId, correlationId);
            SetHeader(request, ObservabilityHeaders.CompatibilityCorrelationId, correlationId);
        }

        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            SetHeader(request, ObservabilityHeaders.TraceId, traceId);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static string? ResolveCorrelationId(HttpContext? context)
    {
        if (context?.Items[ObservabilityHeaders.ContextItemKey] is string itemValue
            && ObservabilityHeaders.IsSafeCorrelationId(itemValue))
        {
            return itemValue;
        }

        var requestValue = context?.Request.Headers[ObservabilityHeaders.CorrelationId]
            .FirstOrDefault();
        if (ObservabilityHeaders.IsSafeCorrelationId(requestValue))
        {
            return requestValue;
        }

        return ObservabilityHeaders.IsSafeCorrelationId(context?.TraceIdentifier)
            ? context!.TraceIdentifier
            : null;
    }

    private static void SetHeader(
        HttpRequestMessage request,
        string name,
        string value)
    {
        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }
}
