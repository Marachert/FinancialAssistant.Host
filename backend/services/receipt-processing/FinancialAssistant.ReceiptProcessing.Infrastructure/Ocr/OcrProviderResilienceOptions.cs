using System.Globalization;
using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;

public sealed record OcrProviderResilienceOptions
{
    public const string ConfigurationSection = "ReceiptProcessing:Ocr";
    public const string DisabledMode = "disabled";
    public const string SandboxMode = "sandbox";
    public const string ProductionMode = "production";

    public const int DefaultRequestTimeoutSeconds = 30;

    public const int DefaultMaximumAttempts = 2;

    public const int DefaultRetryDelayMilliseconds = 100;

    public const string DefaultProviderName = "unconfigured";

    public const string DefaultModelKey = "unconfigured";

    public const int DefaultPerUserDailyRequestLimit = 10;

    public const long DefaultMaximumProviderRequestBytes =
        ReceiptProcessingService.MaximumReceiptSizeBytes;

    public const decimal DefaultMonthlyBudgetAlertUsd = 25m;

    public OcrProviderResilienceOptions(
        TimeSpan requestTimeout,
        int maximumAttempts,
        TimeSpan retryDelay,
        string providerName = DefaultProviderName,
        string modelKey = DefaultModelKey,
        bool enabled = false,
        string mode = DisabledMode,
        string endpoint = "",
        string credentialEnvironmentVariable = "",
        int perUserDailyRequestLimit = DefaultPerUserDailyRequestLimit,
        long maximumProviderRequestBytes = DefaultMaximumProviderRequestBytes,
        decimal monthlyBudgetAlertUsd = DefaultMonthlyBudgetAlertUsd,
        bool adminVisibilityEnabled = true)
    {
        if (requestTimeout <= TimeSpan.Zero || requestTimeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                "Request timeout must be greater than zero and no more than two minutes.");
        }

