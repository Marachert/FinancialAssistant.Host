namespace FinancialAssistant.AiOrchestration.Domain;

public enum PromptFallbackBehavior
{
    RequireManualReview = 1,
}

public sealed record PromptExecutionPolicy
{
    public PromptExecutionPolicy(
        string promptName,
        int promptVersion,
        int maximumAttempts,
        bool retryTransientProviderFailures,
        bool retryInvalidStructuredOutput,
        PromptFallbackBehavior fallbackBehavior,
        string fallbackCode)
    {
        EnsureRequired(promptName, nameof(promptName));
        EnsureRequired(fallbackCode, nameof(fallbackCode));
        if (promptVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(promptVersion),
                "Prompt version must be positive.");
        }

        if (maximumAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAttempts),
                "Maximum attempts must be positive.");
        }

        PromptName = promptName;
        PromptVersion = promptVersion;
        MaximumAttempts = maximumAttempts;
        RetryTransientProviderFailures = retryTransientProviderFailures;
        RetryInvalidStructuredOutput = retryInvalidStructuredOutput;
        FallbackBehavior = fallbackBehavior;
        FallbackCode = fallbackCode;
    }

    public string PromptName { get; }

    public int PromptVersion { get; }

    public int MaximumAttempts { get; }

    public bool RetryTransientProviderFailures { get; }

    public bool RetryInvalidStructuredOutput { get; }

    public PromptFallbackBehavior FallbackBehavior { get; }

    public string FallbackCode { get; }

    private static void EnsureRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }
    }
}
