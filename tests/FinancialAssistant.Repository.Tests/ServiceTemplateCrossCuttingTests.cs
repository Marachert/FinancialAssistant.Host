using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialAssistant.ServiceTemplate.Contracts;
using FinancialAssistant.Shared.Observability;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class ServiceTemplateCrossCuttingTests(
    WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task HealthEndpoints_ReportLiveAndReady()
    {
        using var application = factory.WithWebHostBuilder(
            builder => builder.UseEnvironment(Environments.Development));
        using var client = application.CreateClient();

        var liveResponse = await client.GetAsync("/health/live");
        var readyResponse = await client.GetAsync("/health/ready");
        var live = await liveResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ready = await readyResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        Assert.Equal("healthy", live.GetProperty("status").GetString());
        Assert.Equal("healthy", ready.GetProperty("status").GetString());
        Assert.Single(live.GetProperty("checks").EnumerateArray());
        Assert.Equal(2, ready.GetProperty("checks").GetArrayLength());
    }

    [Fact]
    public async Task OpenApi_IsAvailableOnlyInDevelopment()
    {
        using var developmentApplication = factory.WithWebHostBuilder(
            builder => builder.UseEnvironment(Environments.Development));
        using var developmentClient = developmentApplication.CreateClient();

        var developmentResponse = await developmentClient.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, developmentResponse.StatusCode);

        using var productionApplication = factory.WithWebHostBuilder(
            builder => builder.UseEnvironment(Environments.Production));
        using var productionClient = productionApplication.CreateClient();

        var productionResponse = await productionClient.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.NotFound, productionResponse.StatusCode);
    }

    [Fact]
    public async Task CorrelationMiddleware_PreservesOrGeneratesIdentifier()
    {
        using var application = factory.WithWebHostBuilder(
            builder => builder.UseEnvironment(Environments.Development));
        using var client = application.CreateClient();

        const string suppliedCorrelationId = "fin-52-integration-test";
        using var suppliedRequest = new HttpRequestMessage(HttpMethod.Get, "/service/info");
        suppliedRequest.Headers.TryAddWithoutValidation(
            ObservabilityHeaders.CorrelationId,
            suppliedCorrelationId);

        using var suppliedResponse = await client.SendAsync(suppliedRequest);
        Assert.Equal(HttpStatusCode.OK, suppliedResponse.StatusCode);
        Assert.Equal(
            suppliedCorrelationId,
            suppliedResponse.Headers.GetValues(ObservabilityHeaders.CorrelationId).Single());

        using var generatedResponse = await client.GetAsync("/service/info");
        Assert.Equal(HttpStatusCode.OK, generatedResponse.StatusCode);

        var generatedCorrelationId = generatedResponse.Headers
            .GetValues(ObservabilityHeaders.CorrelationId)
            .Single();
        Assert.True(Guid.TryParseExact(generatedCorrelationId, "N", out _));
    }

    [Fact]
    public async Task ServiceInfo_UsesValidatedTypedOptions()
    {
        using var application = factory.WithWebHostBuilder(
            builder => builder.UseEnvironment(Environments.Development));
        using var client = application.CreateClient();

        var response = await client.GetAsync("/service/info");
        var serviceInfo = await response.Content.ReadFromJsonAsync<ServiceInfoResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(serviceInfo);
        Assert.Equal("FinancialAssistant.ServiceTemplate", serviceInfo.Name);
        Assert.Equal("1.0.0", serviceInfo.Version);
        Assert.Equal(Environments.Development, serviceInfo.Environment);
    }

    [Fact]
    public void CrossCuttingSource_ContainsLoggingOptionsAndHealthBoundaries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var program = ReadRequiredFile(
            repositoryRoot,
            "backend/templates/service-template/ServiceTemplate.Api/Program.cs");
        var middleware = ReadRequiredFile(
            repositoryRoot,
            "backend/shared/observability/FinancialAssistant.Shared.Observability/FinancialAssistantCorrelationMiddleware.cs");
        var options = ReadRequiredFile(
            repositoryRoot,
            "backend/templates/service-template/ServiceTemplate.Api/Configuration/ServiceOptions.cs");
        var appSettings = ReadRequiredFile(
            repositoryRoot,
            "backend/templates/service-template/ServiceTemplate.Api/appsettings.json");

        var requiredProgramPhrases = new[]
        {
            "AddFinancialAssistantObservability",
            "ValidateOnStart",
            "AddFinancialAssistantHealthChecks",
            "tags: [\"ready\"]",
            "MapFinancialAssistantHealthEndpoints",
            "app.Environment.IsDevelopment()",
            "app.UseSwagger()",
            "app.UseFinancialAssistantCorrelation()"
        };

        foreach (var phrase in requiredProgramPhrases)
        {
            Assert.Contains(phrase, program, StringComparison.Ordinal);
        }

        Assert.Contains("ObservabilityHeaders.CorrelationId", middleware, StringComparison.Ordinal);
        Assert.Contains("ObservabilityHeaders.TraceId", middleware, StringComparison.Ordinal);
        Assert.Contains("BeginScope", middleware, StringComparison.Ordinal);
        Assert.Contains("context.TraceIdentifier", middleware, StringComparison.Ordinal);
        Assert.Contains("SectionName = \"Service\"", options, StringComparison.Ordinal);
        Assert.Contains("\"Name\": \"FinancialAssistant.ServiceTemplate\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Version\": \"1.0.0\"", appSettings, StringComparison.Ordinal);
    }

    private static string ReadRequiredFile(string repositoryRoot, string path)
    {
        var fullPath = ToRepositoryPath(repositoryRoot, path);
        Assert.True(File.Exists(fullPath), $"Required template file '{path}' is missing.");
        return File.ReadAllText(fullPath);
    }

    private static string ToRepositoryPath(string repositoryRoot, string path) =>
        Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing FinancialAssistant.Backend.sln.");
    }
}
