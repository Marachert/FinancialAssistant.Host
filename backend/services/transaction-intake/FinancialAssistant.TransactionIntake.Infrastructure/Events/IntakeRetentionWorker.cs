using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Infrastructure.Storage;
using Microsoft.Extensions.Hosting;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Events;

internal sealed class IntakeRetentionWorker(PostgresTransactionDraftCreationStore sources, PostgresIntakeOutbox outbox) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await sources.CleanupExpiredBatchAsync(stoppingToken);
                await outbox.CleanupPublishedBatchAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (Exception exception) when (exception is StorageUnavailableException or StorageConflictException
                || exception is OperationCanceledException && !stoppingToken.IsCancellationRequested)
            { }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
