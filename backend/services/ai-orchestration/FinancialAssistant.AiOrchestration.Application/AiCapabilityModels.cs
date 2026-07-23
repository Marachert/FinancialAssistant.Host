namespace FinancialAssistant.AiOrchestration.Application;

public sealed record LlmProviderRequest(
    string CapabilityName,
    string Model,
    string PromptTemplate,
    string Input,
    string OutputJsonSchema);

public sealed record LlmProviderResponse(
    string StructuredOutputJson,
    int InputTokens,
    int OutputTokens);

public sealed record StructuredOutputValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);
