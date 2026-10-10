using System.Net;
using FinancialAssistant.PublicApiGateway.Diagnostics;
using FinancialAssistant.PublicApiGateway.Routing;
using FinancialAssistant.PublicApiGateway.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class WindowsNativeGatewayTests
{
    [Fact]
    public void NativeProfile_ActivatesKnownRoutes_EnforcesJwt_AndFixesDestinations()
    {
        var configuration = LoadConfiguration();
        WindowsNativeGatewayProfile.Apply(configuration);
        var security = configuration.GetSection("Gateway:Security").Get<GatewaySecurityOptions>()!;
        Assert.Equal("enforce", security.Mode);
        Assert.Equal("Authorization", security.AuthenticationHeaderName);
        Assert.Throws<InvalidOperationException>(() => new GatewayAccessTokenValidator(Options.Create(security)));
        var routes = configuration.GetSection("Gateway:RouteMap").Get<GatewayRouteMapOptions>()!;
        Assert.Equal(18, routes.Routes.Length);
        Assert.All(routes.Routes, route => Assert.Equal("active", route.Status));
        var destinations = configuration.GetSection("Gateway:DestinationMap").Get<GatewayDestinationMapOptions>()!;
        var catalog = new GatewayDestinationCatalog(Options.Create(destinations));
        Assert.Equal(13, catalog.Destinations.Count);
        Assert.All(catalog.Destinations, destination =>
        {
            Assert.True(destination.Enabled);
            Assert.StartsWith("http://127.0.0.1:", destination.BaseAddress);
            Assert.Equal(destination.DestinationKey != "auth-service", destination.RequiresGatewayAuthentication);
        });
        Assert.True(catalog.TryGetBaseAddress("audit-service", out var audit));
        Assert.Equal("http://127.0.0.1:5113/", audit.AbsoluteUri);
        Assert.True(catalog.TryGetBaseAddress("notification-service", out var notifications));
        Assert.Equal("http://127.0.0.1:5108/", notifications.AbsoluteUri);
    }

    [Fact]
    public void DefaultProfile_RemainsPlaceholder()
    {
        var configuration = LoadConfiguration();
        Assert.Equal("placeholder", configuration["Gateway:Security:Mode"]);
        Assert.Equal("placeholder", configuration["Gateway:RouteMap:Routes:0:Status"]);
    }

    [Fact]
    public void UnknownDestination_IsRejected()
    {
        var configuration = LoadConfiguration();
        configuration["Gateway:DestinationMap:Destinations:0:DestinationKey"] = "foreign-service";
        Assert.Throws<InvalidOperationException>(() => WindowsNativeGatewayProfile.Apply(configuration));
    }

    [Theory]
    [InlineData("Gateway:Security:PublicEndpoints:0:Path", "/incomes")]
    [InlineData("Gateway:Security:PublicEndpoints:0:Method", "GET")]
    [InlineData("Gateway:RouteMap:Routes:0:InternalDestination", "income-service")]
    [InlineData("Gateway:RouteMap:Routes:0:RouteKey", "foreign-route")]
    public void NativeTrustCatalogTampering_IsRejected(string key, string value)
    {
        var configuration = LoadConfiguration();
        configuration[key] = value;
        Assert.Throws<InvalidOperationException>(() => WindowsNativeGatewayProfile.Apply(configuration));
    }

    [Theory]
    [InlineData(200, false, true)]
    [InlineData(503, false, false)]
    [InlineData(302, false, false)]
    [InlineData(200, true, false)]
    public async Task NativeReadiness_RequiresDownstreamSuccess_WithoutInspectingPrivateBodies(int code, bool fail, bool healthy)
    {
        var configuration = LoadConfiguration();
        WindowsNativeGatewayProfile.Apply(configuration);
        var options = configuration.GetSection("Gateway:DestinationMap").Get<GatewayDestinationMapOptions>()!;
        var factory = new SyntheticClientFactory(code, fail);
        var check = new NativeGatewayReadinessHealthCheck(factory, new GatewayDestinationCatalog(Options.Create(options)));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(healthy ? HealthStatus.Healthy : HealthStatus.Unhealthy, result.Status);
        Assert.Equal(12, factory.Handler.RequestCount);
        Assert.DoesNotContain("synthetic-private-body", result.Description!);
        Assert.Empty(result.Data);
        Assert.Null(result.Exception);
    }

    private static ConfigurationManager LoadConfiguration()
    {
        var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        return configuration;
    }

    private sealed class SyntheticClientFactory(int code, bool fail) : IHttpClientFactory
    {
        public SyntheticHandler Handler { get; } = new(code, fail);
        public HttpClient CreateClient(string name) => new(Handler, disposeHandler: false);
    }

    private sealed class SyntheticHandler(int code, bool fail) : HttpMessageHandler
    {
        private int count;
        public int RequestCount => count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref count);
            Assert.Equal("/health/ready", request.RequestUri!.AbsolutePath);
            Assert.Equal("127.0.0.1", request.RequestUri.Host);
            Assert.Null(request.Headers.Authorization);
            if (fail) { throw new HttpRequestException("synthetic private failure must not be published"); }
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)code)
            {
                Content = new StringContent("synthetic-private-body")
            });
        }
    }
}
