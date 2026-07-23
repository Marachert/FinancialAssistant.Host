namespace FinancialAssistant.AiOrchestration.Api.Configuration;

public sealed class AiOrchestrationOptions
{
    public const string SectionName = "AiOrchestration";
    public const string SuggestionAuthority = "suggestion";

    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string OutputAuthority { get; init; } = SuggestionAuthority;

    public AiProviderPlaceholderOptions Provider { get; init; } = new();
}

public sealed class AiProviderPlaceholderOptions
{
    public string Name { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Model) &&
        Uri.TryCreate(Endpoint, UriKind.Absolute, out _);
}
