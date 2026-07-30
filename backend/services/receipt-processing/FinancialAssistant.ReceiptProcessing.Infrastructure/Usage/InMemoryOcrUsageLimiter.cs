using FinancialAssistant.ReceiptProcessing.Application.Abstractions;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Usage;

public sealed class InMemoryOcrUsageLimiter : IOcrUsageLimiter
{
    private readonly object gate = new();
    private readonly Dictionary<UsageKey, int> requestsByUser = [];

    public bool TryAcquire(
        string userId,
        string providerName,
        DateOnly utcDate,
        int dailyLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentOutOfRangeException.ThrowIfLessThan(dailyLimit, 1);

        lock (gate)
        {
            foreach (var expired in requestsByUser.Keys
                         .Where(key => key.UtcDate < utcDate)
                         .ToArray())
            {
                requestsByUser.Remove(expired);
            }

            var key = new UsageKey(userId, providerName, utcDate);
            requestsByUser.TryGetValue(key, out var current);
            if (current >= dailyLimit)
            {
                return false;
            }

            requestsByUser[key] = current + 1;
            return true;
        }
    }

    private sealed record UsageKey(
        string UserId,
        string ProviderName,
        DateOnly UtcDate);
}
