using FinancialAssistant.PublicApiGateway.Routing;
using FinancialAssistant.PublicApiGateway.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Diagnostics;

public sealed class GatewayReadinessHealthCheck(
    GatewayRouteCatalog routeCatalog,
    GatewayDestinationCatalog destinationCatalog,
    IOptions<GatewaySecurityOptions> securityOptions,
    IOptions<GatewayDownstreamAuthenticationOptions> downstreamAuthenticationOptions) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var activeRoutes = routeCatalog.Routes
            .Where(route => string.Equals(
                route.Status,
                GatewayRouteStatuses.Active,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var isReady = activeRoutes.Length > 0
            && destinationCatalog.Destinations.Count > 0
            && !string.IsNullOrWhiteSpace(securityOptions.Value.Mode)
            && activeRoutes.All(RouteDestinationIsReady);

        return Task.FromResult(
            isReady
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy());

        bool RouteDestinationIsReady(GatewayRouteDefinition route)
        {
            if (!destinationCatalog.TryGetDestination(route.InternalDestination, out var destination)
                || !destination.Enabled
                || !destinationCatalog.TryGetBaseAddress(route.InternalDestination, out _))
            {
                return false;
            }

            return !destination.RequiresGatewayAuthentication
                || downstreamAuthenticationOptions.Value.IsConfigured;
        }
    }
}
