namespace FinancialAssistant.Profile.Contracts;

public sealed record UserProfileResponse(
    string UserId,
    string Locale,
    string TimeZone,
    string CurrencyCode,
    string PrivacyMode,
    bool AiPersonalizationEnabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
