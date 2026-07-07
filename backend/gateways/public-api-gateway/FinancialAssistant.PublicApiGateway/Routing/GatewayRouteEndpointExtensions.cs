using FinancialAssistant.PublicApiGateway.Security;

namespace FinancialAssistant.PublicApiGateway.Routing;

public static class GatewayRouteEndpointExtensions
{
    private static readonly string[] DefaultMethods = new[] { "GET", "POST", "PUT", "PATCH" };

    public static WebApplication MapGatewayRouteMap(this WebApplication app)
    {
        var routeCatalog = app.Services.GetRequiredService<GatewayRouteCatalog>();

        app.MapGet("/gateway/routes", () => Results.Ok(new
        {
            routes = routeCatalog.Routes
        }));

        foreach (var route in routeCatalog.Routes)
        {
            var methods = route.Methods.Length == 0 ? DefaultMethods : route.Methods;

            app.MapMethods(
                route.PublicPattern,
                methods,
                (HttpContext context, GatewaySecurityBoundary securityBoundary, GatewayRequestDispatcher dispatcher) =>
                    HandleGatewayRouteAsync(context, securityBoundary, dispatcher, route));

            if (!string.IsNullOrWhiteSpace(route.CatchAllPattern))
            {
                app.MapMethods(
                    route.CatchAllPattern,
                    methods,
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
