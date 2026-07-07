using System.Diagnostics;
using FinancialAssistant.PublicApiGateway.Routing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.Configure<GatewayRouteMapOptions>(builder.Configuration.GetSection("Gateway:RouteMap"));
builder.Services.Configure<GatewayDestinationMapOptions>(builder.Configuration.GetSection("Gateway:DestinationMap"));
builder.Services.AddSingleton<GatewayRouteCatalog>();
builder.Services.AddSingleton<GatewayDestinationCatalog>();
builder.Services.AddHttpClient<GatewayRequestDispatcher>();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/health"));

app.MapHealthChecks("/health");

app.MapGet("/gateway/info", (IHostEnvironment environment, GatewayRouteCatalog routeCatalog) => Results.Ok(new
{
    service = "financial-assistant-public-api-gateway",
    status = "running",
    environment = environment.EnvironmentName,
    routeCount = routeCatalog.Routes.Count,
    traceId = Activity.Current?.TraceId.ToString()
}));

app.MapGatewayRouteMap();

app.Run();
