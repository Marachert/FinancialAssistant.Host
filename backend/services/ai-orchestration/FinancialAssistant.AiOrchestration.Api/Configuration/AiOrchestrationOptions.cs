using FinancialAssistant.AiOrchestration.Domain;
using FinancialAssistant.AiOrchestration.Infrastructure.Prompts;
using FinancialAssistant.AiOrchestration.Infrastructure.Providers;

namespace FinancialAssistant.AiOrchestration.Api.Configuration;

public sealed class AiOrchestrationOptions
{
    public const string SectionName = "AiOrchestration";
    public const string SuggestionAuthority = "suggestion";

    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string OutputAuthority { get; init; } = SuggestionAuthority;

    public AiProviderClientOptions Provider { get; init; } = new();
}

public sealed class AiProviderClientOptions
{
    public string Name { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public int RequestTimeoutSeconds { get; init; } = 30;

    public int MaximumAttempts { get; init; } =
        TransactionParsingPromptCatalog.ExecutionPolicy.MaximumAttempts;

    public int RetryDelayMilliseconds { get; init; } = 200;

    public bool HasAnyProviderIdentity =>
        !string.IsNullOrWhiteSpace(Name) ||
        !string.IsNullOrWhiteSpace(Model) ||
        !string.IsNullOrWhiteSpace(Endpoint);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Model) &&
        Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) &&
        endpoint.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(endpoint.UserInfo);

    public bool HasValidResilienceSettings =>
        RequestTimeoutSeconds is >= 1 and <= 120 &&
        MaximumAttempts >= 1 &&
        MaximumAttempts <= TransactionParsingPromptCatalog.ExecutionPolicy.MaximumAttempts &&
        RetryDelayMilliseconds is >= 0 and <= 5000;

    public AiModelRoute CreateRoute(string capabilityName)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Provider name, model, and HTTPS endpoint must be configured first.");
        }

        return new AiModelRoute(capabilityName, Name, Model);
    }

    public LlmProviderResilienceOptions CreateResilienceOptions()
    {
        if (!HasValidResilienceSettings)
        {
            throw new InvalidOperationException(
                "Provider timeout and retry settings are outside the allowed range.");
        }

        return new LlmProviderResilienceOptions(
            TimeSpan.FromSeconds(RequestTimeoutSeconds),
            MaximumAttempts,
            TimeSpan.FromMilliseconds(RetryDelayMilliseconds));
    }
}