        if (maximumAttempts is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAttempts),
                "Maximum attempts must be between one and three.");
        }

        if (retryDelay < TimeSpan.Zero || retryDelay > TimeSpan.FromSeconds(5))
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryDelay),
                "Retry delay must be between zero and five seconds.");
        }

        ProviderName = NormalizeIdentity(providerName, nameof(providerName));
        ModelKey = NormalizeIdentity(modelKey, nameof(modelKey));
        Enabled = enabled;
        Mode = mode?.Trim() ?? string.Empty;
        Endpoint = endpoint?.Trim() ?? string.Empty;
        CredentialEnvironmentVariable =
            credentialEnvironmentVariable?.Trim() ?? string.Empty;
        ValidateProviderConfiguration();
        UsageCostControlPolicy = new OcrUsageCostControlPolicy(
            perUserDailyRequestLimit,
            maximumProviderRequestBytes,
            monthlyBudgetAlertUsd,
            adminVisibilityEnabled);
        RequestTimeout = requestTimeout;
        MaximumAttempts = maximumAttempts;
        RetryDelay = retryDelay;
    }

    public TimeSpan RequestTimeout { get; }

    public int MaximumAttempts { get; }

    public TimeSpan RetryDelay { get; }

    public string ProviderName { get; }

    public string ModelKey { get; }

    public bool Enabled { get; }

    public string Mode { get; }

    public string Endpoint { get; }

    public string CredentialEnvironmentVariable { get; }

    public OcrUsageCostControlPolicy UsageCostControlPolicy { get; }

    public static OcrProviderResilienceOptions FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var timeoutSeconds = ReadInteger(
            configuration,
            "RequestTimeoutSeconds",
            DefaultRequestTimeoutSeconds);
        var maximumAttempts = ReadInteger(
            configuration,
            "MaximumAttempts",
            DefaultMaximumAttempts);
        var retryDelayMilliseconds = ReadInteger(
            configuration,
            "RetryDelayMilliseconds",
            DefaultRetryDelayMilliseconds);
        var enabled = ReadBoolean(configuration, "Enabled", defaultValue: false);
        var perUserDailyRequestLimit = ReadInteger(
            configuration,
            "UsageCostControls:PerUserDailyRequestLimit",
            DefaultPerUserDailyRequestLimit);
        var maximumProviderRequestBytes = ReadLong(
            configuration,
            "UsageCostControls:MaximumProviderRequestBytes",
            DefaultMaximumProviderRequestBytes);
        var monthlyBudgetAlertUsd = ReadDecimal(
            configuration,
            "UsageCostControls:MonthlyBudgetAlertUsd",
            DefaultMonthlyBudgetAlertUsd);
        var adminVisibilityEnabled = ReadBoolean(
            configuration,
            "UsageCostControls:AdminVisibilityEnabled",
            defaultValue: true);

        return new OcrProviderResilienceOptions(
            TimeSpan.FromSeconds(timeoutSeconds),
            maximumAttempts,
            TimeSpan.FromMilliseconds(retryDelayMilliseconds),
            configuration[$"{ConfigurationSection}:ProviderName"] ?? DefaultProviderName,
            configuration[$"{ConfigurationSection}:ModelKey"] ?? DefaultModelKey,
            enabled,
            configuration[$"{ConfigurationSection}:Mode"] ?? DisabledMode,
            configuration[$"{ConfigurationSection}:Endpoint"] ?? string.Empty,
            configuration[$"{ConfigurationSection}:CredentialEnvironmentVariable"] ??
            string.Empty,
            perUserDailyRequestLimit,
            maximumProviderRequestBytes,
            monthlyBudgetAlertUsd,
            adminVisibilityEnabled);
    }

    private static int ReadInteger(
        IConfiguration configuration,
        string settingName,
        int defaultValue)
    {
        var key = $"{ConfigurationSection}:{settingName}";
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException(
                $"Configuration setting '{key}' must be an integer.");
        }

        return parsed;
    }

    private static bool ReadBoolean(
        IConfiguration configuration,
        string settingName,
        bool defaultValue)
    {
        var key = $"{ConfigurationSection}:{settingName}";
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!bool.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException(
                $"Configuration setting '{key}' must be a boolean.");
        }

        return parsed;
    }

    private static long ReadLong(
        IConfiguration configuration,
        string settingName,
        long defaultValue)
    {
        var key = $"{ConfigurationSection}:{settingName}";
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException(
                $"Configuration setting '{key}' must be an integer.");
        }

        return parsed;
    }

    private static decimal ReadDecimal(
        IConfiguration configuration,
        string settingName,
        decimal defaultValue)
    {
        var key = $"{ConfigurationSection}:{settingName}";
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException(
                $"Configuration setting '{key}' must be a decimal number.");
        }

        return parsed;
    }

    private static string NormalizeIdentity(string value, string parameterName)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length > 64 ||
            normalized.Any(character =>
                !(character is >= 'a' and <= 'z' ||
                    character is >= '0' and <= '9' ||
                    character is '.' or '_' or '-')))
        {
            throw new ArgumentException(
                "Provider identity must contain 1 to 64 lowercase safe characters.",
                parameterName);
        }

        return normalized;
    }

    private void ValidateProviderConfiguration()
    {
        if (!Enabled)
        {
            if (!string.Equals(Mode, DisabledMode, StringComparison.Ordinal) ||
                ProviderName != DefaultProviderName ||
                ModelKey != DefaultModelKey ||
                Endpoint.Length > 0 ||
                CredentialEnvironmentVariable.Length > 0)
            {
                throw new InvalidOperationException(
                    "Disabled OCR provider settings must use empty local placeholders.");
            }

            return;
        }

        if (!(string.Equals(Mode, SandboxMode, StringComparison.Ordinal) ||
              string.Equals(Mode, ProductionMode, StringComparison.Ordinal)) ||
            ProviderName == DefaultProviderName ||
            ModelKey == DefaultModelKey ||
            !Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !IsSafeEnvironmentVariableName(CredentialEnvironmentVariable))
        {
            throw new InvalidOperationException(
                "Enabled OCR provider settings require a valid mode, identity, HTTPS endpoint, and credential environment-variable reference.");
        }
    }

    private static bool IsSafeEnvironmentVariableName(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value[0] is >= 'A' and <= 'Z' &&
        value.All(character =>
            character is >= 'A' and <= 'Z' ||
            character is >= '0' and <= '9' ||
            character == '_');
}
