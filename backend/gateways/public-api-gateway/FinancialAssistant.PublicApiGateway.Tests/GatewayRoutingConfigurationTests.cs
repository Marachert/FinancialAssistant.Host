using System.Text.Json;
using FinancialAssistant.PublicApiGateway.Routing;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewayRoutingConfigurationTests
{
    [Fact]
    public void RouteCatalog_WhenRouteKeyIsDuplicated_FailsFast()
    {
        var options = Options.Create(new GatewayRouteMapOptions
        {
            Routes =
            [
                CreateRoute("auth", "/auth"),
                CreateRoute("auth", "/auth-copy")
            ]
        });

        var exception = Assert.Throws<InvalidOperationException>(() => new GatewayRouteCatalog(options));

        Assert.Contains("Duplicate gateway route key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RouteCatalog_WhenMethodsAreNotExplicit_FailsFast()
    {
        var route = CreateRoute("auth", "/auth");
        route = new GatewayRouteDefinition
        {
            RouteKey = route.RouteKey,
            PublicPattern = route.PublicPattern,
            ServiceOwner = route.ServiceOwner,
            InternalDestination = route.InternalDestination,
            AccessPolicy = route.AccessPolicy,
            Status = route.Status,
            Methods = Array.Empty<string>()
        };
        var options = Options.Create(new GatewayRouteMapOptions { Routes = [route] });

        var exception = Assert.Throws<InvalidOperationException>(() => new GatewayRouteCatalog(options));

        Assert.Contains("explicit HTTP methods", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RouteCatalog_PublicDescriptors_DoNotExposeInternalDestinationKeys()
    {
        var catalog = new GatewayRouteCatalog(
            Options.Create(new GatewayRouteMapOptions
            {
                Routes = [CreateRoute("auth", "/auth")]
            }));

        var json = JsonSerializer.Serialize(catalog.PublicRoutes);

        Assert.DoesNotContain("internalDestination", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("auth-service", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Auth Service", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DestinationCatalog_WhenEnabledDestinationHasNoAddress_FailsFast()
    {
        var options = Options.Create(new GatewayDestinationMapOptions
        {
            Destinations =
            [
                new GatewayDestinationDefinition
                {
                    DestinationKey = "auth-service",
                    Enabled = true,
                    BaseAddress = string.Empty
                }
            ]
        });

        var exception = Assert.Throws<InvalidOperationException>(() => new GatewayDestinationCatalog(options));

        Assert.Contains("requires a base address", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ftp://identity.internal")]
    [InlineData("https://user:password@identity.internal")]
    [InlineData("https://identity.internal?secret=value")]
    public void DestinationCatalog_WhenAddressIsUnsafe_FailsFast(string address)
    {
        var options = Options.Create(new GatewayDestinationMapOptions
        {
            Destinations =
            [
                new GatewayDestinationDefinition
                {
                    DestinationKey = "auth-service",
                    Enabled = true,
                    BaseAddress = address
                }
            ]
        });

        var exception = Assert.Throws<InvalidOperationException>(() => new GatewayDestinationCatalog(options));

        Assert.Contains("invalid base address", exception.Message, StringComparison.Ordinal);
    }

    private static GatewayRouteDefinition CreateRoute(string routeKey, string pattern) =>
        new()
        {
            RouteKey = routeKey,
            PublicPattern = pattern,
            ServiceOwner = "Auth Service",
            InternalDestination = "auth-service",
            AccessPolicy = "public",
            Status = "placeholder",
            Methods = ["GET", "POST"]
        };
}
