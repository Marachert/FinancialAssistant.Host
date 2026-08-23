using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewayEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public GatewayEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task HealthEndpoint_WhenGatewayStarts_ReturnsHealthy()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RoutesEndpoint_ReturnsConfiguredSafeRouteMap()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/gateway/routes");
        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var routes = document.RootElement.GetProperty("routes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(17, routes.GetArrayLength());
        Assert.Contains(routes.EnumerateArray(), route =>
            route.GetProperty("routeKey").GetString() == "auth"
            && route.GetProperty("serviceOwner").GetString() == "Auth Service"
            && route.GetProperty("accessPolicy").GetString() == "authenticated");
        Assert.Contains(routes.EnumerateArray(), route =>
            route.GetProperty("routeKey").GetString() == "incomes"
            && route.GetProperty("serviceOwner").GetString() == "Income Service"
            && route.GetProperty("accessPolicy").GetString() == "authenticated"
            && route.GetProperty("status").GetString() == "active");
        Assert.Contains(routes.EnumerateArray(), route =>
            route.GetProperty("routeKey").GetString() == "expenses"
            && route.GetProperty("serviceOwner").GetString() == "Expense Service"
            && route.GetProperty("accessPolicy").GetString() == "authenticated"
            && route.GetProperty("status").GetString() == "active");
        Assert.Contains(routes.EnumerateArray(), route =>
            route.GetProperty("routeKey").GetString() == "admin-monitoring"
            && route.GetProperty("accessPolicy").GetString() == "admin");
        Assert.Contains(routes.EnumerateArray(), route =>
            route.GetProperty("routeKey").GetString() == "notification-preferences"
            && route.GetProperty("serviceOwner").GetString() == "Notification Service"
            && route.GetProperty("accessPolicy").GetString() == "authenticated"
            && route.GetProperty("methods").EnumerateArray()
                .Select(method => method.GetString())
                .SequenceEqual(["GET", "PUT"]));
        Assert.DoesNotContain("internalDestination", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("auth-service", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsLoadedConfigurationSummary()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/health/ready");
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ready", root.GetProperty("status").GetString());
        Assert.Equal(17, root.GetProperty("routeCount").GetInt32());
        Assert.Equal(12, root.GetProperty("destinationCount").GetInt32());
        Assert.Equal(2, root.GetProperty("enabledDestinationCount").GetInt32());
        Assert.Equal("placeholder", root.GetProperty("securityMode").GetString());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task NotificationPreferencesRoute_AllowsTrustedContractMethods(
        string method)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            "/notification-preferences");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Equal("authenticated", GetHeader(response, "X-Gateway-Access-Policy"));
    }

    [Fact]
    public async Task DraftRejectRoute_AllowsPost()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/transactions/drafts/synthetic-draft/reject");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Equal("authenticated", GetHeader(response, "X-Gateway-Access-Policy"));
    }

    [Fact]
    public async Task PlaceholderRoute_ReturnsSafeProblemWithoutInternalMetadata()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/categories");
        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Equal("route_not_active", root.GetProperty("code").GetString());
        Assert.Equal(501, root.GetProperty("status").GetInt32());
        Assert.DoesNotContain("category-service", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Category Service", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("authenticated", GetHeader(response, "X-Gateway-Access-Policy"));
        Assert.Equal("placeholder", GetHeader(response, "X-Gateway-Security-Mode"));
    }

    private HttpClient CreateClient()
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    private static string GetHeader(HttpResponseMessage response, string headerName)
    {
        Assert.True(response.Headers.TryGetValues(headerName, out var values));
        return Assert.Single(values);
    }
}
