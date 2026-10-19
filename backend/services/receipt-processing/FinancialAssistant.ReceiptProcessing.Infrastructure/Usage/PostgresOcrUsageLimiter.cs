using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Usage;

public sealed class PostgresOcrUsageLimiter(ReceiptPersistenceContext context) : IOcrUsageLimiter
{
    public bool TryAcquire(string userId, string providerName, DateOnly utcDate, int dailyLimit) =>
        throw new InvalidOperationException("Durable usage requires asynchronous acquisition and a stable request identity.");

    public Task<bool> TryAcquireAsync(string userId, string providerName, DateOnly utcDate, int dailyLimit,
        string requestId, CancellationToken token)
    {
        OwnedUsageReservations.Validate(userId, providerName, utcDate, dailyLimit, requestId);
        return context.WithRecordsAsync(userId, (records, _) =>
            OwnedUsageReservations.TryAcquireAsync(records, userId, providerName, utcDate, dailyLimit, requestId), token);
    }
}
