using System.Security.Cryptography;
using System.Text;

namespace FinancialAssistant.RecommendationsNotifications.Domain;

public sealed class RecommendationGenerator
{
    public IReadOnlyList<FinancialRecommendation> Generate(
        InsightSnapshot snapshot,
        string sourceEventId,
        DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var recommendations = new List<FinancialRecommendation>();

        if (snapshot.Analytics is { } analytics)
        {
            AddLimitRecommendation(recommendations, snapshot, analytics, sourceEventId, generatedAtUtc);
            AddCashFlowRecommendation(recommendations, snapshot, analytics, sourceEventId, generatedAtUtc);
        }

        if (snapshot.Score is { } score)
        {
            if (score.Score < 50)
            {
                recommendations.Add(Create(
                    snapshot,
                    sourceEventId,
                    generatedAtUtc,
                    "score-recovery",
                    RecommendationSeverities.Warning,
                    "Focus on one score factor",
                    "Review the score factors and improve the largest negative deterministic factor first.",
                    new RecommendationFact("score", score.Score)));
            }
            else if (score.Score >= 75)
            {
                recommendations.Add(Create(
                    snapshot,
                    sourceEventId,
                    generatedAtUtc,
                    "score-strength",
                    RecommendationSeverities.Information,
                    "Your financial pattern is stable",
                    "Keep the current pattern and review changes before they affect monthly cash flow.",
                    new RecommendationFact("score", score.Score)));
            }
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(Create(
                snapshot,
                sourceEventId,
                generatedAtUtc,
                "steady-course",
                RecommendationSeverities.Information,
                "Keep tracking your finances",
                "No urgent deterministic signal is present. Continue confirming transactions for useful trends.",
                Array.Empty<RecommendationFact>()));
        }

        return recommendations;
    }

    private static void AddLimitRecommendation(
        ICollection<FinancialRecommendation> recommendations,
        InsightSnapshot snapshot,
        AnalyticsInsightFacts analytics,
        string sourceEventId,
        DateTimeOffset generatedAtUtc)
    {
        if (analytics.DailyExpenseLimit is not > 0m ||
            analytics.DailyExpenseSpent < analytics.DailyExpenseLimit.Value)
        {
            return;
        }

        recommendations.Add(Create(
            snapshot,
            sourceEventId,
            generatedAtUtc,
            "daily-limit-reached",
            RecommendationSeverities.Critical,
            "Daily spending reached your limit",
            "Review today's confirmed expenses before making another non-essential purchase.",
            new RecommendationFact("daily-expense-limit", analytics.DailyExpenseLimit.Value),
            new RecommendationFact("daily-expense-spent", analytics.DailyExpenseSpent)));
    }

    private static void AddCashFlowRecommendation(
        ICollection<FinancialRecommendation> recommendations,
        InsightSnapshot snapshot,
        AnalyticsInsightFacts analytics,
        string sourceEventId,
        DateTimeOffset generatedAtUtc)
    {
        if (analytics.MonthlyIncomeTotal <= 0m)
        {
            return;
        }

        var ratio = Math.Round(
            analytics.MonthlyExpenseTotal / analytics.MonthlyIncomeTotal * 100m,
            2,
            MidpointRounding.AwayFromZero);
        if (ratio >= 100m)
        {
            recommendations.Add(Create(
                snapshot,
                sourceEventId,
                generatedAtUtc,
                "negative-cash-flow",
                RecommendationSeverities.Critical,
                "Monthly expenses reached income",
                "Review the largest confirmed expense categories and pause optional spending.",
                new RecommendationFact("expense-to-income-percent", ratio)));
        }
        else if (ratio >= 85m)
        {
            recommendations.Add(Create(
                snapshot,
                sourceEventId,
                generatedAtUtc,
                "spending-pressure",
                RecommendationSeverities.Warning,
                "Monthly spending is close to income",
                "Check upcoming expenses and protect room for essential payments.",
                new RecommendationFact("expense-to-income-percent", ratio)));
        }
    }

    private static FinancialRecommendation Create(
        InsightSnapshot snapshot,
        string sourceEventId,
        DateTimeOffset generatedAtUtc,
        string code,
        string severity,
        string title,
        string body,
        params RecommendationFact[] facts) =>
        new(
            StableId("recommendation", snapshot.UserIdHash, snapshot.Currency, sourceEventId, code),
            snapshot.UserIdHash,
            snapshot.Currency,
            code,
            severity,
            title,
            body,
            facts,
            sourceEventId,
            generatedAtUtc.ToUniversalTime(),
            RecommendationStatuses.Active,
            generatedAtUtc.ToUniversalTime());

    internal static string StableId(params string[] components)
    {
        var material = string.Join('|', components);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }
}
