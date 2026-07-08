using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.AspNetCore.RateLimiting;

namespace FinancialAssistant.Identity.Api.RateLimiting;

internal static class IdentityRateLimitingExtensions
{
    private const string ProblemJson = "application/problem+json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddIdentityRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection($"{IdentityServiceOptions.SectionName}:RateLimiting")
            .Get<IdentityRateLimitingOptions>() ?? new IdentityRateLimitingOptions();
        Validate(options);

        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiterOptions.OnRejected = WriteRejectedAsync;
            AddPolicy(rateLimiterOptions, IdentityRateLimitPolicies.Registration, options.Registration, options);
            AddPolicy(rateLimiterOptions, IdentityRateLimitPolicies.SignIn, options.SignIn, options);
            AddPolicy(rateLimiterOptions, IdentityRateLimitPolicies.ProviderSignIn, options.ProviderSignIn, options);
            AddPolicy(rateLimiterOptions, IdentityRateLimitPolicies.PhoneStart, options.PhoneStart, options);
            AddPolicy(rateLimiterOptions, IdentityRateLimitPolicies.PhoneConfirm, options.PhoneConfirm, options);
            AddPolicy(rateLimiterOptions, IdentityRateLimitPolicies.Session, options.Session, options);
        });

        return services;
    }

    private static void AddPolicy(
        RateLimiterOptions rateLimiterOptions,
        string policyName,
        IdentityFixedWindowPolicyOptions policy,
        IdentityRateLimitingOptions options)
    {
        rateLimiterOptions.AddPolicy(
            policyName,
            context => RateLimitPartition.GetFixedWindowLimiter(
                CreatePartitionKey(context, policyName, options),
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = options.Enabled ? policy.PermitLimit : int.MaxValue,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    Window = TimeSpan.FromSeconds(options.Enabled ? policy.WindowSeconds : 1)
                }));
    }

    private static string CreatePartitionKey(
        HttpContext context,
        string policyName,
        IdentityRateLimitingOptions options)
    {
        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var clientInstance = ReadClientInstance(context, options.ClientInstanceHeaderName);
        var material = $"{policyName}|{remoteAddress}|{clientInstance}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"{policyName}:{Convert.ToHexString(digest)}";
    }

    private static string ReadClientInstance(HttpContext context, string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return "none";
        }

        var value = context.Request.Headers[headerName].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value)
            || value.Length is < 8 or > 128
            || value.Any(char.IsControl)
                ? "none"
                : value;
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

    private static void Validate(IdentityRateLimitingOptions options)
    {
        foreach (var policy in new[]
        {
            options.Registration,
            options.SignIn,
            options.ProviderSignIn,
            options.PhoneStart,
            options.PhoneConfirm,
            options.Session
        })
        {
            if (policy.PermitLimit < 1 || policy.WindowSeconds < 1)
            {
                throw new InvalidOperationException("Identity rate limit configuration is invalid.");
            }
        }
    }
}
