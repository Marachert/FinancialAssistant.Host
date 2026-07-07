using System.Diagnostics;
using FinancialAssistant.PublicApiGateway.Observability;
using FinancialAssistant.PublicApiGateway.Routing;
using FinancialAssistant.PublicApiGateway.Security;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Diagnostics;

public static class GatewayDiagnosticsEndpointExtensions
{
    public static WebApplication MapGatewayDiagnostics(this WebApplication app)
    {
        app.MapGet(
            "/health/live",
            (HttpContext context, GatewayDiagnosticsClock clock) => Results.Ok(new
            {
                status = "live",
                service = "financial-assistant-public-api-gateway",
                startedAtUtc = clock.StartedAtUtc,
                uptimeSeconds = Math.Floor(clock.Uptime.TotalSeconds),
                correlationId = CorrelationHeaders.GetCorrelationId(context)
            }));

        app.MapGet(
            "/health/ready",
            (
                HttpContext context,
                GatewayRouteCatalog routeCatalog,
                GatewayDestinationCatalog destinationCatalog,
                IOptions<GatewaySecurityOptions> securityOptions) => Results.Ok(new
                {
                    status = "ready",
                    service = "financial-assistant-public-api-gateway",
                    routeCount = routeCatalog.Routes.Count,
                    destinationCount = destinationCatalog.Destinations.Count,
                    enabledDestinationCount = destinationCatalog.Destinations.Count(destination => destination.Enabled),
                    securityMode = securityOptions.Value.Mode,
                    correlationId = CorrelationHeaders.GetCorrelationId(context)
                }));

        app.MapGet(
            "/gateway/status",
            (
                HttpContext context,
                IHostEnvironment environment,
                GatewayDiagnosticsClock clock,
                GatewayRouteCatalog routeCatalog,
                GatewayDestinationCatalog destinationCatalog,
                IOptions<GatewaySecurityOptions> securityOptions) => Results.Ok(new
                {
                    service = "financial-assistant-public-api-gateway",
                    status = "running",
                    environment = environment.EnvironmentName,
                    generatedAtUtc = DateTimeOffset.UtcNow,
                    startedAtUtc = clock.StartedAtUtc,
                    uptimeSeconds = Math.Floor(clock.Uptime.TotalSeconds),
                    routeSummary = BuildRouteSummary(routeCatalog),
                    destinationSummary = BuildDestinationSummary(destinationCatalog),
                    securityMode = securityOptions.Value.Mode,
                    correlationId = CorrelationHeaders.GetCorrelationId(context),
                    traceId = Activity.Current?.TraceId.ToString()
                }));

        return app;
    }

    private static object BuildRouteSummary(GatewayRouteCatalog routeCatalog)
    {
        return new
        {
            total = routeCatalog.Routes.Count,
            byStatus = routeCatalog.Routes
                .GroupBy(route => NormalizeValue(route.Status, "unknown"))
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Count()),
            byAccessPolicy = routeCatalog.Routes
                .GroupBy(route => NormalizeValue(route.AccessPolicy, "unknown"))
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Count()),
            adminRouteCount = routeCatalog.Routes.Count(route =>
                string.Equals(route.AccessPolicy, GatewayAccessPolicies.Admin, StringComparison.OrdinalIgnoreCase))
        };
    }

    private static object BuildDestinationSummary(GatewayDestinationCatalog destinationCatalog)
    {
        return new
        {
            total = destinationCatalog.Destinations.Count,
            enabled = destinationCatalog.Destinations.Count(destination => destination.Enabled),
            disabled = destinationCatalog.Destinations.Count(destination => !destination.Enabled)
        };
    }

    private static string NormalizeValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }
}
