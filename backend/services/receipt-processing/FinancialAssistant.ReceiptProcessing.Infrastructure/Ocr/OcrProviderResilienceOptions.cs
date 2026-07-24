using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;

public sealed record OcrProviderResilienceOptions
{
    public const string ConfigurationSection = "ReceiptProcessing:Ocr";

    public const int DefaultRequestTimeoutSeconds = 30;

    public const int DefaultMaximumAttempts = 2;

    public const int DefaultRetryDelayMilliseconds = 100;

    public OcrProviderResilienceOptions(
        TimeSpan requestTimeout,
        int maximumAttempts,
        TimeSpan retryDelay)
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

        RequestTimeout = requestTimeout;
        MaximumAttempts = maximumAttempts;
        RetryDelay = retryDelay;
    }

    public TimeSpan RequestTimeout { get; }

    public int MaximumAttempts { get; }

    public TimeSpan RetryDelay { get; }

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
            TimeSpan.FromMilliseconds(retryDelayMilliseconds));
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
}
