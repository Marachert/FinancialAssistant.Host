using System.Security.Cryptography;
using System.Text;

namespace FinancialAssistant.RecommendationsNotifications.Domain;

public sealed class RecommendationGenerator
{
    private const decimal HighCategorySharePercent = 40m;
    private const decimal BudgetNearLimitPercent = 80m;
    private const decimal PositiveBudgetMaximumPercent = 75m;

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
            AddHighCategoryRecommendation(recommendations, snapshot, analytics, sourceEventId, generatedAtUtc);
            AddBudgetRecommendation(recommendations, snapshot, analytics, sourceEventId, generatedAtUtc);
            AddMissingIncomeRecommendation(recommendations, snapshot, analytics, sourceEventId, generatedAtUtc);
            AddIncompleteProfileRecommendation(recommendations, snapshot, sourceEventId, generatedAtUtc);
            AddUncategorizedRecommendation(recommendations, snapshot, analytics, sourceEventId, generatedAtUtc);
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

        if (recommendations.Count == 0 && snapshot.Analytics is { } positiveAnalytics)
        {
            AddPositiveProgressRecommendation(
                recommendations,
                snapshot,
                positiveAnalytics,
                sourceEventId,
                generatedAtUtc);
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

        return recommendations
            .DistinctBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
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

    private static void AddHighCategoryRecommendation(
        ICollection<FinancialRecommendation> recommendations,
        InsightSnapshot snapshot,
        AnalyticsInsightFacts analytics,
        string sourceEventId,
        DateTimeOffset generatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(analytics.TopExpenseCategoryId) ||
            analytics.MonthlyExpenseTotal <= 0m ||
            analytics.TopExpenseCategoryAmount <= 0m)
        {
            return;
        }

        var share = Percentage(analytics.TopExpenseCategoryAmount, analytics.MonthlyExpenseTotal);
        if (share < HighCategorySharePercent)
        {
            return;
        }

        recommendations.Add(Create(
            snapshot,
            sourceEventId,
            generatedAtUtc,
            "high-spending-category",
            RecommendationSeverities.Warning,
            "One category drives much of this month's spending",
            "Review the largest confirmed expense category for optional costs.",
            new RecommendationFact("top-category-expense", analytics.TopExpenseCategoryAmount),
            new RecommendationFact("top-category-share-percent", share)));
    }

    private static void AddBudgetRecommendation(
        ICollection<FinancialRecommendation> recommendations,
        InsightSnapshot snapshot,
        AnalyticsInsightFacts analytics,
        string sourceEventId,
        DateTimeOffset generatedAtUtc)
    {
        if (snapshot.Profile?.MonthlyBudgetLimit is not > 0m)
        {
            return;
        }

        var usage = Percentage(
            analytics.MonthlyExpenseTotal,
            snapshot.Profile.MonthlyBudgetLimit.Value);
        if (usage < BudgetNearLimitPercent)
        {
            return;
        }

        recommendations.Add(Create(
            snapshot,
            sourceEventId,
            generatedAtUtc,
            "monthly-budget-nearing-limit",
            usage >= 100m
                ? RecommendationSeverities.Critical
                : RecommendationSeverities.Warning,
            usage >= 100m
                ? "Monthly spending reached your budget"
                : "Monthly spending is nearing your budget",
            "Review confirmed expenses and protect the remaining budget for essentials.",
            new RecommendationFact("monthly-budget-limit", snapshot.Profile.MonthlyBudgetLimit.Value),
            new RecommendationFact("monthly-budget-used-percent", usage)));
    }

    private static void AddMissingIncomeRecommendation(
        ICollection<FinancialRecommendation> recommendations,
        InsightSnapshot snapshot,
        AnalyticsInsightFacts analytics,
        string sourceEventId,
        DateTimeOffset generatedAtUtc)
    {
        if (analytics.MonthlyIncomeTotal != 0m || analytics.MonthlyExpenseTotal <= 0m)
        {
            return;
        }

        recommendations.Add(Create(
            snapshot,
            sourceEventId,
            generatedAtUtc,
            "missing-income",
            RecommendationSeverities.Warning,
            "No confirmed income is recorded this month",
            "Confirm or add income so budget and cash-flow guidance use a complete picture.",
            new RecommendationFact("monthly-expense-total", analytics.MonthlyExpenseTotal)));
    }

    private static void AddIncompleteProfileRecommendation(
        ICollection<FinancialRecommendation> recommendations,
        InsightSnapshot snapshot,
        string sourceEventId,
        DateTimeOffset generatedAtUtc)
    {
        if (snapshot.Profile is not { IsAvailable: true, IsComplete: false })
        {
            return;
        }

        recommendations.Add(Create(
            snapshot,
            sourceEventId,
            generatedAtUtc,
            "incomplete-profile",
            RecommendationSeverities.Information,
            "Complete your financial profile",
            "Complete missing profile settings to improve deterministic budgets and guidance.",
            Array.Empty<RecommendationFact>()));
    }

    private static void AddUncategorizedRecommendation(
        ICollection<FinancialRecommendation> recommendations,
        InsightSnapshot snapshot,
        AnalyticsInsightFacts analytics,
        string sourceEventId,
        DateTimeOffset generatedAtUtc)
    {
        if (analytics.UncategorizedExpenseTotal <= 0m)
        {
            return;
        }

        recommendations.Add(Create(
            snapshot,
            sourceEventId,
            generatedAtUtc,
            "uncategorized-expenses",
            RecommendationSeverities.Information,
            "Review uncategorized expenses",
            "Categorize confirmed expenses to improve category trends and recommendations.",
            new RecommendationFact("uncategorized-expense-total", analytics.UncategorizedExpenseTotal)));
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

        var ratio = Percentage(analytics.MonthlyExpenseTotal, analytics.MonthlyIncomeTotal);
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

    private static void AddPositiveProgressRecommendation(
        ICollection<FinancialRecommendation> recommendations,
        InsightSnapshot snapshot,
        AnalyticsInsightFacts analytics,
        string sourceEventId,
        DateTimeOffset generatedAtUtc)
    {
        if (snapshot.Profile is not
            {
                IsAvailable: true,
                IsComplete: true,
                MonthlyBudgetLimit: > 0m
            } profile ||
            analytics.MonthlyIncomeTotal <= analytics.MonthlyExpenseTotal ||
            analytics.MonthlyExpenseTotal <= 0m)
        {
            return;
        }

        var usage = Percentage(analytics.MonthlyExpenseTotal, profile.MonthlyBudgetLimit.Value);
        if (usage > PositiveBudgetMaximumPercent)
        {
            return;
        }

        recommendations.Add(Create(
            snapshot,
            sourceEventId,
            generatedAtUtc,
            "positive-budget-progress",
            RecommendationSeverities.Information,
            "Your budget progress is on track",
            "Keep confirming transactions and maintain the current spending pattern.",
            new RecommendationFact("monthly-budget-used-percent", usage)));
    }

    private static decimal Percentage(decimal numerator, decimal denominator) =>
        Math.Round(numerator / denominator * 100m, 2, MidpointRounding.AwayFromZero);

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
