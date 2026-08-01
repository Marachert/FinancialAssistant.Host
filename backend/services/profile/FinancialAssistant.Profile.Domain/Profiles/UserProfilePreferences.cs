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

    private static readonly HashSet<string> SupportedFirstDaysOfWeek = new(StringComparer.OrdinalIgnoreCase)
    {
        "monday",
        "tuesday",
        "wednesday",
        "thursday",
        "friday",
        "saturday",
        "sunday"
    };

    private UserProfilePreferences(
        string locale,
        string timeZone,
        string currencyCode,
        string privacyMode,
        bool aiPersonalizationEnabled,
        string firstDayOfWeek,
        decimal monthlyBudgetAmount,
        bool budgetNotificationsEnabled,
        bool weeklySummaryNotificationsEnabled,
        bool profileOnboardingCompleted,
        bool preferencesOnboardingCompleted)
    {
        Locale = locale;
        TimeZone = timeZone;
        CurrencyCode = currencyCode;
        PrivacyMode = privacyMode;
        AiPersonalizationEnabled = aiPersonalizationEnabled;
        FirstDayOfWeek = firstDayOfWeek;
        MonthlyBudgetAmount = monthlyBudgetAmount;
        BudgetNotificationsEnabled = budgetNotificationsEnabled;
        WeeklySummaryNotificationsEnabled = weeklySummaryNotificationsEnabled;
        ProfileOnboardingCompleted = profileOnboardingCompleted;
        PreferencesOnboardingCompleted = preferencesOnboardingCompleted;
    }

    public string Locale { get; }

    public string TimeZone { get; }

    public string CurrencyCode { get; }

    public string PrivacyMode { get; }

    public bool AiPersonalizationEnabled { get; }

    public string FirstDayOfWeek { get; }

    public decimal MonthlyBudgetAmount { get; }

    public bool BudgetNotificationsEnabled { get; }

    public bool WeeklySummaryNotificationsEnabled { get; }

    public bool ProfileOnboardingCompleted { get; }

    public bool PreferencesOnboardingCompleted { get; }

    public static UserProfilePreferences Default() =>
        Create(
            "en-US",
            "UTC",
            "USD",
            StandardPrivacyMode,
            aiPersonalizationEnabled: false,
            firstDayOfWeek: "monday",
            monthlyBudgetAmount: 0m,
            budgetNotificationsEnabled: false,
            weeklySummaryNotificationsEnabled: false,
            profileOnboardingCompleted: false,
            preferencesOnboardingCompleted: false);

    public static UserProfilePreferences Create(
        string locale,
        string timeZone,
        string currencyCode,
        string privacyMode,
        bool aiPersonalizationEnabled,
        string firstDayOfWeek = "monday",
        decimal monthlyBudgetAmount = 0m,
        bool budgetNotificationsEnabled = false,
        bool weeklySummaryNotificationsEnabled = false,
        bool profileOnboardingCompleted = false,
        bool preferencesOnboardingCompleted = false)
    {
        var normalizedLocale = NormalizeLocale(locale);
        var normalizedTimeZone = NormalizeTimeZone(timeZone);
        var normalizedCurrency = NormalizeCurrency(currencyCode);
        var normalizedPrivacyMode = NormalizePrivacyMode(privacyMode);
        var normalizedFirstDayOfWeek = NormalizeFirstDayOfWeek(firstDayOfWeek);
        var normalizedMonthlyBudgetAmount = NormalizeMonthlyBudgetAmount(monthlyBudgetAmount);

        return new UserProfilePreferences(
            normalizedLocale,
            normalizedTimeZone,
            normalizedCurrency,
            normalizedPrivacyMode,
            aiPersonalizationEnabled,
            normalizedFirstDayOfWeek,
            normalizedMonthlyBudgetAmount,
            budgetNotificationsEnabled,
            weeklySummaryNotificationsEnabled,
            profileOnboardingCompleted,
            preferencesOnboardingCompleted);
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
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(normalized);
        }
        catch (Exception exception)
            when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException("Time zone is invalid.", nameof(timeZone), exception);
        }

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

    private static string NormalizeFirstDayOfWeek(string firstDayOfWeek)
    {
        if (string.IsNullOrWhiteSpace(firstDayOfWeek))
        {
            throw new ArgumentException("First day of week is required.", nameof(firstDayOfWeek));
        }

        var normalized = firstDayOfWeek.Trim().ToLowerInvariant();
        if (!SupportedFirstDaysOfWeek.Contains(normalized))
        {
            throw new ArgumentException(
                "First day of week must be a weekday name from monday through sunday.",
                nameof(firstDayOfWeek));
        }

        return normalized;
    }

    private static decimal NormalizeMonthlyBudgetAmount(decimal monthlyBudgetAmount)
    {
        if (monthlyBudgetAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(monthlyBudgetAmount),
                "Monthly budget amount cannot be negative.");
        }

        return monthlyBudgetAmount;
    }
}
