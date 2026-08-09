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

    [Fact]
    public void Generate_EmitsMvpRiskRulesFromConfirmedAndProfileFacts()
    {
        var snapshot = new InsightSnapshot(
            "owner-hash",
            "USD",
            new AnalyticsInsightFacts(
                new DateOnly(2026, 8, 2),
                1_000m,
                850m,
                null,
                20m,
                "expense.housing",
                Now,
                400m,
                75m),
            null,
            new RecommendationProfileSettings(true, false, 1_000m));

        var result = new RecommendationGenerator().Generate(
            snapshot,
            "mvp-risk-rules",
            Now);

        Assert.Contains(result, item => item.Code == "high-spending-category");
        Assert.Contains(result, item => item.Code == "monthly-budget-nearing-limit");
        Assert.Contains(result, item => item.Code == "incomplete-profile");
        Assert.Contains(result, item => item.Code == "uncategorized-expenses");
        Assert.Equal(
            result.Count,
            result.Select(item => item.Code).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Generate_EmitsMissingIncomeRule()
    {
        var snapshot = new InsightSnapshot(
            "owner-hash",
            "USD",
            new AnalyticsInsightFacts(
                new DateOnly(2026, 8, 2),
                0m,
                200m,
                null,
                20m,
                null,
                Now),
            null);

        var result = new RecommendationGenerator().Generate(
            snapshot,
            "missing-income-rule",
            Now);

        Assert.Contains(result, item => item.Code == "missing-income");
    }

    [Fact]
    public void Generate_EmitsPositiveProgressOnlyWithoutRiskSignals()
    {
        var snapshot = new InsightSnapshot(
            "owner-hash",
            "USD",
            new AnalyticsInsightFacts(
                new DateOnly(2026, 8, 2),
                1_000m,
                500m,
                null,
                20m,
                null,
                Now),
            null,
            new RecommendationProfileSettings(true, true, 1_000m));

        var result = new RecommendationGenerator().Generate(
            snapshot,
            "positive-progress-rule",
            Now);

        var recommendation = Assert.Single(result);
        Assert.Equal("positive-budget-progress", recommendation.Code);
        Assert.Equal(
            RecommendationSeverities.Information,
            recommendation.Severity);
    }

    [Fact]
    public void Generate_StartsRecommendationsActiveAndLifecycleIsTerminal()
    {
        var generatedAt = new DateTimeOffset(2026, 8, 9, 6, 0, 0, TimeSpan.Zero);
        var snapshot = new InsightSnapshot(
            "owner-hash",
            "USD",
            new AnalyticsInsightFacts(
                new DateOnly(2026, 8, 9),
                1_000m,
                900m,
                null,
                0m,
                null,
                generatedAt),
            null);
        var result = new RecommendationGenerator().Generate(
            snapshot,
            "lifecycle-event",
            generatedAt);

        Assert.NotEmpty(result);
        Assert.All(result, recommendation =>
        {
            Assert.Equal(RecommendationStatuses.Active, recommendation.Status);
            Assert.Equal(generatedAt, recommendation.StatusChangedAtUtc);
        });
        Assert.True(RecommendationStatuses.CanTransition(
            RecommendationStatuses.Active,
            RecommendationStatuses.Read));
        Assert.True(RecommendationStatuses.CanTransition(
            RecommendationStatuses.Read,
            RecommendationStatuses.Dismissed));
        Assert.True(RecommendationStatuses.CanTransition(
            RecommendationStatuses.Active,
            RecommendationStatuses.Dismissed));
        Assert.True(RecommendationStatuses.CanTransition(
            RecommendationStatuses.Active,
            RecommendationStatuses.Expired));
        Assert.False(RecommendationStatuses.CanTransition(
            RecommendationStatuses.Dismissed,
            RecommendationStatuses.Active));
        Assert.False(RecommendationStatuses.CanTransition(
            RecommendationStatuses.Expired,
            RecommendationStatuses.Dismissed));
    }
}

