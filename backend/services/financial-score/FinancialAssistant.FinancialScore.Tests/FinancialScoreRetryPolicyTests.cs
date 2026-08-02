using FinancialAssistant.FinancialScore.Infrastructure;
using Xunit;

namespace FinancialAssistant.FinancialScore.Tests;

public sealed class FinancialScoreRetryPolicyTests
{
    [Fact]
    public void RetryPolicy_UsesThreeBoundedDelays()
    {
        Assert.True(FinancialScoreRetryPolicy.TryGetNext(0, out var first));
        Assert.True(FinancialScoreRetryPolicy.TryGetNext(1, out var second));
        Assert.True(FinancialScoreRetryPolicy.TryGetNext(2, out var third));
        Assert.False(FinancialScoreRetryPolicy.TryGetNext(3, out _));

        Assert.Equal(5_000, first.DelayMilliseconds);
        Assert.Equal(30_000, second.DelayMilliseconds);
        Assert.Equal(300_000, third.DelayMilliseconds);
        Assert.Contains("income.created.v1", FinancialScoreRetryPolicy.CreateQueueName(
            "fa.financial-score.financial-events.v1",
            first,
            "income.created.v1"), StringComparison.Ordinal);
    }
}
