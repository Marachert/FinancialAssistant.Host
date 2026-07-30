namespace FinancialAssistant.AiOrchestration.Domain;

public enum AiCallStatus
{
    Succeeded,
    ValidationFailed,
    ProviderFailed,
    CostControlRejected,
    Cancelled,
}

public sealed record AiTokenUsage(
    int InputTokens,
    int OutputTokens)
{
    public long TotalTokens => (long)InputTokens + OutputTokens;
}

public sealed record AiProviderUsageMetadata(
    long RequestCharacters,
    int ProviderRequestUnits,
    string BillingMonth);

public sealed record AiCallMetadata(
    string CallId,
    string CapabilityName,
    string PromptName,
    int PromptVersion,
    string Provider,
    string Model,
    AiCallStatus Status,
    AiTokenUsage? TokenUsage,
    decimal? Confidence,
    string? FailureCategory,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    AiProviderUsageMetadata ProviderUsage)
{
    public string RequestId => CallId;

    public string TraceId => CallId;

    public long DurationMilliseconds =>
        Math.Max(0, (long)(CompletedAtUtc - StartedAtUtc).TotalMilliseconds);
}
