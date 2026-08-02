using FinancialAssistant.RecommendationsNotifications.Domain;
using Xunit;

namespace FinancialAssistant.RecommendationsNotifications.Tests;

public sealed class RecommendationGeneratorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Generate_UsesOnlyDeterministicFacts()
    {
        var snapshot = new InsightSnapshot(
            "owner-hash",
            "USD",
            new AnalyticsInsightFacts(
                new DateOnly(2026, 8, 2),
                1_000m,
                1_050m,
                50m,
                60m,
                "food",
                Now),
            new ScoreInsightFacts(42, "financial-score-v1", Now));
        var generator = new RecommendationGenerator();

        var first = generator.Generate(snapshot, "source-1", Now);
        var second = generator.Generate(snapshot, "source-1", Now);

        Assert.Equal(
            first.Select(item => item.RecommendationId),
            second.Select(item => item.RecommendationId));
        Assert.Equal(
            first.Select(item => item.Code),
            second.Select(item => item.Code));
        Assert.Equal(
            new[] { "daily-limit-reached", "negative-cash-flow", "score-recovery" },
            first.Select(item => item.Code));
        Assert.All(first, item => Assert.Equal("owner-hash", item.UserIdHash));
        Assert.Contains(
            first.SelectMany(item => item.Facts),
            item => item.Code == "expense-to-income-percent" && item.Value == 105m);
    }

    [Fact]
    public void Generate_ProvidesNonInvasiveFallback()
    {
        var snapshot = new InsightSnapshot(
            "owner-hash",
            "EUR",
            new AnalyticsInsightFacts(
                new DateOnly(2026, 8, 2),
                1_000m,
                500m,
                null,
                10m,
                null,
                Now),
            new ScoreInsightFacts(60, "financial-score-v1", Now));

        var result = new RecommendationGenerator().Generate(snapshot, "source-2", Now);

        var recommendation = Assert.Single(result);
        Assert.Equal("steady-course", recommendation.Code);
        Assert.Equal(RecommendationSeverities.Information, recommendation.Severity);
    }
}
