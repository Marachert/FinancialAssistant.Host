namespace FinancialAssistant.Analytics.Contracts;

public static class DashboardContractVersions
{
    public const string V1 = "1";
}

public sealed record DashboardPeriodWidgetResponse(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    decimal BalanceDelta);

public sealed record DashboardSummaryWidgetResponse(
    DashboardPeriodWidgetResponse Daily,
    DashboardPeriodWidgetResponse Weekly,
    DashboardPeriodWidgetResponse Monthly);

public sealed record DashboardCategoryItemResponse(
    string CategoryId,
    decimal ExpenseTotal,
    decimal ExpenseSharePercent);

public sealed record DashboardCategoryWidgetResponse(
    IReadOnlyList<DashboardCategoryItemResponse> TopExpenseCategories,
    bool HasMore);

public sealed record DashboardScoreWidgetResponse(
    bool IsAvailable,
    int? Score,
    string? FormulaVersion,
    DateTimeOffset? CalculatedAtUtc);

public sealed record DashboardLimitWidgetItemResponse(
    bool IsConfigured,
    decimal? Limit,
    decimal Spent,
    decimal? Remaining,
    decimal? UsedPercent);

public sealed record DashboardLimitsWidgetResponse(
    DashboardLimitWidgetItemResponse Daily,
    DashboardLimitWidgetItemResponse Weekly,
    DashboardLimitWidgetItemResponse Monthly,
    int TrackingStreakDays,
    string TrackingMessage);

public sealed record DashboardRecommendationPreviewItemResponse(
    string RecommendationId,
    string Code,
    string Severity,
    string Title,
    string Body);

public sealed record DashboardRecommendationWidgetResponse(
    IReadOnlyList<DashboardRecommendationPreviewItemResponse> Items,
    bool HasMore);

public sealed record DashboardNotificationBadgeResponse(
    int UnreadCount,
    bool HasUnread);

public sealed record DashboardEmptyStateResponse(
    bool HasFinancialData,
    bool HasCategoryData,
    bool HasScore,
    bool HasRecommendations,
    bool HasNotifications);

public sealed record DashboardCompositionResponse(
    string SchemaVersion,
    string Currency,
    string TimeZoneId,
    DateOnly ReferenceDate,
    DateTimeOffset GeneratedAtUtc,
    DashboardSummaryWidgetResponse Summary,
    DashboardCategoryWidgetResponse Categories,
    DashboardScoreWidgetResponse Score,
    DashboardLimitsWidgetResponse Limits,
    DashboardRecommendationWidgetResponse Recommendations,
    DashboardNotificationBadgeResponse Notifications,
    DashboardEmptyStateResponse EmptyState,
    AnalyticsFreshnessResponse AnalyticsFreshness);
