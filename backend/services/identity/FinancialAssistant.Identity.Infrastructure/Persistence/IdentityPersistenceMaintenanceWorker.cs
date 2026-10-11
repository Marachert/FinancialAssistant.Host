using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Hosting;

namespace FinancialAssistant.Identity.Infrastructure.Persistence;

internal sealed class IdentityPersistenceMaintenanceWorker(IdentityPersistenceMaintenance maintenance, ISystemClock clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await maintenance.CleanupBatchAsync(clock.UtcNow, stoppingToken);
                await maintenance.ProcessDeletionBatchAsync(clock.UtcNow, stoppingToken);
            }
            catch (Exception exception) when (exception is StorageUnavailableException or StorageConflictException
                || exception is OperationCanceledException && !stoppingToken.IsCancellationRequested)
            {
                // Storage/readiness remains unavailable; do not log sensitive exception contents or replace the store.
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
