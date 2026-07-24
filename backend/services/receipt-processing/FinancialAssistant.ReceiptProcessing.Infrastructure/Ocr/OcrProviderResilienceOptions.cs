using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;

public sealed record OcrProviderResilienceOptions
{
    public const string ConfigurationSection = "ReceiptProcessing:Ocr";

    public const int DefaultRequestTimeoutSeconds = 30;

    public const int DefaultMaximumAttempts = 2;

    public const int DefaultRetryDelayMilliseconds = 100;

    public const string DefaultProviderName = "unconfigured";

    public const string DefaultModelKey = "unconfigured";

    public OcrProviderResilienceOptions(
        TimeSpan requestTimeout,
        int maximumAttempts,
        TimeSpan retryDelay,
        string providerName = DefaultProviderName,
        string modelKey = DefaultModelKey)
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
        RequestTimeout = requestTimeout;
        MaximumAttempts = maximumAttempts;
        RetryDelay = retryDelay;
    }

    public TimeSpan RequestTimeout { get; }

    public int MaximumAttempts { get; }

    public TimeSpan RetryDelay { get; }

    public string ProviderName { get; }

    public string ModelKey { get; }

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

        return new OcrProviderResilienceOptions(
            TimeSpan.FromSeconds(timeoutSeconds),
            maximumAttempts,
            TimeSpan.FromMilliseconds(retryDelayMilliseconds),
            configuration[$"{ConfigurationSection}:ProviderName"] ?? DefaultProviderName,
            configuration[$"{ConfigurationSection}:ModelKey"] ?? DefaultModelKey);
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

    private static string NormalizeIdentity(string value, string parameterName)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length > 64 ||
            normalized.Any(character =>
                !(char.IsLower(character) ||
                    char.IsDigit(character) ||
                    character is '.' or '_' or '-')))
        {
            throw new ArgumentException(
                "Provider identity must contain 1 to 64 lowercase safe characters.",
                parameterName);
        }

        return normalized;
    }
}
