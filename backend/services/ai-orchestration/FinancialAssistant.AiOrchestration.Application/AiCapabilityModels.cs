using System.Text.Json;

namespace FinancialAssistant.AiOrchestration.Application;

public sealed record AiCapabilityRequest(
    string CapabilityName,
    string PromptName,
    string Input,
    int? PromptVersion = null);

public sealed record AiCapabilityResult(
    string CallId,
    string CapabilityName,
    string PromptName,
    int PromptVersion,
    string Provider,
    string Model,
    JsonElement StructuredOutput);

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
