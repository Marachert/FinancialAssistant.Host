using FinancialAssistant.AiOrchestration.Infrastructure.Usage;

namespace FinancialAssistant.AiOrchestration.Tests;

public sealed class AiUsageLimiterTests
{
    [Fact]
    public async Task TryAcquire_ConcurrentRequestsRespectLimitAndResetNextUtcDay()
    {
        var limiter = new InMemoryAiUsageLimiter();
        var utcDate = new DateOnly(2026, 7, 30);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 100)
                .Select(_ => Task.Run(() =>
                    limiter.TryAcquire(
                        "synthetic-user",
                        "synthetic-provider",
                        utcDate,
                        dailyLimit: 7))));

        Assert.Equal(7, results.Count(acquired => acquired));
        Assert.False(
            limiter.TryAcquire(
                "synthetic-user",
                "synthetic-provider",
                utcDate,
                dailyLimit: 7));
        Assert.True(
            limiter.TryAcquire(
                "synthetic-user",
                "synthetic-provider",
                utcDate.AddDays(1),
                dailyLimit: 7));
    }
}
