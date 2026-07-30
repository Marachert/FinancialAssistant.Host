using FinancialAssistant.AiOrchestration.Application.Abstractions;

namespace FinancialAssistant.AiOrchestration.Infrastructure.Usage;

public sealed class InMemoryAiUsageLimiter : IAiUsageLimiter
{
    private readonly object gate = new();
    private readonly Dictionary<UsageKey, int> requestsBySubject = [];

    public bool TryAcquire(
        string usageSubjectId,
        string providerName,
        DateOnly utcDate,
        int dailyLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usageSubjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentOutOfRangeException.ThrowIfLessThan(dailyLimit, 1);

        lock (gate)
        {
            foreach (var expired in requestsBySubject.Keys
                         .Where(key => key.UtcDate < utcDate)
                         .ToArray())
            {
                requestsBySubject.Remove(expired);
            }

            var key = new UsageKey(usageSubjectId, providerName, utcDate);
            requestsBySubject.TryGetValue(key, out var current);
            if (current >= dailyLimit)
            {
                return false;
            }

            requestsBySubject[key] = current + 1;
            return true;
        }
    }

    private sealed record UsageKey(
        string UsageSubjectId,
        string ProviderName,
        DateOnly UtcDate);
}
