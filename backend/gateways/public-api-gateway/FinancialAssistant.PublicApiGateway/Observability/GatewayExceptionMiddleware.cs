using System.Text.Json;

namespace FinancialAssistant.PublicApiGateway.Observability;

public sealed class GatewayExceptionMiddleware
{
    private const string ProblemJson = "application/problem+json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate next;
    private readonly ILogger<GatewayExceptionMiddleware> logger;

    public GatewayExceptionMiddleware(
        RequestDelegate next,
        ILogger<GatewayExceptionMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Gateway request was cancelled by the client.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Unhandled gateway request failure. FailureType: {FailureType}.",
                exception.GetType().Name);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = ProblemJson;
            context.Response.Headers.CacheControl = "no-store";

            var response = new
            {
                type = "https://errors.financial-assistant.app/gateway/internal-error",
                title = "The request could not be completed.",
                status = StatusCodes.Status500InternalServerError,
                code = "internal_error",
                detail = "An unexpected gateway error occurred.",
                correlationId = CorrelationHeaders.GetCorrelationId(context)
            };

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                response,
                JsonOptions,
                context.RequestAborted);
        }
    }
}
