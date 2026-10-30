using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.FinancialScore.Infrastructure.Persistence;
using FinancialAssistant.Profile.Client;
using FinancialAssistant.Profile.Contracts;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.FinancialScore.Infrastructure;

public sealed class NativeScoreProfileSettingsLoader(NativeProfileSettingsClient client, PostgresFinancialScoreProfileSettingsProvider profiles)
{
    public async Task RefreshAsync(string owner, IEnumerable<string> currencies, CancellationToken token)
    {
        var normalized = currencies.Select(c => c.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        if (normalized.Length is < 1 or > 8) { throw new ArgumentException("Invalid settings currency selection."); }
        foreach (var currency in normalized) { ScorePersistenceValidation.Currency(currency); }
        ProfileFinancialSettingsSnapshot source;
        try { source = await client.ReadAsync(owner, token); }
        catch (ProfileSettingsUnavailableException) { throw new StorageUnavailableException(); }
        foreach (var currency in normalized) { await profiles.ApplySnapshotAsync(CreateSnapshot(source, currency), token); }
    }

    public static ScoreProfileSnapshot CreateSnapshot(ProfileFinancialSettingsSnapshot source, string currency)
    {
        ScorePersistenceValidation.Currency(currency);
        var budget = source.CurrencyCode == currency ? source.MonthlyBudgetAmount : 0m;
        return new(source.OwnerHash, currency, source.SourceVersion, source.SourceRevision,
            new FinancialScoreProfileSettings(budget, source.ProfileOnboardingCompleted, source.PreferencesOnboardingCompleted), source.ObservedAtUtc);
    }
}
