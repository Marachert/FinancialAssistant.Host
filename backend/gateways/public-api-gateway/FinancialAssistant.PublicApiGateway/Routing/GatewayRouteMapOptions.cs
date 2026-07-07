namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayRouteMapOptions
{
    public GatewayRouteDefinition[] Routes { get; init; } = Array.Empty<GatewayRouteDefinition>();
}
