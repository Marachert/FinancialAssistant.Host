using FinancialAssistant.AiOrchestration.Api.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.AiOrchestration.Api.Health;

public sealed class AiOrchestrationReadinessHealthCheck(
    IOptions<AiOrchestrationOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var value = options.Value;
        var isReady =
            !string.IsNullOrWhiteSpace(value.Name) &&
            !string.IsNullOrWhiteSpace(value.Version) &&
            string.Equals(
                value.OutputAuthority,
                AiOrchestrationOptions.SuggestionAuthority,
                StringComparison.Ordinal);

        return Task.FromResult(
            isReady
                ? HealthCheckResult.Healthy(
                    "The suggestion-only AI service boundary is configured.")
                : HealthCheckResult.Unhealthy(
                    "Required AI service boundary configuration is missing."));
    }
}
