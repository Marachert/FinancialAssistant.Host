using FinancialAssistant.Profile.Client;
using FinancialAssistant.Profile.Contracts;
using FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed class NativeInsightProfileSettingsLoader(NativeProfileSettingsClient client,
    PostgresRecommendationProfileSettingsProvider profile, PostgresNotificationPreferenceProvider preferences)
{
    public async Task RefreshAsync(string owner, string? currency, CancellationToken token)
    {
        var selected = currency?.Trim().ToUpperInvariant();
        if (selected is not null) { InsightPersistenceValidation.Currency(selected); }
        ProfileFinancialSettingsSnapshot source;
        try { source = await client.ReadAsync(owner, token); }
        catch (ProfileSettingsUnavailableException) { throw new StorageUnavailableException(); }
        await profile.ApplySnapshotAsync(CreateSnapshot(source, selected ?? source.CurrencyCode), token);
        await preferences.EnsureDisabledIfMissingAsync(owner, token);
    }

    public static RecommendationProfileSnapshot CreateSnapshot(ProfileFinancialSettingsSnapshot source, string currency)
    {
        InsightPersistenceValidation.Currency(currency);
        decimal? budget = source.CurrencyCode == currency && source.MonthlyBudgetAmount > 0 ? source.MonthlyBudgetAmount : null;
        return new(source.OwnerHash, currency, source.SourceVersion, source.SourceRevision,
            source.ProfileOnboardingCompleted && source.PreferencesOnboardingCompleted, budget, source.ObservedAtUtc);
    }
}
