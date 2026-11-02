using System.Text.Json;

namespace FinancialAssistant.PublicApiGateway.Observability;

internal static class GatewayRequestBodyErrors
{
    public static bool IsTooLarge(Exception exception)
    {
        // HttpClient wraps local request-body read errors during streaming dispatch.
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge }) { return true; }
        }
        return false;
    }

    public static async Task RejectTooLargeAsync(HttpContext context)
    {
        if (context.Response.HasStarted) { context.Abort(); return; }
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers.CacheControl = "no-store";
        if (context.Request.Protocol is "HTTP/1.0" or "HTTP/1.1") { context.Response.Headers.Connection = "close"; }
        await JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            type = "https://errors.financial-assistant.app/gateway/request-body-too-large",
            title = "Request body is too large.",
            status = StatusCodes.Status413PayloadTooLarge,
            code = "request_body_too_large",
            detail = "The request exceeds the configured body limit.",
            correlationId = CorrelationHeaders.GetCorrelationId(context)
        }, cancellationToken: context.RequestAborted);
    }
}
