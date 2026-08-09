namespace FinancialAssistant.Analytics.Domain;

public static class AnalyticsRecordTypes
{
    public const string Income = "income";
    public const string Expense = "expense";
}

public static class AnalyticsCategoryIds
{
    public const string Uncategorized = "uncategorized";
}

public static class AnalyticsProjectionStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";
}

public sealed record AnalyticsRecordProjection(
    string RecordType,
    string RecordId,
    string UserIdHash,
    decimal Amount,
    string Currency,
    string CategoryId,
    DateOnly Date,
    string Status,
    long Revision,
    DateTimeOffset ChangedAtUtc,
    string EventId);

public sealed record AnalyticsProjectionWriteOutcome(
    bool Accepted,
    IReadOnlyList<string> PendingPublicationCurrencies);

public sealed record AnalyticsAggregateTotals(decimal Income, decimal Expense)
{
    public decimal BalanceDelta => Income - Expense;
}

public sealed record AnalyticsPeriodSummary(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Income,
    decimal Expense)
{
    public decimal BalanceDelta => Income - Expense;
}

public sealed record AnalyticsMonthlyAggregate(
    DateOnly MonthStart,
    AnalyticsAggregateTotals Totals,
    IReadOnlyList<AnalyticsCategoryTotal> CategoryTotals);

public sealed record AnalyticsProjectionSnapshot(
    string UserIdHash,
    string Currency,
    IReadOnlyDictionary<DateOnly, AnalyticsAggregateTotals> DailyTotals,
    IReadOnlyDictionary<DateOnly, AnalyticsAggregateTotals> WeeklyTotals,
    IReadOnlyDictionary<DateOnly, IReadOnlyList<AnalyticsCategoryTotal>> DailyCategoryTotals,
    IReadOnlyDictionary<DateOnly, IReadOnlyList<AnalyticsCategoryTotal>> WeeklyCategoryTotals,
    IReadOnlyDictionary<DateOnly, AnalyticsMonthlyAggregate> MonthlyTotals,
    DateTimeOffset? LastEventAtUtc);

public sealed record AnalyticsDailyLimit(
    bool IsConfigured,
    decimal? Limit,
    decimal Spent,
    decimal? Remaining,
    decimal? UsedPercent);

public sealed record AnalyticsMonthlyProgress(
    decimal Income,
    decimal Expense,
    decimal BalanceDelta,
    decimal? ExpenseToIncomePercent);

public sealed record AnalyticsLimitProgress(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    bool IsConfigured,
    decimal? Limit,
    decimal Spent,
    decimal? Remaining,
    decimal? UsedPercent);

public sealed record AnalyticsTrackingStreak(
    int CurrentDays,
    DateOnly? LastTrackedDate,
    string Message);

public sealed record AnalyticsLimitsProgress(
    AnalyticsLimitProgress Daily,
    AnalyticsLimitProgress Weekly,
    AnalyticsLimitProgress Monthly,
    AnalyticsTrackingStreak TrackingStreak);

public sealed record AnalyticsCategoryTotal(
    string CategoryId,
    decimal Income,
    decimal Expense)
{
    public decimal BalanceDelta => Income - Expense;
}

public sealed record AnalyticsCategoryBreakdownItem(
    string CategoryId,
    decimal Income,
    decimal Expense,
    decimal IncomeSharePercent,
    decimal ExpenseSharePercent)
{
    public decimal BalanceDelta => Income - Expense;
}

public sealed record AnalyticsCategoryBreakdownReadModel(
    string Currency,
    DateOnly ReferenceDate,
    string Period,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<AnalyticsCategoryBreakdownItem> Categories,
    IReadOnlyList<AnalyticsCategoryBreakdownItem> TopIncomeCategories,
    IReadOnlyList<AnalyticsCategoryBreakdownItem> TopExpenseCategories,
    DateTimeOffset? LastEventAtUtc,
    bool IsStale);

public sealed record AnalyticsTrendPoint(
    DateOnly Date,
    decimal Income,
    decimal Expense)
{
    public decimal BalanceDelta => Income - Expense;
}

public sealed record AnalyticsDashboardReadModel(
    string UserIdHash,
    string Currency,
    DateOnly ReferenceDate,
    AnalyticsPeriodSummary DailySummary,
    AnalyticsPeriodSummary WeeklySummary,
    AnalyticsPeriodSummary MonthlySummary,
    AnalyticsDailyLimit DailyLimit,
    AnalyticsLimitsProgress LimitsProgress,
    AnalyticsMonthlyProgress MonthlyProgress,
    IReadOnlyList<AnalyticsCategoryTotal> CategoryTotals,
    IReadOnlyList<AnalyticsTrendPoint> RecentTrend,
    DateTimeOffset? LastEventAtUtc,
    bool IsStale);
