using FinancialAssistant.FinancialSummary.Contracts;
using FinancialAssistant.FinancialSummary.Domain;

namespace FinancialAssistant.FinancialSummary.Application;

public static class FinancialSummaryResponseMapper
{
    public static FinancialSummaryResponse Map(
        FinancialSummaryReadModel readModel,
        string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new ArgumentException("Time zone is required.", nameof(timeZoneId));
        }

        return new FinancialSummaryResponse(
            readModel.Currency,
            timeZoneId.Trim(),
            readModel.ReferenceDate,
            MapPeriod("daily", readModel.Daily),
            MapPeriod("weekly", readModel.Weekly),
            MapPeriod("monthly", readModel.Monthly),
            readModel.BalanceDelta,
            readModel.CategoryBreakdown
                .Select(category => new FinancialSummaryCategoryResponse(
                    category.CategoryId,
                    category.Income,
                    category.Expense,
                    category.BalanceDelta))
                .ToArray(),
            new FinancialSummaryFreshnessResponse(
                readModel.IsStale,
                readModel.LastEventAtUtc));
    }

    private static FinancialSummaryPeriodResponse MapPeriod(
        string period,
        FinancialPeriodTotals totals) =>
        new(
            period,
            totals.From,
            totals.To,
            totals.Income,
            totals.Expense,
            totals.BalanceDelta);
}
