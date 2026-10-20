using System.Text.Json.Serialization;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Analytics.Infrastructure.Persistence;

public sealed record AnalyticsLimitSnapshot(string OwnerHash, string Currency, string SourceSnapshotVersion,
    long SourceRevision, AnalyticsExpenseLimits Limits, DateTimeOffset ObservedAtUtc);

public sealed class PostgresAnalyticsLimitProvider(AnalyticsPersistenceContext context, TimeProvider timeProvider) :
    IAnalyticsDailyLimitProvider, IAnalyticsLimitProvider
{
    public static TimeSpan MaximumSnapshotAge { get; } = TimeSpan.FromHours(2);

    public async Task<decimal?> GetDailyExpenseLimitAsync(string userIdHash, string currency, DateOnly referenceDate, CancellationToken cancellationToken) =>
        (await GetExpenseLimitsAsync(userIdHash, currency, referenceDate, cancellationToken)).Daily;

    public Task<AnalyticsExpenseLimits> GetExpenseLimitsAsync(string userIdHash, string currency, DateOnly referenceDate, CancellationToken cancellationToken)
    {
        AnalyticsProjectionCodec.Owner(userIdHash); AnalyticsProjectionCodec.Currency(currency);
        if (referenceDate == default) { throw new ArgumentException("An Analytics reference date is required."); }
        return context.WithRecordsAsync(userIdHash, async (records, _) =>
        {
            var durable = await records.GetAsync("limit_snapshots", userIdHash, currency) ?? throw new StorageUnavailableException();
            var snapshot = Decode(durable, userIdHash, currency);
            var now = timeProvider.GetUtcNow();
            if (snapshot.ObservedAtUtc > now || now - snapshot.ObservedAtUtc > MaximumSnapshotAge)
            { throw new StorageUnavailableException(); }
            return snapshot.Limits;
        }, cancellationToken);
    }

    // Only an authorized settings-source adapter may supply this materialized input; this is not a public setter.
    public Task<bool> ApplySnapshotAsync(AnalyticsLimitSnapshot snapshot, CancellationToken token = default)
    {
        Validate(snapshot);
        if (snapshot.ObservedAtUtc > timeProvider.GetUtcNow()) { throw new ArgumentException("Analytics settings observation cannot be in the future."); }
        return context.WithRecordsAsync(snapshot.OwnerHash, async (records, _) =>
        {
            var durable = await records.GetAsync("limit_snapshots", snapshot.OwnerHash, snapshot.Currency);
            if (durable is not null)
            {
                var current = Decode(durable, snapshot.OwnerHash, snapshot.Currency);
                if (snapshot.SourceRevision < current.SourceRevision) { return false; }
                if (snapshot.SourceRevision == current.SourceRevision)
                {
                    if (snapshot.SourceSnapshotVersion != current.SourceSnapshotVersion || snapshot.Limits != current.Limits)
                    { throw new StorageConflictException(); }
                    if (snapshot.ObservedAtUtc <= current.ObservedAtUtc) { return false; }
                }
                if (snapshot.ObservedAtUtc < current.ObservedAtUtc) { throw new StorageConflictException(); }
                await records.ReplaceAsync("limit_snapshots", snapshot.OwnerHash, snapshot.Currency, durable.Revision, Encode(snapshot));
            }
            else { await records.InsertAsync("limit_snapshots", snapshot.OwnerHash, snapshot.Currency, Encode(snapshot)); }
            return true;
        }, token);
    }

    public static void Validate(AnalyticsLimitSnapshot value)
    {
        ArgumentNullException.ThrowIfNull(value);
        AnalyticsProjectionCodec.Owner(value.OwnerHash); AnalyticsProjectionCodec.Currency(value.Currency);
        if (string.IsNullOrWhiteSpace(value.SourceSnapshotVersion) || value.SourceSnapshotVersion.Length > 128 ||
            value.SourceSnapshotVersion != value.SourceSnapshotVersion.Trim() || value.SourceSnapshotVersion.Any(char.IsControl) ||
            value.SourceRevision is < 0 or long.MaxValue || value.Limits is null ||
            value.Limits.Daily is <= 0 || value.Limits.Weekly is <= 0 || value.Limits.Monthly is <= 0 ||
            value.ObservedAtUtc == default || value.ObservedAtUtc.Offset != TimeSpan.Zero)
        { throw new ArgumentException("Invalid authorized Analytics settings snapshot."); }
    }

    private static byte[] Encode(AnalyticsLimitSnapshot value) => OwnedRecordCodec.Encode(1,
        new State(value.OwnerHash, value.Currency, value.SourceSnapshotVersion, value.SourceRevision,
            value.Limits.Daily, value.Limits.Weekly, value.Limits.Monthly, value.ObservedAtUtc));

    private static AnalyticsLimitSnapshot Decode(DurableRecord durable, string owner, string currency)
    {
        try
        {
            var state = OwnedRecordCodec.Decode<State>(1, durable);
            var value = new AnalyticsLimitSnapshot(state.OwnerHash, state.Currency, state.SourceSnapshotVersion,
                state.SourceRevision, new AnalyticsExpenseLimits(state.Daily, state.Weekly, state.Monthly), state.ObservedAtUtc);
            Validate(value);
            if (value.OwnerHash != owner || value.Currency != currency) { throw new StorageUnavailableException(); }
            return value;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private sealed record State([property: JsonRequired] string OwnerHash, [property: JsonRequired] string Currency,
        [property: JsonRequired] string SourceSnapshotVersion, [property: JsonRequired] long SourceRevision,
        [property: JsonRequired] decimal? Daily, [property: JsonRequired] decimal? Weekly,
        [property: JsonRequired] decimal? Monthly, [property: JsonRequired] DateTimeOffset ObservedAtUtc);
}
