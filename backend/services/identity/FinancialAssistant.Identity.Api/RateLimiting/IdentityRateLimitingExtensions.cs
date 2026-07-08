using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Api.RateLimiting;

internal static class IdentityRateLimitingExtensions
{
    private const string ProblemJson = "application/problem+json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddIdentityRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration;
        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiterOptions.OnRejected = WriteRejectedAsync;
            AddPolicy(
                rateLimiterOptions,
                IdentityRateLimitPolicies.Registration,
                options => options.Registration);
            AddPolicy(
                rateLimiterOptions,
                IdentityRateLimitPolicies.SignIn,
                options => options.SignIn);
            AddPolicy(
                rateLimiterOptions,
                IdentityRateLimitPolicies.ProviderSignIn,
                options => options.ProviderSignIn);
            AddPolicy(
                rateLimiterOptions,
                IdentityRateLimitPolicies.PhoneStart,
                options => options.PhoneStart);
            AddPolicy(
                rateLimiterOptions,
                IdentityRateLimitPolicies.PhoneConfirm,
                options => options.PhoneConfirm);
            AddPolicy(
                rateLimiterOptions,
                IdentityRateLimitPolicies.Session,
                options => options.Session);
        });

        return services;
    }

    private static void AddPolicy(
        RateLimiterOptions rateLimiterOptions,
        string policyName,
        Func<IdentityRateLimitingOptions, IdentityFixedWindowPolicyOptions> selectPolicy)
    {
        rateLimiterOptions.AddPolicy(
            policyName,
            context =>
            {
                var options = context.RequestServices
                    .GetRequiredService<IOptions<IdentityServiceOptions>>()
                    .Value
                    .RateLimiting;
                var policy = selectPolicy(options);
                Validate(policy);
                return RateLimitPartition.GetFixedWindowLimiter(
                    CreatePartitionKey(context, policyName),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = options.Enabled ? policy.PermitLimit : int.MaxValue,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        Window = TimeSpan.FromSeconds(options.Enabled ? policy.WindowSeconds : 1)
                    });
            });
    }

    private static string CreatePartitionKey(HttpContext context, string policyName)
    {
        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var material = $"{policyName}|{remoteAddress}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"{policyName}:{Convert.ToHexString(digest)}";
    }

    private static async ValueTask WriteRejectedAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var retryAfterSeconds = 60;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        }

        var httpContext = context.HttpContext;
        var suppliedCorrelationId = httpContext.Request.Headers[IdentityApiHeaders.CorrelationId].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(suppliedCorrelationId)
            ? Activity.Current?.Id ?? httpContext.TraceIdentifier
            : suppliedCorrelationId;
        var response = new IdentityApiErrorResponse(
            "https://errors.financial-assistant.app/identity/rate-limited",
            "Request rate limit exceeded.",
            StatusCodes.Status429TooManyRequests,
            IdentityErrorCodes.RateLimited,
            "Too many requests were received. Wait before trying again.",
            correlationId,
            RetryAfterSeconds: retryAfterSeconds);

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.ContentType = ProblemJson;
        httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        httpContext.Response.Headers.CacheControl = "no-store";
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            response,
            JsonOptions,
            cancellationToken);
    }

    private static void Validate(IdentityFixedWindowPolicyOptions policy)
    {
        if (policy.PermitLimit < 1 || policy.WindowSeconds < 1)
        {
            throw new InvalidOperationException("Identity rate limit configuration is invalid.");
        }
    }
}
