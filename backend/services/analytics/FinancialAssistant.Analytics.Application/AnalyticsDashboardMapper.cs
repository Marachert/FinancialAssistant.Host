using FinancialAssistant.Analytics.Contracts;
using FinancialAssistant.Analytics.Domain;

namespace FinancialAssistant.Analytics.Application;

public static class AnalyticsDashboardMapper
{
    public static AnalyticsDashboardResponse Map(
        AnalyticsDashboardReadModel readModel,
        string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new ArgumentException("Time zone is required.", nameof(timeZoneId));
        }

        return new AnalyticsDashboardResponse(
            readModel.Currency,
            timeZoneId.Trim(),
            readModel.ReferenceDate,
            MapPeriodSummary(readModel.DailySummary),
            MapPeriodSummary(readModel.WeeklySummary),
            MapPeriodSummary(readModel.MonthlySummary),
            new AnalyticsDailyLimitResponse(
                readModel.DailyLimit.IsConfigured,
                readModel.DailyLimit.Limit,
                readModel.DailyLimit.Spent,
                readModel.DailyLimit.Remaining,
                readModel.DailyLimit.UsedPercent),
            new AnalyticsMonthlyProgressResponse(
                readModel.MonthlyProgress.Income,
                readModel.MonthlyProgress.Expense,
                readModel.MonthlyProgress.BalanceDelta,
                readModel.MonthlyProgress.ExpenseToIncomePercent),
            readModel.CategoryTotals
                .Select(item => new AnalyticsCategoryTotalResponse(
                    item.CategoryId,
                    item.Income,
                    item.Expense,
                    item.BalanceDelta))
                .ToArray(),
            readModel.RecentTrend
                .Select(item => new AnalyticsTrendPointResponse(
                    item.Date,
                    item.Income,
                    item.Expense,
                    item.BalanceDelta))
                .ToArray(),
            new AnalyticsFreshnessResponse(
                readModel.IsStale,
                readModel.LastEventAtUtc));
    }

    public static AnalyticsCategoryBreakdownResponse Map(
        AnalyticsCategoryBreakdownReadModel readModel,
        string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new ArgumentException("Time zone is required.", nameof(timeZoneId));
        }

        return new AnalyticsCategoryBreakdownResponse(
            readModel.Currency,
            timeZoneId.Trim(),
            readModel.ReferenceDate,
            readModel.Period,
            readModel.PeriodStart,
            readModel.PeriodEnd,
            readModel.Categories.Select(MapCategory).ToArray(),
            readModel.TopIncomeCategories.Select(MapCategory).ToArray(),
            readModel.TopExpenseCategories.Select(MapCategory).ToArray(),
            new AnalyticsFreshnessResponse(
                readModel.IsStale,
                readModel.LastEventAtUtc));
    }

    private static AnalyticsCategoryBreakdownItemResponse MapCategory(
        AnalyticsCategoryBreakdownItem item) =>
        new(
            item.CategoryId,
            item.Income,
            item.Expense,
            item.BalanceDelta,
            item.IncomeSharePercent,
            item.ExpenseSharePercent);

    private static AnalyticsPeriodSummaryResponse MapPeriodSummary(
        AnalyticsPeriodSummary summary) =>
        new(
            summary.PeriodStart,
            summary.PeriodEnd,
            summary.Income,
            summary.Expense,
            summary.BalanceDelta);
}
