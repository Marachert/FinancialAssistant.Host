using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinancialAssistant.Shared.Observability;

public static class FinancialAssistantHealthExtensions
{
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    public static IHealthChecksBuilder AddFinancialAssistantHealthChecks(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: [LiveTag, ReadyTag]);
    }

    public static IEndpointRouteBuilder MapFinancialAssistantHealthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks(
            "/health",
            CreateOptions(_ => true));
        endpoints.MapHealthChecks(
            "/health/live",
            CreateOptions(registration => registration.Tags.Contains(LiveTag)));
        endpoints.MapHealthChecks(
            "/health/ready",
            CreateOptions(registration => registration.Tags.Contains(ReadyTag)));

        return endpoints;
    }

    public static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        var identity = context.RequestServices.GetRequiredService<ObservabilityRuntimeIdentity>();
        context.Response.Headers.CacheControl = "no-store";

        return context.Response.WriteAsJsonAsync(
            new
            {
                status = Normalize(report.Status),
                service = identity.ServiceName,
                environment = identity.Environment,
                checkedAtUtc = DateTimeOffset.UtcNow,
                durationMilliseconds = ToMilliseconds(report.TotalDuration),
                checks = report.Entries
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => new
                    {
                        name = entry.Key,
                        status = Normalize(entry.Value.Status),
                        durationMilliseconds = ToMilliseconds(entry.Value.Duration),
                        errorCategory = GetErrorCategory(entry.Value)
                    })
                    .ToArray()
            });
    }

    private static HealthCheckOptions CreateOptions(
        Func<HealthCheckRegistration, bool> predicate) =>
        new()
        {
            Predicate = predicate,
            ResponseWriter = WriteHealthResponseAsync
        };

    private static string Normalize(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "healthy",
        HealthStatus.Degraded => "degraded",
        _ => "unavailable"
    };

    private static long ToMilliseconds(TimeSpan duration) =>
        Math.Max(0, (long)Math.Ceiling(duration.TotalMilliseconds));

    private static string? GetErrorCategory(HealthReportEntry entry)
    {
        if (entry.Status == HealthStatus.Healthy)
        {
            return null;
        }

        return entry.Exception is OperationCanceledException or TimeoutException
            ? "timeout"
            : "check_failed";
    }
}
