using System.Text.Json;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Contracts;
using FinancialAssistant.AiOrchestration.Domain;

namespace FinancialAssistant.AiOrchestration.Application;

public sealed class AiOrchestrationService : IAiOrchestrationService
{
    private readonly IModelRouter modelRouter;
    private readonly IPromptRegistry promptRegistry;
    private readonly ILlmProviderResolver providerResolver;
    private readonly IStructuredOutputValidator outputValidator;
    private readonly IAiCallMetadataStore metadataStore;
    private readonly IAiOrchestrationClock clock;
    private readonly IAiCallIdGenerator callIdGenerator;

    public AiOrchestrationService(
        IModelRouter modelRouter,
        IPromptRegistry promptRegistry,
        ILlmProviderResolver providerResolver,
        IStructuredOutputValidator outputValidator,
        IAiCallMetadataStore metadataStore,
        IAiOrchestrationClock clock,
        IAiCallIdGenerator callIdGenerator)
    {
        this.modelRouter = modelRouter;
        this.promptRegistry = promptRegistry;
        this.providerResolver = providerResolver;
        this.outputValidator = outputValidator;
        this.metadataStore = metadataStore;
        this.clock = clock;
        this.callIdGenerator = callIdGenerator;
    }

    public async Task<AiCapabilityResult> ExecuteAsync(
        AiCapabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureRequired(request.CapabilityName, nameof(request.CapabilityName));
        EnsureRequired(request.PromptName, nameof(request.PromptName));
        EnsureRequired(request.Input, nameof(request.Input));

        var route = modelRouter.GetRequiredRoute(request.CapabilityName);
        var prompt = promptRegistry.GetRequired(request.PromptName, request.PromptVersion);
        var provider = providerResolver.GetRequired(route.Provider);
        var callId = callIdGenerator.CreateCallId();
        var startedAtUtc = clock.UtcNow;

        LlmProviderResponse response;
        try
        {
            response = await provider.CompleteAsync(
                    new LlmProviderRequest(
                        request.CapabilityName,
                        route.Model,
                        prompt.Template,
                        request.Input,
                        prompt.OutputJsonSchema),
                    cancellationToken)
                ?? throw new InvalidOperationException("The LLM provider returned no response.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RecordAsync(AiCallStatus.Cancelled, null);
            throw;
        }
        catch (LlmProviderException)
        {
            await RecordAsync(AiCallStatus.ProviderFailed, null);
            throw;
        }
        catch
        {
            await RecordAsync(AiCallStatus.ProviderFailed, null);
            throw new LlmProviderException(
                route.Provider,
                "provider_failure",
                isTransient: false);
        }

        if (response.InputTokens < 0 || response.OutputTokens < 0)
        {
            await RecordAsync(AiCallStatus.ProviderFailed, null);
            throw new InvalidOperationException("LLM providers must return non-negative token usage.");
        }

        var tokenUsage = new AiTokenUsage(response.InputTokens, response.OutputTokens);
        StructuredOutputValidationResult validation;
        try
        {
            validation = outputValidator.Validate(
                response.StructuredOutputJson,
                prompt.OutputJsonSchema);
        }
        catch
        {
            await RecordAsync(AiCallStatus.ValidationFailed, tokenUsage);
            throw;
        }

        if (!validation.IsValid)
        {
            await RecordAsync(AiCallStatus.ValidationFailed, tokenUsage);
            throw new StructuredOutputValidationException(validation.Errors);
        }

        using var output = JsonDocument.Parse(response.StructuredOutputJson);
        await RecordAsync(AiCallStatus.Succeeded, tokenUsage);

        return new AiCapabilityResult(
            callId,
            request.CapabilityName,
            prompt.Name,
            prompt.Version,
            route.Provider,
            route.Model,
            output.RootElement.Clone(),
            new AiSuggestionReview(
                Confidence: null,
                Ambiguities: new[] { "unverified_ai_output" },
                RequiresReview: true));

        Task RecordAsync(AiCallStatus status, AiTokenUsage? usage) =>
            metadataStore.AddAsync(
                new AiCallMetadata(
                    callId,
                    request.CapabilityName,
                    prompt.Name,
                    prompt.Version,
                    route.Provider,
                    route.Model,
                    status,
                    usage,
                    startedAtUtc,
                    clock.UtcNow),
                CancellationToken.None);
    }

    private static void EnsureRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }
    }
}
