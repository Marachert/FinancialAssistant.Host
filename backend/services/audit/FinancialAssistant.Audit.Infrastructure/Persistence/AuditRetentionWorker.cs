using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.Audit.Infrastructure.Persistence;

internal sealed class AuditRetentionWorker : BackgroundService
{
    private readonly Func<CancellationToken, Task<AuditCleanupResult>> cleanup;
    private readonly TimeProvider clock;
    private readonly ILogger<AuditRetentionWorker> logger;

    public AuditRetentionWorker(PostgresAuditRecordStore store, TimeProvider clock, ILogger<AuditRetentionWorker> logger)
        : this(store.CleanupExpiredAsync, clock, logger) { }

    internal AuditRetentionWorker(Func<CancellationToken, Task<AuditCleanupResult>> cleanup,
        TimeProvider clock, ILogger<AuditRetentionWorker> logger)
    { this.cleanup = cleanup; this.clock = clock; this.logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            try { await cleanup(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) when (exception is OperationCanceledException or StorageUnavailableException or StorageConflictException)
            { logger.LogWarning(new EventId(28901), "Audit retention storage is unavailable; cleanup will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
