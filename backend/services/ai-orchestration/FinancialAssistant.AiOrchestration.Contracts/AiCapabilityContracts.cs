using System.Text.Json;

namespace FinancialAssistant.AiOrchestration.Contracts;

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
    JsonElement StructuredOutput,
    AiSuggestionReview Review);

public sealed record AiSuggestionReview(
    decimal? Confidence,
    IReadOnlyList<string> Ambiguities,
    bool RequiresReview);

public sealed record AiOrchestrationServiceInfoResponse(
    string Name,
    string Version,
    string Environment,
    string OutputAuthority,
    bool ProviderConfigured);
