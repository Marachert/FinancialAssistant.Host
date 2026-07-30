using FinancialAssistant.ReceiptProcessing.Infrastructure.Usage;

namespace FinancialAssistant.ReceiptProcessing.Tests;

public sealed class OcrUsageLimiterTests
{
    [Fact]
    public async Task TryAcquire_ConcurrentRequestsRespectLimitAndResetNextUtcDay()
    {
        var limiter = new InMemoryOcrUsageLimiter();
        var utcDate = new DateOnly(2026, 7, 30);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 100)
                .Select(_ => Task.Run(() =>
                    limiter.TryAcquire(
                        "synthetic-user",
                        "synthetic-provider",
                        utcDate,
                        dailyLimit: 5))));

        Assert.Equal(5, results.Count(acquired => acquired));
        Assert.False(
            limiter.TryAcquire(
                "synthetic-user",
                "synthetic-provider",
                utcDate,
                dailyLimit: 5));
        Assert.True(
            limiter.TryAcquire(
                "synthetic-user",
                "synthetic-provider",
                utcDate.AddDays(1),
                dailyLimit: 5));
    }
}
