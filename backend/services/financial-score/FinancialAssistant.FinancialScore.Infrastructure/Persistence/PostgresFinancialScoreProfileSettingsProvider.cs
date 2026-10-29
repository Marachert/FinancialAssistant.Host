using System.Text.Json.Serialization;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.FinancialScore.Infrastructure.Persistence;

public sealed record ScoreProfileSnapshot(string Owner, string Currency, string SourceVersion, long SourceRevision,
    FinancialScoreProfileSettings Settings, DateTimeOffset ObservedAtUtc);

public sealed class PostgresFinancialScoreProfileSettingsProvider(ScorePersistenceContext context, TimeProvider timeProvider) : IFinancialScoreProfileSettingsProvider
{
    public static TimeSpan MaximumAge { get; } = TimeSpan.FromHours(2);

    public Task<FinancialScoreProfileSettings> GetAsync(string userIdHash, string currency, CancellationToken cancellationToken)
    {
        ScorePersistenceValidation.Currency(currency);
        return context.WithRecordsAsync(userIdHash, async (records, _) =>
        {
            var durable = await records.GetAsync("profile_snapshots", userIdHash, currency) ?? throw new StorageUnavailableException();
            var snapshot = Decode(durable, userIdHash, currency);
            var now = timeProvider.GetUtcNow();
            if (snapshot.ObservedAtUtc > now || now - snapshot.ObservedAtUtc > MaximumAge) { throw new StorageUnavailableException(); }
            return snapshot.Settings;
        }, cancellationToken);
    }

    // Internal ingestion boundary, not an HTTP setter; FIN-276 supplies authorized Profile API/event input.
    public Task<bool> ApplySnapshotAsync(ScoreProfileSnapshot snapshot, CancellationToken token = default)
    {
        Validate(snapshot);
        if (context.CurrentSource is not null || snapshot.ObservedAtUtc > timeProvider.GetUtcNow()) { throw new ArgumentException("Invalid Score settings ingestion context/time."); }
        return context.WithRecordsAsync(snapshot.Owner, async (records, _) =>
        {
            var previous = await records.GetAsync("profile_snapshots", snapshot.Owner, snapshot.Currency);
            if (previous is not null)
            {
                var current = Decode(previous, snapshot.Owner, snapshot.Currency);
                if (snapshot.SourceRevision < current.SourceRevision) { return false; }
                if (snapshot.SourceRevision == current.SourceRevision)
                {
                    if (snapshot.SourceVersion != current.SourceVersion || snapshot.Settings != current.Settings) { throw new StorageConflictException(); }
                    if (snapshot.ObservedAtUtc <= current.ObservedAtUtc) { return false; }
                }
                if (snapshot.ObservedAtUtc < current.ObservedAtUtc) { throw new StorageConflictException(); }
            }
            var bytes = Encode(snapshot);
            return previous is null ? await records.InsertAsync("profile_snapshots", snapshot.Owner, snapshot.Currency, bytes) :
                await records.ReplaceAsync("profile_snapshots", snapshot.Owner, snapshot.Currency, previous.Revision, bytes);
        }, token);
    }

    public static void Validate(ScoreProfileSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ScorePersistenceValidation.Owner(snapshot.Owner); ScorePersistenceValidation.Currency(snapshot.Currency);
        ScorePersistenceValidation.Identifier(snapshot.SourceVersion); ScorePersistenceValidation.Utc(snapshot.ObservedAtUtc);
        if (snapshot.SourceRevision is < 0 or long.MaxValue || snapshot.Settings is null || snapshot.Settings.MonthlyBudgetAmount < 0)
        { throw new ArgumentException("Invalid authorized Score Profile snapshot."); }
    }

    private static byte[] Encode(ScoreProfileSnapshot s) => OwnedRecordCodec.Encode(1, new State(s.Owner, s.Currency,
        s.SourceVersion, s.SourceRevision, s.Settings.MonthlyBudgetAmount, s.Settings.ProfileOnboardingCompleted,
        s.Settings.PreferencesOnboardingCompleted, s.ObservedAtUtc));

    private static ScoreProfileSnapshot Decode(DurableRecord record, string owner, string currency)
    {
        try
        {
            var s = OwnedRecordCodec.Decode<State>(1, record);
            var result = new ScoreProfileSnapshot(s.Owner, s.Currency, s.Version, s.Revision,
                new FinancialScoreProfileSettings(s.Budget, s.ProfileComplete, s.PreferencesComplete), s.ObservedAtUtc);
            Validate(result);
            if (s.Owner != owner || s.Currency != currency) { throw new StorageUnavailableException(); }
            return result;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private sealed record State([property: JsonRequired] string Owner, [property: JsonRequired] string Currency,
        [property: JsonRequired] string Version, [property: JsonRequired] long Revision,
        [property: JsonRequired] decimal Budget, [property: JsonRequired] bool ProfileComplete,
        [property: JsonRequired] bool PreferencesComplete, [property: JsonRequired] DateTimeOffset ObservedAtUtc);
}
