using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.AiOrchestration.Api.Middleware;
using FinancialAssistant.AiOrchestration.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialAssistant.AiOrchestration.Tests;

public sealed class AiOrchestrationApiTests :
    IClassFixture<AiOrchestrationWebApplicationFactory>
{
    private readonly HttpClient client;

    public AiOrchestrationApiTests(AiOrchestrationWebApplicationFactory factory)
    {
        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task HealthAndInfo_ExposeSuggestionOnlyServiceBoundary()
    {
        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");
        using var infoResponse = await client.GetAsync("/service/info");
        var info = await infoResponse.Content
            .ReadFromJsonAsync<AiOrchestrationServiceInfoResponse>();

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, infoResponse.StatusCode);
        Assert.NotNull(info);
        Assert.Equal("FinancialAssistant.AiOrchestration", info.Name);
        Assert.Equal("suggestion", info.OutputAuthority);
        Assert.False(info.ProviderConfigured);
    }

    [Fact]
    public async Task CorrelationMiddleware_PreservesSafeCallerIdentifier()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "synthetic-correlation-105");

        using var response = await client.SendAsync(request);

        Assert.Equal(
            "synthetic-correlation-105",
            response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }
}

public sealed class AiOrchestrationWebApplicationFactory :
    WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
