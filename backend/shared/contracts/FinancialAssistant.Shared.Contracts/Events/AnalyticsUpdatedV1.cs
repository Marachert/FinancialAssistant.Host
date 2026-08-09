namespace FinancialAssistant.Shared.Contracts.Events;

public static class AnalyticsEventTypes
{
    public const int SchemaVersion = 1;
    public const string AnalyticsUpdated = "analytics.updated.v1";
}

public sealed record AnalyticsUpdatedV1(
    string Currency,
    DateOnly ReferenceDate,
    decimal MonthlyIncomeTotal,
    decimal MonthlyExpenseTotal,
    decimal? DailyExpenseLimit,
    decimal DailyExpenseSpent,
    string? TopExpenseCategoryId,
    DateTimeOffset UpdatedAtUtc,
    decimal TopExpenseCategoryAmount = 0m,
    decimal UncategorizedExpenseTotal = 0m);
