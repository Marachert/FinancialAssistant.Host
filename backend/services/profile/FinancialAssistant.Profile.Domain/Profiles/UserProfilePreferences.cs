using System.Globalization;

namespace FinancialAssistant.Profile.Domain.Profiles;

public sealed record UserProfilePreferences
{
    public const string StandardPrivacyMode = "standard";
    public const string StrictPrivacyMode = "strict";

    private static readonly HashSet<string> SupportedPrivacyModes = new(StringComparer.OrdinalIgnoreCase)
    {
        StandardPrivacyMode,
        StrictPrivacyMode
    };

    private UserProfilePreferences(
        string locale,
        string timeZone,
        string currencyCode,
        string privacyMode,
        bool aiPersonalizationEnabled)
    {
        Locale = locale;
        TimeZone = timeZone;
        CurrencyCode = currencyCode;
        PrivacyMode = privacyMode;
        AiPersonalizationEnabled = aiPersonalizationEnabled;
    }

    public string Locale { get; }

    public string TimeZone { get; }

    public string CurrencyCode { get; }

    public string PrivacyMode { get; }

    public bool AiPersonalizationEnabled { get; }

    public static UserProfilePreferences Default() =>
        Create("en-US", "UTC", "USD", StandardPrivacyMode, aiPersonalizationEnabled: false);

    public static UserProfilePreferences Create(
        string locale,
        string timeZone,
        string currencyCode,
        string privacyMode,
        bool aiPersonalizationEnabled)
    {
        var normalizedLocale = NormalizeLocale(locale);
        var normalizedTimeZone = NormalizeTimeZone(timeZone);
        var normalizedCurrency = NormalizeCurrency(currencyCode);
        var normalizedPrivacyMode = NormalizePrivacyMode(privacyMode);

        return new UserProfilePreferences(
            normalizedLocale,
            normalizedTimeZone,
            normalizedCurrency,
            normalizedPrivacyMode,
            aiPersonalizationEnabled);
    }

    private static string NormalizeLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            throw new ArgumentException("Locale is required.", nameof(locale));
        }

        var normalized = locale.Trim();
        _ = CultureInfo.GetCultureInfo(normalized);
        return normalized;
    }

    private static string NormalizeTimeZone(string timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            throw new ArgumentException("Time zone is required.", nameof(timeZone));
        }

        var normalized = timeZone.Trim();
        _ = TimeZoneInfo.FindSystemTimeZoneById(normalized);
        return normalized;
    }

    private static string NormalizeCurrency(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            throw new ArgumentException("Currency code is required.", nameof(currencyCode));
        }

        var normalized = currencyCode.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(value => value < 'A' || value > 'Z'))
        {
            throw new ArgumentException("Currency code must be a three-letter ISO code.", nameof(currencyCode));
        }

        return normalized;
    }

    private static string NormalizePrivacyMode(string privacyMode)
    {
        if (string.IsNullOrWhiteSpace(privacyMode))
        {
            throw new ArgumentException("Privacy mode is required.", nameof(privacyMode));
        }

        var normalized = privacyMode.Trim().ToLowerInvariant();
        if (!SupportedPrivacyModes.Contains(normalized))
        {
            throw new ArgumentException("Privacy mode must be either standard or strict.", nameof(privacyMode));
        }

        return normalized;
    }
}
