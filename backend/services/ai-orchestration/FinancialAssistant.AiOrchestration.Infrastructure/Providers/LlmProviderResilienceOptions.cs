namespace FinancialAssistant.AiOrchestration.Infrastructure.Providers;

public sealed record LlmProviderResilienceOptions
{
    public LlmProviderResilienceOptions(
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
}
