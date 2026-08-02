namespace FinancialAssistant.Analytics.Contracts;

public static class AnalyticsApiRoutes
{
    public const string Dashboard = "/api/v1/analytics/dashboard";
    public const string GatewayDashboard = "/analytics/dashboard";
}

public static class AnalyticsGatewayHeaders
{
    public const string Authentication = "X-Gateway-Authentication";
    public const string UserId = "X-Gateway-User-Id";
}

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
    AnalyticsDailyLimitResponse DailyLimit,
    AnalyticsMonthlyProgressResponse MonthlyProgress,
    IReadOnlyList<AnalyticsCategoryTotalResponse> CategoryTotals,
    IReadOnlyList<AnalyticsTrendPointResponse> RecentTrend,
    AnalyticsFreshnessResponse Freshness);

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
