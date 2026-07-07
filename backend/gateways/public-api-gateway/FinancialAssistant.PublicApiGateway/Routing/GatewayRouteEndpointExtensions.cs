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
            app.MapMethods(route.PublicPattern, methods, (HttpContext context) => CreateRouteResponse(context, route));

            if (!string.IsNullOrWhiteSpace(route.CatchAllPattern))
            {
                app.MapMethods(route.CatchAllPattern, methods, (HttpContext context) => CreateRouteResponse(context, route));
            }
        }

        return app;
    }

    private static IResult CreateRouteResponse(HttpContext context, GatewayRouteDefinition route)
    {
        return Results.Json(new
        {
            status = route.Status,
            routeKey = route.RouteKey,
            publicPath = context.Request.Path.Value,
            serviceOwner = route.ServiceOwner,
            internalDestination = route.InternalDestination,
            accessPolicy = route.AccessPolicy,
            message = "Route configured. Service integration will be added later."
        }, statusCode: StatusCodes.Status501NotImplemented);
    }
}
