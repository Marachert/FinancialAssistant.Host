namespace FinancialAssistant.FinancialSummary.Contracts;

public static class FinancialSummaryApiRoutes
{
    public const string Summary = "/api/v1/financial-summary";
    public const string GatewaySummary = "/financial-summary";
}

public sealed record FinancialSummaryQuery(
    string Currency,
    string TimeZoneId,
    DateOnly? ReferenceDate = null);

public sealed record FinancialSummaryPeriodResponse(
    string Period,
    DateOnly From,
    DateOnly To,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    decimal BalanceDelta);

public sealed record FinancialSummaryCategoryResponse(
    string CategoryId,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    decimal BalanceDelta);

public sealed record FinancialSummaryFreshnessResponse(
    bool IsStale,
    DateTimeOffset? LastEventAtUtc);

public sealed record FinancialSummaryResponse(
    string Currency,
    string TimeZoneId,
    DateOnly ReferenceDate,
    FinancialSummaryPeriodResponse Daily,
    FinancialSummaryPeriodResponse Weekly,
    FinancialSummaryPeriodResponse Monthly,
    decimal BalanceDelta,
    IReadOnlyList<FinancialSummaryCategoryResponse> CategoryBreakdown,
    FinancialSummaryFreshnessResponse Freshness);
