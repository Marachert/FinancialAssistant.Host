namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayDestinationDefinition
{
    public string DestinationKey { get; init; } = string.Empty;

    public string BaseAddress { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public int RequestTimeoutSeconds { get; init; } = 30;
}
