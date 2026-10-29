using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.Audit.Infrastructure.Persistence;

internal sealed class AuditRetentionWorker(PostgresAuditRecordStore store, TimeProvider clock,
    ILogger<AuditRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            try { await store.CleanupExpiredAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) when (exception is StorageUnavailableException or StorageConflictException)
            { logger.LogWarning(new EventId(28901), "Audit retention storage is unavailable; cleanup will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
