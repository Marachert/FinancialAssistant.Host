namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayDestinationMapOptions
{
    public GatewayDestinationDefinition[] Destinations { get; init; } = Array.Empty<GatewayDestinationDefinition>();
}
