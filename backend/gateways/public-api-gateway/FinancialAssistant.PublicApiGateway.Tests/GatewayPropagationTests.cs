using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewayPropagationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public GatewayPropagationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task RequestWithoutCorrelationId_GeneratesMatchingResponseHeaders()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        var primary = GetHeader(response, "correlationId");
        var compatibility = GetHeader(response, "X-Correlation-Id");
        var traceId = GetHeader(response, "X-Trace-Id");
        var serverTiming = GetHeader(response, "Server-Timing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(primary));
        Assert.Equal(primary, compatibility);
        Assert.True(Guid.TryParse(primary, out _));
        Assert.Equal(32, traceId.Length);
        Assert.StartsWith("gateway;dur=", serverTiming, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestWithCorrelationId_PropagatesValueToResponseAndPayload()
    {
        const string correlationId = "fin-265-synthetic-correlation";
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/gateway/info");
        request.Headers.TryAddWithoutValidation("correlationId", correlationId);

        using var response = await client.SendAsync(request);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(correlationId, GetHeader(response, "correlationId"));
        Assert.Equal(correlationId, GetHeader(response, "X-Correlation-Id"));
        Assert.Equal(correlationId, document.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal(document.RootElement.GetProperty("traceId").GetString(), GetHeader(response, "X-Trace-Id"));
    }

    [Fact]
    public async Task RequestWithTraceParent_UsesIncomingTraceId()
    {
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        const string traceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/gateway/info");
        request.Headers.TryAddWithoutValidation("traceparent", traceParent);

        using var response = await client.SendAsync(request);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(traceId, document.RootElement.GetProperty("traceId").GetString());
        Assert.Equal(traceId, GetHeader(response, "X-Trace-Id"));
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
