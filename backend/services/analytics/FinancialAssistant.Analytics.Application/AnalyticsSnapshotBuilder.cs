using FinancialAssistant.Analytics.Domain;

namespace FinancialAssistant.Analytics.Application;

public static class AnalyticsSnapshotBuilder
{
    public static AnalyticsProjectionSnapshot Build(string userIdHash, string currency,
        IEnumerable<AnalyticsRecordProjection> projections)
    {
        var normalizedCurrency = currency.ToUpperInvariant();
        var records = projections.Where(item => item.UserIdHash == userIdHash && item.Currency == normalizedCurrency).ToArray();
        var active = records.Where(item => item.Status == AnalyticsProjectionStatuses.Active).ToArray();
        return new AnalyticsProjectionSnapshot(userIdHash, normalizedCurrency,
            active.GroupBy(item => item.Date).ToDictionary(group => group.Key, group => BuildTotals(group)),
            active.GroupBy(item => StartOfWeek(item.Date)).ToDictionary(group => group.Key, group => BuildTotals(group)),
            active.GroupBy(item => item.Date).ToDictionary(group => group.Key, group => BuildCategoryTotals(group)),
            active.GroupBy(item => StartOfWeek(item.Date)).ToDictionary(group => group.Key, group => BuildCategoryTotals(group)),
            active.GroupBy(item => new DateOnly(item.Date.Year, item.Date.Month, 1)).ToDictionary(group => group.Key,
                group => new AnalyticsMonthlyAggregate(group.Key, BuildTotals(group), BuildCategoryTotals(group))),
            records.Length == 0 ? null : records.Max(item => item.ChangedAtUtc));
    }

    private static AnalyticsAggregateTotals BuildTotals(IEnumerable<AnalyticsRecordProjection> records) =>
        new(Sum(records, AnalyticsRecordTypes.Income), Sum(records, AnalyticsRecordTypes.Expense));

    private static IReadOnlyList<AnalyticsCategoryTotal> BuildCategoryTotals(IEnumerable<AnalyticsRecordProjection> records) =>
        records.GroupBy(item => item.CategoryId, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new AnalyticsCategoryTotal(group.Key, Sum(group, AnalyticsRecordTypes.Income), Sum(group, AnalyticsRecordTypes.Expense))).ToArray();

    private static decimal Sum(IEnumerable<AnalyticsRecordProjection> records, string recordType) =>
        records.Where(item => item.RecordType == recordType).Sum(item => item.Amount);

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
