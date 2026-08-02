using System.Text.Json;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class IncomeGatewayConfigurationTests
{
    [Fact]
    public void DefaultConfiguration_ExposesAuthenticatedIncomeRoute()
    {
        var gateway = ReadGatewayConfiguration();

        var destinations = gateway
            .GetProperty("DestinationMap")
            .GetProperty("Destinations")
            .EnumerateArray();
        var incomeDestination = destinations.Single(destination =>
            destination.GetProperty("DestinationKey").GetString() == "income-service");

        Assert.True(incomeDestination.GetProperty("Enabled").GetBoolean());
        Assert.True(incomeDestination.GetProperty("RequiresGatewayAuthentication").GetBoolean());
        Assert.Equal("http://localhost:5111", incomeDestination.GetProperty("BaseAddress").GetString());

        var routes = gateway
            .GetProperty("RouteMap")
            .GetProperty("Routes")
            .EnumerateArray();
        var incomeRoute = routes.Single(route =>
            route.GetProperty("RouteKey").GetString() == "incomes");

        Assert.Equal("/incomes", incomeRoute.GetProperty("PublicPattern").GetString());
        Assert.Equal("/incomes/{**gatewayPath}", incomeRoute.GetProperty("CatchAllPattern").GetString());
        Assert.Equal("Income Service", incomeRoute.GetProperty("ServiceOwner").GetString());
        Assert.Equal("income-service", incomeRoute.GetProperty("InternalDestination").GetString());
        Assert.Equal("authenticated", incomeRoute.GetProperty("AccessPolicy").GetString());
        Assert.Equal("active", incomeRoute.GetProperty("Status").GetString());
        Assert.Equal(
            ["GET", "POST", "PUT"],
            incomeRoute.GetProperty("Methods")
                .EnumerateArray()
                .Select(method => method.GetString())
                .ToArray());
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
