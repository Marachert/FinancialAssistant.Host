using System.Text.Json;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class ExpenseGatewayConfigurationTests
{
    [Fact]
    public void DefaultConfiguration_ExposesAuthenticatedExpenseRoute()
    {
        var gateway = ReadGatewayConfiguration();

        var destinations = gateway
            .GetProperty("DestinationMap")
            .GetProperty("Destinations")
            .EnumerateArray();
        var expenseDestination = destinations.Single(destination =>
            destination.GetProperty("DestinationKey").GetString() == "expense-service");

        Assert.True(expenseDestination.GetProperty("Enabled").GetBoolean());
        Assert.True(expenseDestination.GetProperty("RequiresGatewayAuthentication").GetBoolean());
        Assert.Equal("http://localhost:5112", expenseDestination.GetProperty("BaseAddress").GetString());

        var routes = gateway
            .GetProperty("RouteMap")
            .GetProperty("Routes")
            .EnumerateArray();
        var expenseRoute = routes.Single(route =>
            route.GetProperty("RouteKey").GetString() == "expenses");

        Assert.Equal("/expenses", expenseRoute.GetProperty("PublicPattern").GetString());
        Assert.Equal("/expenses/{**gatewayPath}", expenseRoute.GetProperty("CatchAllPattern").GetString());
        Assert.Equal("Expense Service", expenseRoute.GetProperty("ServiceOwner").GetString());
        Assert.Equal("expense-service", expenseRoute.GetProperty("InternalDestination").GetString());
        Assert.Equal("authenticated", expenseRoute.GetProperty("AccessPolicy").GetString());
        Assert.Equal("active", expenseRoute.GetProperty("Status").GetString());
        var methods = expenseRoute.GetProperty("Methods").EnumerateArray().ToArray();
        Assert.Collection(
            methods,
            method => Assert.Equal("GET", method.GetString()),
            method => Assert.Equal("POST", method.GetString()),
            method => Assert.Equal("PUT", method.GetString()));
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
