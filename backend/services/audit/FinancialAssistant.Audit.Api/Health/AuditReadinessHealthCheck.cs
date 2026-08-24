using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Audit.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Audit.Api.Health;

public sealed class AuditReadinessHealthCheck(IOptions<AuditOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var value = options.Value;
        var valid = value.AllowedProducers.Length > 0
            && value.RetentionDays.Count > 0
            && value.RetentionDays.All(item => item.Value is >= 1 and <= 3650)
            && (!string.Equals(value.Events.Mode, "RabbitMq", StringComparison.OrdinalIgnoreCase)
                || (Uri.TryCreate(value.Events.ConnectionString, UriKind.Absolute, out _)
                    && string.Equals(
                        value.Events.RoutingKey,
                        AuditEventTypes.Recorded,
                        StringComparison.Ordinal)));
        return Task.FromResult(valid
            ? HealthCheckResult.Healthy("Audit configuration is ready.")
            : HealthCheckResult.Unhealthy("Audit configuration is incomplete."));
    }
}
