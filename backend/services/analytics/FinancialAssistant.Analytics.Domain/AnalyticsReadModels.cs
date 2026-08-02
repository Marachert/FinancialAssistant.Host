namespace FinancialAssistant.Analytics.Domain;

public static class AnalyticsRecordTypes
{
    public const string Income = "income";
    public const string Expense = "expense";
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

public sealed record AnalyticsMonthlyAggregate(
    DateOnly MonthStart,
    AnalyticsAggregateTotals Totals,
    IReadOnlyList<AnalyticsCategoryTotal> CategoryTotals);

public sealed record AnalyticsProjectionSnapshot(
    string UserIdHash,
    string Currency,
    IReadOnlyDictionary<DateOnly, AnalyticsAggregateTotals> DailyTotals,
    IReadOnlyDictionary<DateOnly, AnalyticsAggregateTotals> WeeklyTotals,
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

public sealed record AnalyticsCategoryTotal(
    string CategoryId,
    decimal Income,
    decimal Expense)
{
    public decimal BalanceDelta => Income - Expense;
}

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
    AnalyticsDailyLimit DailyLimit,
    AnalyticsMonthlyProgress MonthlyProgress,
    IReadOnlyList<AnalyticsCategoryTotal> CategoryTotals,
    IReadOnlyList<AnalyticsTrendPoint> RecentTrend,
    DateTimeOffset? LastEventAtUtc,
    bool IsStale);
