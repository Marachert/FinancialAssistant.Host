using System.Text.Json;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class TransactionIntakeGatewayConfigurationTests
{
    [Fact]
    public void DefaultConfiguration_ExposesAuthenticatedDraftReviewRoutes()
    {
        var routes = ReadGatewayConfiguration()
            .GetProperty("RouteMap")
            .GetProperty("Routes")
            .EnumerateArray()
            .ToArray();

        var draftRoute = routes.Single(route =>
            route.GetProperty("RouteKey").GetString() == "transaction-draft-review");
        AssertRoute(
            draftRoute,
            "/transactions/drafts/{id}",
            ["GET", "PUT"]);

        var receiptDraftRoute = routes.Single(route =>
            route.GetProperty("RouteKey").GetString() == "transaction-receipt-draft");
        AssertRoute(
            receiptDraftRoute,
            "/transactions/drafts/receipts/{receiptId}",
            ["GET"]);

        var rejectDraftRoute = routes.Single(route =>
            route.GetProperty("RouteKey").GetString() == "transaction-draft-reject");
        AssertRoute(
            rejectDraftRoute,
            "/transactions/drafts/{id}/reject",
            ["POST"]);
    }

    private static void AssertRoute(
        JsonElement route,
        string publicPattern,
        string[] expectedMethods)
    {
        Assert.Equal(publicPattern, route.GetProperty("PublicPattern").GetString());
        Assert.Equal("Transaction Intake Service", route.GetProperty("ServiceOwner").GetString());
        Assert.Equal("transaction-intake-service", route.GetProperty("InternalDestination").GetString());
        Assert.Equal("authenticated", route.GetProperty("AccessPolicy").GetString());
        Assert.Equal("placeholder", route.GetProperty("Status").GetString());
        Assert.Equal(
            expectedMethods,
            route.GetProperty("Methods")
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
