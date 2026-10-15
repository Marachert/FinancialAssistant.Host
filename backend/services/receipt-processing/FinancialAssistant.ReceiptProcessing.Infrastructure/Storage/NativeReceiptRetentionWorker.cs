using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

internal sealed partial class NativeReceiptRetentionWorker(NativeReceiptObjectStore store, TimeProvider clock,
    ILogger<NativeReceiptRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await store.CleanupBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (NativeReceiptStorageException) { MaintenanceUnavailable(logger); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
    [LoggerMessage(EventId = 27701, Level = LogLevel.Warning, Message = "Native receipt maintenance is unavailable.")]
    private static partial void MaintenanceUnavailable(ILogger logger);
}
