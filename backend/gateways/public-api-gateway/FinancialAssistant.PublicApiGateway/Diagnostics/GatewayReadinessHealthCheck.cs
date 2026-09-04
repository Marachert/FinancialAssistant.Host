using FinancialAssistant.PublicApiGateway.Routing;
using FinancialAssistant.PublicApiGateway.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Diagnostics;

public sealed class GatewayReadinessHealthCheck(
    GatewayRouteCatalog routeCatalog,
    GatewayDestinationCatalog destinationCatalog,
    IOptions<GatewaySecurityOptions> securityOptions) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isReady = routeCatalog.Routes.Count > 0
            && destinationCatalog.Destinations.Count > 0
            && !string.IsNullOrWhiteSpace(securityOptions.Value.Mode);

        return Task.FromResult(
            isReady
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy());
    }
}
