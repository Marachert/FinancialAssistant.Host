namespace FinancialAssistant.Profile.Contracts;

public sealed record UserProfileResponse(
    string UserId,
    string Locale,
    string TimeZone,
    string CurrencyCode,
    string PrivacyMode,
    bool AiPersonalizationEnabled,
    string FirstDayOfWeek,
    decimal MonthlyBudgetAmount,
    bool BudgetNotificationsEnabled,
    bool WeeklySummaryNotificationsEnabled,
    bool ProfileOnboardingCompleted,
    bool PreferencesOnboardingCompleted,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
