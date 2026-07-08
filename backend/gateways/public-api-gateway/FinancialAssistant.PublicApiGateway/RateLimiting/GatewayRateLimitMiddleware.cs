using System.Globalization;
using System.Text.Json;
using FinancialAssistant.PublicApiGateway.Observability;

namespace FinancialAssistant.PublicApiGateway.RateLimiting;

public sealed class GatewayRateLimitMiddleware
{
    private const string ProblemJson = "application/problem+json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate next;

    public GatewayRateLimitMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        GatewayRateLimitCatalog catalog,
        GatewayRateLimitPartitioner partitioner,
        GatewayRateLimiter limiter)
    {
        var decision = catalog.Classify(context);
        if (!decision.IsLimited)
        {
            await next(context);
            return;
        }

        var partitionKey = partitioner.CreatePartitionKey(context, decision);
        var lease = await limiter.AcquireAsync(
            decision.PolicyName,
            partitionKey,
            decision.Policy,
            context.RequestAborted);
        if (lease.IsAcquired)
        {
            await next(context);
            return;
        }

        await WriteRejectedAsync(context, lease.RetryAfterSeconds);
    }

    private static async Task WriteRejectedAsync(HttpContext context, int retryAfterSeconds)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = ProblemJson;
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers.CacheControl = "no-store";

        var response = new
        {
            type = "https://errors.financial-assistant.app/gateway/rate-limited",
            title = "Request rate limit exceeded.",
            status = StatusCodes.Status429TooManyRequests,
            code = "rate_limited",
            detail = "Too many requests were received. Wait before trying again.",
            correlationId = CorrelationHeaders.GetCorrelationId(context),
            retryAfterSeconds
        };
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            JsonOptions,
            context.RequestAborted);
    }
}
