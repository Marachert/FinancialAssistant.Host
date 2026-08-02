using FinancialAssistant.RecommendationsNotifications.Infrastructure;
using Xunit;

namespace FinancialAssistant.RecommendationsNotifications.Tests;

public sealed class RecommendationNotificationRetryPolicyTests
{
    [Fact]
    public void Policy_UsesThreeBoundedDelayedRetries()
    {
        var delays = new List<int>();
        for (var completed = 0;
             RecommendationNotificationRetryPolicy.TryGetNext(completed, out var step);
             completed++)
        {
            delays.Add(step.DelayMilliseconds);
        }

        Assert.Equal(new[] { 5_000, 30_000, 300_000 }, delays);
        Assert.False(RecommendationNotificationRetryPolicy.TryGetNext(3, out _));
    }
}
