using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Domain;

namespace FinancialAssistant.Analytics.Infrastructure;

public sealed class InMemoryAnalyticsReadModelStore : IAnalyticsReadModelStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, AnalyticsRecordProjection> projections =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, AnalyticsProjectionSnapshot> snapshots =
        new(StringComparer.Ordinal);

    public Task<AnalyticsProjectionWriteOutcome> UpsertIfNewerAsync(
        AnalyticsRecordProjection projection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projection);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var key = $"{projection.UserIdHash}|{projection.RecordType}|{projection.RecordId}";
            string? previousCurrency = null;
            if (projections.TryGetValue(key, out var current) &&
                current.Revision >= projection.Revision)
            {
                return Task.FromResult(
                    new AnalyticsProjectionWriteOutcome(false, null));
            }

            if (current is not null)
            {
                previousCurrency = current.Currency;
            }

            projections[key] = projection;
            if (previousCurrency is not null &&
                !string.Equals(previousCurrency, projection.Currency, StringComparison.Ordinal))
            {
                RebuildSnapshot(projection.UserIdHash, previousCurrency);
            }

            RebuildSnapshot(projection.UserIdHash, projection.Currency);
            return Task.FromResult(
                new AnalyticsProjectionWriteOutcome(true, previousCurrency));
        }
    }

    public Task<AnalyticsProjectionSnapshot> GetAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var key = CreateSnapshotKey(userIdHash, currency);
            return Task.FromResult(
                snapshots.GetValueOrDefault(key) ??
                new AnalyticsProjectionSnapshot(
                    userIdHash,
                    currency.ToUpperInvariant(),
                    new Dictionary<DateOnly, AnalyticsAggregateTotals>(),
                    new Dictionary<DateOnly, AnalyticsAggregateTotals>(),
                    new Dictionary<DateOnly, AnalyticsMonthlyAggregate>(),
                    LastEventAtUtc: null));
        }
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            projections.Clear();
            snapshots.Clear();
        }

        return Task.CompletedTask;
    }

    private void RebuildSnapshot(string userIdHash, string currency)
    {
        var normalizedCurrency = currency.ToUpperInvariant();
        var records = projections.Values
            .Where(item =>
                item.UserIdHash == userIdHash &&
                item.Currency == normalizedCurrency)
            .ToArray();
        var active = records
            .Where(item => item.Status == AnalyticsProjectionStatuses.Active)
            .ToArray();
        var daily = active
            .GroupBy(item => item.Date)
            .ToDictionary(
                group => group.Key,
                group => BuildTotals(group));
        var weekly = active
            .GroupBy(item => StartOfWeek(item.Date))
            .ToDictionary(
                group => group.Key,
                group => BuildTotals(group));
        var monthly = active
            .GroupBy(item => new DateOnly(item.Date.Year, item.Date.Month, 1))
            .ToDictionary(
                group => group.Key,
                group => new AnalyticsMonthlyAggregate(
                    group.Key,
                    BuildTotals(group),
                    group.GroupBy(item => item.CategoryId, StringComparer.Ordinal)
                        .OrderBy(category => category.Key, StringComparer.Ordinal)
                        .Select(category => new AnalyticsCategoryTotal(
                            category.Key,
                            Sum(category, AnalyticsRecordTypes.Income),
                            Sum(category, AnalyticsRecordTypes.Expense)))
                        .ToArray()));
        snapshots[CreateSnapshotKey(userIdHash, normalizedCurrency)] =
            new AnalyticsProjectionSnapshot(
                userIdHash,
                normalizedCurrency,
                daily,
                weekly,
                monthly,
                records.Length == 0 ? null : records.Max(item => item.ChangedAtUtc));
    }

    private static AnalyticsAggregateTotals BuildTotals(
        IEnumerable<AnalyticsRecordProjection> records) =>
        new(
            Sum(records, AnalyticsRecordTypes.Income),
            Sum(records, AnalyticsRecordTypes.Expense));

    private static decimal Sum(
        IEnumerable<AnalyticsRecordProjection> records,
        string recordType) =>
        records.Where(item => item.RecordType == recordType).Sum(item => item.Amount);

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static string CreateSnapshotKey(string userIdHash, string currency) =>
        $"{userIdHash}|{currency.ToUpperInvariant()}";
}
