using FinancialAssistant.ServiceTemplate.Api.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.ServiceTemplate.Api.Health;

public sealed class ServiceReadinessHealthCheck(IOptions<ServiceOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var value = options.Value;
        var isReady =
            !string.IsNullOrWhiteSpace(value.Name) &&
            !string.IsNullOrWhiteSpace(value.Version);

        return Task.FromResult(
            isReady
                ? HealthCheckResult.Healthy("Required service configuration is available.")
                : HealthCheckResult.Unhealthy("Required service configuration is missing."));
    }
}
