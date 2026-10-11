using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Hosting;

namespace FinancialAssistant.Category.Infrastructure.Events;

internal sealed class CategoryOutboxRetentionWorker(PostgresCategoryEventPublisher outbox) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await outbox.CleanupPublishedBatchAsync(DateTimeOffset.UtcNow, stoppingToken); }
            catch (Exception exception) when (exception is StorageUnavailableException or StorageConflictException
                || exception is OperationCanceledException && !stoppingToken.IsCancellationRequested)
            { }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
