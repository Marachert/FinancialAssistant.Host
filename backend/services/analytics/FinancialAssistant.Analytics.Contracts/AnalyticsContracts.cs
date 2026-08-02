namespace FinancialAssistant.Analytics.Contracts;

public static class AnalyticsApiRoutes
{
    public const string Dashboard = "/api/v1/analytics/dashboard";
    public const string GatewayDashboard = "/analytics/dashboard";
    public const string CategoryBreakdown = "/api/v1/analytics/category-breakdown";
    public const string GatewayCategoryBreakdown = "/analytics/category-breakdown";
}

public static class AnalyticsBreakdownPeriods
{
    public const string Daily = "daily";
    public const string Weekly = "weekly";
    public const string Monthly = "monthly";
}

public static class AnalyticsGatewayHeaders
{
    public const string Authentication = "X-Gateway-Authentication";
    public const string UserId = "X-Gateway-User-Id";
}

public sealed record AnalyticsPeriodSummaryResponse(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    decimal BalanceDelta);

public sealed record AnalyticsDailyLimitResponse(
    bool IsConfigured,
    decimal? Limit,
    decimal Spent,
    decimal? Remaining,
    decimal? UsedPercent);

public sealed record AnalyticsMonthlyProgressResponse(
    decimal IncomeTotal,
    decimal ExpenseTotal,
    decimal BalanceDelta,
    decimal? ExpenseToIncomePercent);

public sealed record AnalyticsCategoryTotalResponse(
    string CategoryId,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    decimal BalanceDelta);

public sealed record AnalyticsTrendPointResponse(
    DateOnly Date,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    decimal BalanceDelta);

public sealed record AnalyticsFreshnessResponse(
    bool IsStale,
    DateTimeOffset? LastEventAtUtc);

public sealed record AnalyticsDashboardResponse(
    string Currency,
    string TimeZoneId,
    DateOnly ReferenceDate,
    AnalyticsPeriodSummaryResponse DailySummary,
    AnalyticsPeriodSummaryResponse WeeklySummary,
    AnalyticsPeriodSummaryResponse MonthlySummary,
    AnalyticsDailyLimitResponse DailyLimit,
    AnalyticsMonthlyProgressResponse MonthlyProgress,
    IReadOnlyList<AnalyticsCategoryTotalResponse> CategoryTotals,
    IReadOnlyList<AnalyticsTrendPointResponse> RecentTrend,
    AnalyticsFreshnessResponse Freshness);

public sealed record AnalyticsCategoryBreakdownItemResponse(
    string CategoryId,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    decimal BalanceDelta,
    decimal IncomeSharePercent,
    decimal ExpenseSharePercent);

public sealed record AnalyticsCategoryBreakdownResponse(
    string Currency,
    string TimeZoneId,
    DateOnly ReferenceDate,
    string Period,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<AnalyticsCategoryBreakdownItemResponse> Categories,
    IReadOnlyList<AnalyticsCategoryBreakdownItemResponse> TopIncomeCategories,
    IReadOnlyList<AnalyticsCategoryBreakdownItemResponse> TopExpenseCategories);

public sealed record AnalyticsApiErrorResponse(
    string? Title,
    string? Detail,
    int? Status,
    string? Code,
    string? TraceId);

public sealed record AnalyticsServiceInfoResponse(
    string Service,
    string Status,
    string Environment,
    string StorageProvider,
    string ProjectionSource);
