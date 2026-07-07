using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayRouteCatalog
{
    private readonly IReadOnlyList<GatewayRouteDefinition> routes;

    public GatewayRouteCatalog(IOptions<GatewayRouteMapOptions> options)
    {
        routes = options.Value.Routes
            .Where(route => !string.IsNullOrWhiteSpace(route.RouteKey))
            .OrderBy(route => route.RouteKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<GatewayRouteDefinition> Routes => routes;
}
