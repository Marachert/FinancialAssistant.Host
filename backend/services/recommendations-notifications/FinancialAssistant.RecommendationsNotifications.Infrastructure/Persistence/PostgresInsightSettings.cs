using System.Text.Json.Serialization;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;

public sealed record RecommendationProfileSnapshot(string Owner, string Currency, string SourceVersion,
    long SourceRevision, bool Complete, decimal? MonthlyBudget, DateTimeOffset ObservedAtUtc);

public sealed class PostgresRecommendationProfileSettingsProvider(InsightPersistenceContext context, TimeProvider timeProvider) : IRecommendationProfileSettingsProvider
{
    public static TimeSpan MaximumAge { get; } = TimeSpan.FromHours(2);

    public Task<RecommendationProfileSettings> GetAsync(string userIdHash, string currency, CancellationToken cancellationToken)
    {
        InsightPersistenceValidation.Currency(currency);
        return context.WithRecordsAsync(userIdHash, async (records, _) =>
        {
            var durable = await records.GetAsync("profile_snapshots", userIdHash, currency) ?? throw new StorageUnavailableException();
            var snapshot = Decode(durable, userIdHash, currency);
            var now = timeProvider.GetUtcNow();
            if (snapshot.ObservedAtUtc > now || now - snapshot.ObservedAtUtc > MaximumAge) { throw new StorageUnavailableException(); }
            var settings = new RecommendationProfileSettings(true, snapshot.Complete, snapshot.MonthlyBudget);
            if (context.HasSource) { context.Profile = settings; }
            return settings;
        }, cancellationToken);
    }

    // Authorized materialized input only; no Profile SQL, public setter or external call under the transaction.
    public Task<bool> ApplySnapshotAsync(RecommendationProfileSnapshot snapshot, CancellationToken token = default)
    {
        Validate(snapshot);
        if (context.HasSource || snapshot.ObservedAtUtc > timeProvider.GetUtcNow()) { throw new ArgumentException("Invalid recommendation Profile ingestion context/time."); }
        return context.WithRecordsAsync(snapshot.Owner, async (records, _) =>
        {
            var previous = await records.GetAsync("profile_snapshots", snapshot.Owner, snapshot.Currency);
            if (previous is not null)
            {
                var current = Decode(previous, snapshot.Owner, snapshot.Currency);
                if (snapshot.SourceRevision < current.SourceRevision) { return false; }
                if (snapshot.SourceRevision == current.SourceRevision)
                {
                    if (snapshot.SourceVersion != current.SourceVersion || snapshot.Complete != current.Complete || snapshot.MonthlyBudget != current.MonthlyBudget)
                    { throw new StorageConflictException(); }
                    if (snapshot.ObservedAtUtc <= current.ObservedAtUtc) { return false; }
                }
                if (snapshot.ObservedAtUtc < current.ObservedAtUtc) { throw new StorageConflictException(); }
            }
            var bytes = Encode(snapshot);
            return previous is null ? await records.InsertAsync("profile_snapshots", snapshot.Owner, snapshot.Currency, bytes) :
                await records.ReplaceAsync("profile_snapshots", snapshot.Owner, snapshot.Currency, previous.Revision, bytes);
        }, token);
    }

    public static void Validate(RecommendationProfileSnapshot s)
    {
        ArgumentNullException.ThrowIfNull(s); InsightPersistenceValidation.Owner(s.Owner); InsightPersistenceValidation.Currency(s.Currency);
        InsightPersistenceValidation.Id(s.SourceVersion); InsightPersistenceValidation.Utc(s.ObservedAtUtc);
        if (s.SourceRevision is < 0 or long.MaxValue || s.MonthlyBudget is <= 0) { throw new ArgumentException("Invalid authorized recommendation Profile snapshot."); }
    }
    private static byte[] Encode(RecommendationProfileSnapshot s) => OwnedRecordCodec.Encode(1,
        new State(s.Owner, s.Currency, s.SourceVersion, s.SourceRevision, s.Complete, s.MonthlyBudget, s.ObservedAtUtc));
    private static RecommendationProfileSnapshot Decode(DurableRecord record, string owner, string currency)
    {
        try
        {
            var s = OwnedRecordCodec.Decode<State>(1, record);
            var result = new RecommendationProfileSnapshot(s.Owner, s.Currency, s.Version, s.Revision, s.Complete, s.Budget, s.ObservedAtUtc);
            Validate(result);
            if (s.Owner != owner || s.Currency != currency) { throw new StorageUnavailableException(); }
            return result;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }
    private sealed record State([property: JsonRequired] string Owner, [property: JsonRequired] string Currency,
        [property: JsonRequired] string Version, [property: JsonRequired] long Revision, [property: JsonRequired] bool Complete,
        [property: JsonRequired] decimal? Budget, [property: JsonRequired] DateTimeOffset ObservedAtUtc);
}

public sealed class PostgresNotificationPreferenceProvider(InsightPersistenceContext context) : INotificationPreferenceProvider
{
    public Task<NotificationPreferences> GetAsync(string userIdHash, CancellationToken cancellationToken) =>
        context.ReadAsync(userIdHash, state => state.Preferences?.ToDomain() ?? throw new StorageUnavailableException(), cancellationToken);

    public Task<NotificationPreferences> UpdateAsync(string userIdHash, NotificationPreferences preferences, CancellationToken cancellationToken)
    {
        InsightPersistenceValidation.Preferences(preferences);
        return context.MutateAsync(userIdHash, _ =>
        {
            context.SetPreferences(InsightPreferenceState.From(preferences));
            return Task.FromResult(context.State(userIdHash).Preferences!.ToDomain());
        }, cancellationToken);
    }
}
