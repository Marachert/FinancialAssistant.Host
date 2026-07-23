namespace FinancialAssistant.AiOrchestration.Domain;

public sealed record AiModelRoute
{
    public AiModelRoute(string capabilityName, string provider, string model)
    {
        EnsureRequired(capabilityName, nameof(capabilityName));
        EnsureRequired(provider, nameof(provider));
        EnsureRequired(model, nameof(model));
        CapabilityName = capabilityName;
        Provider = provider;
        Model = model;
    }

    public string CapabilityName { get; }

    public string Provider { get; }

    public string Model { get; }

    private static void EnsureRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }
    }
}
