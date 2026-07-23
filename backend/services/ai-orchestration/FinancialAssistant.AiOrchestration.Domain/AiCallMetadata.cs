namespace FinancialAssistant.AiOrchestration.Domain;

public enum AiCallStatus
{
    Succeeded,
    ValidationFailed,
    ProviderFailed,
    Cancelled,
}

public sealed record AiTokenUsage(
    int InputTokens,
    int OutputTokens)
{
    public long TotalTokens => (long)InputTokens + OutputTokens;
}

public sealed record AiCallMetadata(
    string CallId,
    string CapabilityName,
    string PromptName,
    int PromptVersion,
    string Provider,
    string Model,
    AiCallStatus Status,
    AiTokenUsage? TokenUsage,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);
