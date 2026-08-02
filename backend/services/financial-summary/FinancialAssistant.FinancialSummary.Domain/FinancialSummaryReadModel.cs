namespace FinancialAssistant.FinancialSummary.Domain;

public static class FinancialRecordTypes
{
    public const string Income = "income";
    public const string Expense = "expense";
}

public static class FinancialRecordProjectionStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";
}

public sealed record FinancialRecordProjection(
    string RecordType,
    string RecordId,
    string UserIdHash,
    decimal Amount,
    string Currency,
    string CategoryId,
    DateOnly Date,
    string Status,
    long Revision,
    string Origin,
    DateTimeOffset ChangedAtUtc,
    string EventId);

public sealed record FinancialPeriodTotals(
    DateOnly From,
    DateOnly To,
    decimal Income,
    decimal Expense)
{
    public decimal BalanceDelta => Income - Expense;
}

public sealed record FinancialCategoryTotals(
    string CategoryId,
    decimal Income,
    decimal Expense)
{
    public decimal BalanceDelta => Income - Expense;
}

public sealed record FinancialSummaryReadModel(
    string UserIdHash,
    string Currency,
    DateOnly ReferenceDate,
    FinancialPeriodTotals Daily,
    FinancialPeriodTotals Weekly,
    FinancialPeriodTotals Monthly,
    decimal BalanceDelta,
    IReadOnlyList<FinancialCategoryTotals> CategoryBreakdown,
    DateTimeOffset? LastEventAtUtc,
    bool IsStale);
