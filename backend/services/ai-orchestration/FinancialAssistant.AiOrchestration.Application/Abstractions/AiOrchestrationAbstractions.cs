using FinancialAssistant.AiOrchestration.Domain;

namespace FinancialAssistant.AiOrchestration.Application.Abstractions;

public interface ILlmProvider
{
    string Name { get; }

    Task<LlmProviderResponse> CompleteAsync(
        LlmProviderRequest request,
        CancellationToken cancellationToken);
}

public interface ILlmProviderResolver
{
    ILlmProvider GetRequired(string providerName);
}

public interface IModelRouter
{
    AiModelRoute GetRequiredRoute(string capabilityName);
}

public interface IPromptRegistry
{
    PromptDefinition GetRequired(string promptName, int? version = null);
}

public interface IStructuredOutputValidator
{
    StructuredOutputValidationResult Validate(
        string structuredOutputJson,
        string jsonSchema);
}

public interface IAiCallMetadataStore
{
    Task AddAsync(AiCallMetadata metadata, CancellationToken cancellationToken);
}

public interface IAiOrchestrationClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IAiCallIdGenerator
{
    string CreateCallId();
}
