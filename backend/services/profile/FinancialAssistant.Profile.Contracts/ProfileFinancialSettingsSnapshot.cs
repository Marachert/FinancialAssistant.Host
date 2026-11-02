using System.Text.Json.Serialization;

namespace FinancialAssistant.Profile.Contracts;

public static class ProfileFinancialSettingsApi
{
    public const string Path = "/internal/profile/financial-settings";
    public const string ReaderHeader = "X-Profile-Reader";
    public const string AuthenticationHeader = "X-Profile-Authentication";
}

public sealed record ProfileFinancialSettingsQuery([property: JsonRequired] string OwnerHash);

public sealed record ProfileFinancialSettingsSnapshot(
    [property: JsonRequired] string OwnerHash,
    [property: JsonRequired] string CurrencyCode,
    [property: JsonRequired] decimal MonthlyBudgetAmount,
    [property: JsonRequired] string PrivacyMode,
    [property: JsonRequired] bool AiPersonalizationEnabled,
    [property: JsonRequired] bool BudgetNotificationsEnabled,
    [property: JsonRequired] bool WeeklySummaryNotificationsEnabled,
    [property: JsonRequired] bool ProfileOnboardingCompleted,
    [property: JsonRequired] bool PreferencesOnboardingCompleted,
    [property: JsonRequired] string SourceVersion,
    [property: JsonRequired] long SourceRevision,
    [property: JsonRequired] DateTimeOffset ObservedAtUtc);
