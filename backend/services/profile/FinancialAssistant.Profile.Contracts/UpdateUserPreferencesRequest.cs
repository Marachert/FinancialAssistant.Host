namespace FinancialAssistant.Profile.Contracts;

public sealed record UpdateUserPreferencesRequest(
    string? Locale,
    string? TimeZone,
    string? CurrencyCode,
    string? PrivacyMode,
    bool? AiPersonalizationEnabled);
