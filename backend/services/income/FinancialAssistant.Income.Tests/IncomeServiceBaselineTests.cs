using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Income.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialAssistant.Income.Tests;

public sealed class IncomeServiceBaselineTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public IncomeServiceBaselineTests(WebApplicationFactory<Program> factory)
    {
        client = factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"))
            .CreateClient();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_ReturnHealthy(string route)
    {
        using var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InfoEndpoint_DeclaresIncomeOwnershipBoundary()
    {
        var response = await client.GetFromJsonAsync<IncomeServiceInfoResponse>("/income/info");

        Assert.NotNull(response);
        Assert.Equal("financial-assistant-income-service", response.Service);
        Assert.Equal("running", response.Status);
        Assert.Equal("Testing", response.Environment);
        Assert.Equal("in-memory", response.StorageProvider);
        Assert.Equal("confirmed_transaction", response.AuthoritativeInput);
    }

    [Fact]
    public async Task TestingEnvironment_ExposesOpenApiDocument()
    {
        using var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Financial Assistant Income API", document, StringComparison.Ordinal);
    }
}
