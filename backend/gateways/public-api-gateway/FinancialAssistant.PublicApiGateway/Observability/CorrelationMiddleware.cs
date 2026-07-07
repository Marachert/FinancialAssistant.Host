using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Observability;

public sealed class CorrelationMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<CorrelationMiddleware> logger;
    private readonly CorrelationOptions options;

    public CorrelationMiddleware(
        RequestDelegate next,
        ILogger<CorrelationMiddleware> logger,
        IOptions<CorrelationOptions> options)
    {
        this.next = next;
        this.logger = logger;
        this.options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers, options);
        context.Items[CorrelationHeaders.ContextItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[options.PrimaryHeaderName] = correlationId;
            context.Response.Headers[options.CompatibilityHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        var activity = Activity.Current;
        activity?.SetTag("correlation.id", correlationId);
        activity?.AddBaggage(CorrelationHeaders.CorrelationId, correlationId);

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = activity?.TraceId.ToString() ?? context.TraceIdentifier
        });

        logger.LogInformation("Gateway request started.");

        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation("Gateway request completed with status code {StatusCode}.", context.Response.StatusCode);
        }
    }

    private static string ResolveCorrelationId(IHeaderDictionary headers, CorrelationOptions options)
    {
        var maxLength = options.MaxCorrelationIdLength > 0 ? options.MaxCorrelationIdLength : 128;

        var primaryCorrelationId = ReadHeader(headers, options.PrimaryHeaderName);
        if (IsValidCorrelationId(primaryCorrelationId, maxLength))
        {
            return primaryCorrelationId!;
        }

        var compatibilityCorrelationId = ReadHeader(headers, options.CompatibilityHeaderName);
        if (IsValidCorrelationId(compatibilityCorrelationId, maxLength))
        {
            return compatibilityCorrelationId!;
        }

        return Guid.NewGuid().ToString("D");
    }

    private static string? ReadHeader(IHeaderDictionary headers, string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName) || !headers.TryGetValue(headerName, out var values))
        {
            return null;
        }

        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static bool IsValidCorrelationId(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            return false;
        }

        return value.All(character => !char.IsControl(character));
    }
}
