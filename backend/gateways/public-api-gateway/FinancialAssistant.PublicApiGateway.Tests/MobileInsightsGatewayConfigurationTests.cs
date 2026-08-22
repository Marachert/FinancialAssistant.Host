using System.Text.Json;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class MobileInsightsGatewayConfigurationTests
{
    [Fact]
    public void DefaultConfiguration_ProtectsMobileInsightDestinationsWithGatewayAuthentication()
    {
        var gateway = ReadGatewayConfiguration();
        var destinations = gateway
            .GetProperty("DestinationMap")
            .GetProperty("Destinations")
            .EnumerateArray()
            .ToDictionary(destination => destination.GetProperty("DestinationKey").GetString()!);

        foreach (var destinationKey in new[]
                 {
                     "analytics-service",
                     "financial-score-service",
                     "recommendation-service",
                     "notification-service",
                 })
        {
            Assert.True(destinations[destinationKey].GetProperty("RequiresGatewayAuthentication").GetBoolean());
        }
    }

    [Fact]
    public void DefaultConfiguration_ExposesMobileInsightAliasesExpectedByTheServices()
    {
        var gateway = ReadGatewayConfiguration();
        var routes = gateway
            .GetProperty("RouteMap")
            .GetProperty("Routes")
            .EnumerateArray()
            .ToDictionary(route => route.GetProperty("RouteKey").GetString()!);

        AssertRoute(routes["analytics"], "/analytics", "/analytics/{**gatewayPath}", "GET");
        AssertRoute(
            routes["score"],
            "/financial-score",
            "/financial-score/{**gatewayPath}",
            "GET");
        AssertRoute(
            routes["recommendations"],
            "/recommendations",
            "/recommendations/{**gatewayPath}",
            "GET");
        AssertRoute(routes["notification-preferences"], "/notification-preferences", null, "GET", "PUT");
    }

    private static void AssertRoute(
        JsonElement route,
        string publicPattern,
        string? catchAllPattern,
        params string[] methods)
    {
        Assert.Equal(publicPattern, route.GetProperty("PublicPattern").GetString());
        if (catchAllPattern is null)
        {
            Assert.False(route.TryGetProperty("CatchAllPattern", out _));
        }
        else
        {
            Assert.Equal(catchAllPattern, route.GetProperty("CatchAllPattern").GetString());
        }

        Assert.Equal("authenticated", route.GetProperty("AccessPolicy").GetString());
        Assert.Equal(
            methods,
            route.GetProperty("Methods").EnumerateArray().Select(method => method.GetString()).ToArray());
    }

    private static JsonElement ReadGatewayConfiguration()
    {
        var path = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "../../../../FinancialAssistant.PublicApiGateway/appsettings.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("Gateway").Clone();
    }
}
