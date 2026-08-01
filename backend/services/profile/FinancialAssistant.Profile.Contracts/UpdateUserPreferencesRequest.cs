namespace FinancialAssistant.Profile.Contracts;

public sealed record UpdateUserPreferencesRequest(
    string? Locale,
    string? TimeZone,
    string? CurrencyCode,
    string? PrivacyMode,
    bool? AiPersonalizationEnabled,
    string? FirstDayOfWeek = null,
    decimal? MonthlyBudgetAmount = null,
    bool? BudgetNotificationsEnabled = null,
    bool? WeeklySummaryNotificationsEnabled = null,
    bool? ProfileOnboardingCompleted = null,
    bool? PreferencesOnboardingCompleted = null);
