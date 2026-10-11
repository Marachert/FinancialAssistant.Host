using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinancialAssistant.Shared.Persistence;

public sealed class OwnedStorageHealthCheck(OwnedPostgresStore store, bool lifecycleDeliveryPending = true) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!await store.IsReadyAsync(cancellationToken)) { return HealthCheckResult.Unhealthy("Owned PostgreSQL storage is unavailable."); }
        return lifecycleDeliveryPending ? HealthCheckResult.Unhealthy("Native lifecycle delivery qualification is pending FIN-276.")
            : HealthCheckResult.Healthy("Owned PostgreSQL storage verified.");
    }
}
