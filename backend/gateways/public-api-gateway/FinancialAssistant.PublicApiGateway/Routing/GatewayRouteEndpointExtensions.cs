using FinancialAssistant.PublicApiGateway.Security;

namespace FinancialAssistant.PublicApiGateway.Routing;

public static class GatewayRouteEndpointExtensions
{
    public static WebApplication MapGatewayRouteMap(this WebApplication app)
    {
        var routeCatalog = app.Services.GetRequiredService<GatewayRouteCatalog>();

        app.MapGet("/gateway/routes", () => Results.Ok(new
        {
            routes = routeCatalog.PublicRoutes
        }));

        foreach (var route in routeCatalog.Routes)
        {
            app.MapMethods(
                route.PublicPattern,
                route.Methods,
                (HttpContext context, GatewaySecurityBoundary securityBoundary, GatewayRequestDispatcher dispatcher) =>
                    HandleGatewayRouteAsync(context, securityBoundary, dispatcher, route));
            if (!string.IsNullOrWhiteSpace(route.CatchAllPattern))
            {
                app.MapMethods(
                    route.CatchAllPattern,
                    route.Methods,
                    (HttpContext context, GatewaySecurityBoundary securityBoundary, GatewayRequestDispatcher dispatcher) =>
                        HandleGatewayRouteAsync(context, securityBoundary, dispatcher, route));
            }
        }

        return app;
    }

    private static async Task HandleGatewayRouteAsync(
        HttpContext context,
        GatewaySecurityBoundary securityBoundary,
        GatewayRequestDispatcher dispatcher,
        GatewayRouteDefinition route)
    {
        if (!await securityBoundary.AuthorizeAsync(context, route))
        {
            return;
        }

        await dispatcher.DispatchAsync(context, route);
    }
}
