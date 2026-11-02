using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Infrastructure.Persistence;
using FinancialAssistant.Profile.Client;
using FinancialAssistant.Profile.Contracts;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Analytics.Infrastructure;

public sealed class NativeAnalyticsProfileSettingsLoader(NativeProfileSettingsClient client, PostgresAnalyticsLimitProvider limits)
{
    public async Task RefreshAsync(string owner, IEnumerable<string> currencies, CancellationToken token)
    {
        var normalized = currencies.Select(c => c.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        if (normalized.Length is < 1 or > 8) { throw new ArgumentException("Invalid settings currency selection."); }
        foreach (var currency in normalized) { AnalyticsProjectionCodec.Currency(currency); }
        ProfileFinancialSettingsSnapshot source;
        try { source = await client.ReadAsync(owner, token); }
        catch (ProfileSettingsUnavailableException) { throw new StorageUnavailableException(); }
        foreach (var currency in normalized) { await limits.ApplySnapshotAsync(CreateSnapshot(source, currency), token); }
    }

    public static AnalyticsLimitSnapshot CreateSnapshot(ProfileFinancialSettingsSnapshot source, string currency)
    {
        AnalyticsProjectionCodec.Currency(currency);
        // Profile defines only a monthly budget: never infer daily/weekly limits or exchange rates.
        decimal? monthly = source.CurrencyCode == currency && source.MonthlyBudgetAmount > 0 ? source.MonthlyBudgetAmount : null;
        return new(source.OwnerHash, currency, source.SourceVersion, source.SourceRevision,
            new AnalyticsExpenseLimits(null, null, monthly), source.ObservedAtUtc);
    }
}
