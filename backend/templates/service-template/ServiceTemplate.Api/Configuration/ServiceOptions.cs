namespace FinancialAssistant.ServiceTemplate.Api.Configuration;

public sealed class ServiceOptions
{
    public const string SectionName = "Service";

    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;
}
