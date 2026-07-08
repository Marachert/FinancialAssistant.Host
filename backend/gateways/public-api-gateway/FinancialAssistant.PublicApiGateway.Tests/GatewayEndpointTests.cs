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
        Assert.Equal(11, routes.GetArrayLength());
        Assert.Contains(routes.EnumerateArray(), route =>
            route.GetProperty("routeKey").GetString() == "auth"
            && route.GetProperty("serviceOwner").GetString() == "Auth Service"
            && route.GetProperty("accessPolicy").GetString() == "authenticated");
        Assert.Contains(routes.EnumerateArray(), route =>
            route.GetProperty("routeKey").GetString() == "admin-monitoring"
            && route.GetProperty("accessPolicy").GetString() == "admin");
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
        Assert.Equal(11, root.GetProperty("routeCount").GetInt32());
        Assert.Equal(10, root.GetProperty("destinationCount").GetInt32());
        Assert.Equal(0, root.GetProperty("enabledDestinationCount").GetInt32());
        Assert.Equal("placeholder", root.GetProperty("securityMode").GetString());
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
