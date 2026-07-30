using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.AiOrchestration.Api.Middleware;
using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public void EnabledProviderWithoutMatchingAdapter_FailsStartup()
    {
        using var factory = new AiOrchestrationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        CreateEnabledProviderConfiguration())));

        var exception = Assert.Throws<InvalidOperationException>(factory.CreateClient);

        Assert.Equal(
            "The configured AI provider adapter is not registered.",
            exception.Message);
    }

    [Fact]
    public async Task EnabledProviderWithMatchingAdapter_ReportsConfigured()
    {
        using var factory = new AiOrchestrationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        CreateEnabledProviderConfiguration()));
                builder.ConfigureServices(services =>
                    services.AddSingleton<ILlmProvider, SyntheticLlmProvider>());
            });
        using var configuredClient = factory.CreateClient();

        using var response = await configuredClient.GetAsync("/service/info");
        var info = await response.Content
            .ReadFromJsonAsync<AiOrchestrationServiceInfoResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(info);
        Assert.True(info.ProviderConfigured);
    }

    private static Dictionary<string, string?> CreateEnabledProviderConfiguration() =>
        new()
        {
            ["AiOrchestration:Provider:Enabled"] = "true",
            ["AiOrchestration:Provider:Mode"] = "sandbox",
            ["AiOrchestration:Provider:Name"] = "synthetic-provider",
            ["AiOrchestration:Provider:Model"] = "synthetic-model",
            ["AiOrchestration:Provider:Endpoint"] = "https://ai.invalid/v1",
            ["AiOrchestration:Provider:CredentialEnvironmentVariable"] =
                "FINANCIAL_ASSISTANT_TEST_AI_CREDENTIAL"
        };

    private sealed class SyntheticLlmProvider : ILlmProvider
    {
        public string Name => "synthetic-provider";

        public Task<LlmProviderResponse> CompleteAsync(
            LlmProviderRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("No provider call is needed by this test.");
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
