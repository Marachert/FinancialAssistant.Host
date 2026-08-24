using FinancialAssistant.Mcp.Application;
using FinancialAssistant.Mcp.Contracts;

namespace FinancialAssistant.Mcp.Api.Security;

public sealed class McpRequestAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IMcpAuditSink auditSink,
        TimeProvider timeProvider)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp"))
        {
            await next(context);
            return;
        }

        var correlationId = GetCorrelationId(context);
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            await next(context);
            var outcome = context.Response.StatusCode is >= 200 and < 400
                ? "succeeded"
                : "denied";
            await auditSink.RecordAsync(
                new McpAuditEntry(
                    correlationId,
                    "protocol_request",
                    outcome,
                    outcome == "denied" ? "http_status" : null,
                    timeProvider.GetUtcNow()),
                context.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await auditSink.RecordAsync(
                new McpAuditEntry(
                    correlationId,
                    "protocol_request",
                    "failed",
                    "internal",
                    timeProvider.GetUtcNow()),
                CancellationToken.None);
            throw;
        }
    }

    public static string GetCorrelationId(HttpContext context)
    {
        var value = context.Request.Headers[McpHeaders.CorrelationId].SingleOrDefault();
        return IsSafe(value) ? value! : context.TraceIdentifier.Replace(':', '-');
    }

    private static bool IsSafe(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => !char.IsControl(character) && !char.IsWhiteSpace(character));
}
