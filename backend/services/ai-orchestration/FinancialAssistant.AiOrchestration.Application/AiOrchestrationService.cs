using System.Globalization;
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
    private readonly AiUsageCostControlPolicy usagePolicy;
    private readonly IAiUsageLimiter usageLimiter;

    public AiOrchestrationService(
        IModelRouter modelRouter,
        IPromptRegistry promptRegistry,
        ILlmProviderResolver providerResolver,
        IStructuredOutputValidator outputValidator,
        IAiCallMetadataStore metadataStore,
        IAiOrchestrationClock clock,
        IAiCallIdGenerator callIdGenerator,
        AiUsageCostControlPolicy usagePolicy,
        IAiUsageLimiter usageLimiter)
    {
        this.modelRouter = modelRouter;
        this.promptRegistry = promptRegistry;
        this.providerResolver = providerResolver;
        this.outputValidator = outputValidator;
        this.metadataStore = metadataStore;
        this.clock = clock;
        this.callIdGenerator = callIdGenerator;
        this.usagePolicy = usagePolicy;
        this.usageLimiter = usageLimiter;
    }

    public async Task<AiCapabilityResult> ExecuteAsync(
        AiCapabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureRequired(request.CapabilityName, nameof(request.CapabilityName));
        EnsureRequired(request.PromptName, nameof(request.PromptName));
        EnsureRequired(request.Input, nameof(request.Input));
        var usageSubjectId = NormalizeUsageSubjectId(request.UsageSubjectId);

        var route = modelRouter.GetRequiredRoute(request.CapabilityName);
        var prompt = promptRegistry.GetRequired(request.PromptName, request.PromptVersion);
        var provider = providerResolver.GetRequired(route.Provider);
        var callId = callIdGenerator.CreateCallId();
        var startedAtUtc = clock.UtcNow;
        var providerRequestCharacters =
            (long)request.CapabilityName.Length +
            route.Model.Length +
            prompt.Template.Length +
            request.Input.Length +
            prompt.OutputJsonSchema.Length;
        if (providerRequestCharacters > usagePolicy.MaximumRequestCharacters)
        {
            const string code = "provider_request_too_large";
            await RecordAsync(AiCallStatus.CostControlRejected, null, code, 0);
            throw new AiUsageCostControlException(code);
        }

        if (!usageLimiter.TryAcquire(
                usageSubjectId,
                route.Provider,
                DateOnly.FromDateTime(startedAtUtc.UtcDateTime),
                usagePolicy.PerUserDailyRequestLimit))
        {
            const string code = "daily_usage_limit_exceeded";
            await RecordAsync(AiCallStatus.CostControlRejected, null, code, 0);
            throw new AiUsageCostControlException(code);
        }

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
            await RecordAsync(AiCallStatus.Cancelled, null, "cancelled", 1);
            throw;
        }
        catch (LlmProviderException exception)
        {
            await RecordAsync(
                AiCallStatus.ProviderFailed,
                null,
                NormalizeProviderFailureCategory(exception.Code),
                1);
            throw;
        }
        catch
        {
            await RecordAsync(AiCallStatus.ProviderFailed, null, "provider_failure", 1);
            throw new LlmProviderException(
                route.Provider,
                "provider_failure",
                isTransient: false);
        }

        if (response.InputTokens < 0 || response.OutputTokens < 0)
        {
            await RecordAsync(AiCallStatus.ProviderFailed, null, "invalid_token_usage", 1);
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
            await RecordAsync(
                AiCallStatus.ValidationFailed,
                tokenUsage,
                "structured_output_validation_failed",
                1);
            throw;
        }

        if (!validation.IsValid)
        {
            await RecordAsync(
                AiCallStatus.ValidationFailed,
                tokenUsage,
                "structured_output_validation_failed",
                1);
            throw new StructuredOutputValidationException(validation.Errors);
        }

        using var output = JsonDocument.Parse(response.StructuredOutputJson);
        await RecordAsync(AiCallStatus.Succeeded, tokenUsage, failureCategory: null, 1);

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

        Task RecordAsync(
            AiCallStatus status,
            AiTokenUsage? usage,
            string? failureCategory,
            int providerRequestUnits) =>
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
                    Confidence: null,
                    failureCategory,
                    startedAtUtc,
                    clock.UtcNow,
                    new AiProviderUsageMetadata(
                        providerRequestCharacters,
                        providerRequestUnits,
                        startedAtUtc.ToString("yyyy-MM", CultureInfo.InvariantCulture))),
                CancellationToken.None);
    }

    private static string NormalizeProviderFailureCategory(string code) =>
        code is
            "invalid_provider_response" or
            "provider_failure" or
            "provider_timeout" or
            "provider_unavailable"
            ? code
            : "provider_failure";

    private static void EnsureRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }
    }

    private static string NormalizeUsageSubjectId(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length > 200 ||
            normalized.Any(character =>
                !(character is >= 'A' and <= 'Z' ||
                  character is >= 'a' and <= 'z' ||
                  character is >= '0' and <= '9' ||
                  character is '.' or '_' or '~' or '-')))
        {
            throw new ArgumentException(
                "A safe opaque usage subject identifier is required.",
                nameof(AiCapabilityRequest.UsageSubjectId));
        }

        return normalized;
    }
}
