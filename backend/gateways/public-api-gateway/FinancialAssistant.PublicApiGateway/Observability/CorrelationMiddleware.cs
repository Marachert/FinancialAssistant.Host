using System.Diagnostics;
using System.Globalization;
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

        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString() ?? context.TraceIdentifier;
        activity?.SetTag("correlation.id", correlationId);
        activity?.SetTag("http.request.method", context.Request.Method);
        activity?.AddBaggage(CorrelationHeaders.CorrelationId, correlationId);

        var stopwatch = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            stopwatch.Stop();
            context.Response.Headers[options.PrimaryHeaderName] = correlationId;
            context.Response.Headers[options.CompatibilityHeaderName] = correlationId;
            context.Response.Headers[options.TraceIdHeaderName] = traceId;
            context.Response.Headers[options.RequestDurationHeaderName] =
                $"gateway;dur={stopwatch.Elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)}";
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = traceId,
            ["RequestMethod"] = context.Request.Method
        });

        logger.LogInformation("Gateway request started.");

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Gateway request completed with status code {StatusCode} in {ElapsedMilliseconds} ms.",
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds);
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
