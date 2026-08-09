namespace FinancialAssistant.RecommendationsNotifications.Domain;

public static class RecommendationExplanationConfidences
{
    public const string High = "high";
    public const string Baseline = "baseline";

    public static bool IsKnown(string value) =>
        value is High or Baseline;
}

public sealed record RecommendationActionLink(
    string Code,
    string Route);

public sealed record RecommendationExplanationInput(
    string RecommendationCode,
    IReadOnlyList<RecommendationFact> Facts,
    string LocalizationKey,
    string FallbackText,
    RecommendationActionLink Action,
    string Confidence);

public sealed record RecommendationExplanation(
    string LocalizationKey,
    string Text,
    string Confidence,
    RecommendationActionLink Action,
    bool IsWordingEnhanced);

public static class RecommendationExplanationCatalog
{
    public static RecommendationExplanationInput CreateInput(
        FinancialRecommendation recommendation)
    {
        ArgumentNullException.ThrowIfNull(recommendation);

        var (fallbackText, actionCode, actionRoute) = recommendation.Code switch
        {
            "daily-limit-reached" => (
                "This recommendation is based on confirmed daily spending and your configured daily limit.",
                "review-expenses",
                "/transactions"),
            "high-spending-category" => (
                "This recommendation is based on the share of confirmed monthly expenses in your largest category.",
                "review-categories",
                "/reports/categories"),
            "monthly-budget-nearing-limit" => (
                "This recommendation compares confirmed monthly spending with your configured monthly budget.",
                "review-limits",
                "/settings/limits"),
            "missing-income" => (
                "This recommendation appears because confirmed expenses exist without confirmed income for the month.",
                "add-income",
                "/transactions/new?type=income"),
            "incomplete-profile" => (
                "This recommendation appears because required financial profile settings are incomplete.",
                "complete-profile",
                "/settings/profile"),
            "uncategorized-expenses" => (
                "This recommendation is based on confirmed expenses that do not yet have a category.",
                "categorize-expenses",
                "/transactions?category=uncategorized"),
            "negative-cash-flow" or "spending-pressure" => (
                "This recommendation compares confirmed monthly expenses with confirmed monthly income.",
                "review-cash-flow",
                "/reports/monthly"),
            "score-recovery" or "score-strength" => (
                "This recommendation is based on the current deterministic financial score.",
                "review-score",
                "/score"),
            "positive-budget-progress" => (
                "This recommendation is based on confirmed spending remaining within the configured monthly budget.",
                "view-progress",
                "/dashboard"),
            _ => (
                "This recommendation is based on confirmed financial data and deterministic product rules.",
                "view-dashboard",
                "/dashboard")
        };

        return new RecommendationExplanationInput(
            recommendation.Code,
            recommendation.Facts.ToArray(),
            $"recommendations.{recommendation.Code}.explanation",
            fallbackText,
            new RecommendationActionLink(actionCode, actionRoute),
            recommendation.Facts.Count > 0
                ? RecommendationExplanationConfidences.High
                : RecommendationExplanationConfidences.Baseline);
    }

    public static RecommendationExplanation CreateFallback(
        FinancialRecommendation recommendation)
    {
        var input = CreateInput(recommendation);
        return new RecommendationExplanation(
            input.LocalizationKey,
            input.FallbackText,
            input.Confidence,
            input.Action,
            false);
    }
}
