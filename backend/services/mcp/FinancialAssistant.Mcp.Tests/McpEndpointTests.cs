using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using FinancialAssistant.Mcp.Api.Tools;
using FinancialAssistant.Mcp.Contracts;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using Xunit;

namespace FinancialAssistant.Mcp.Tests;

public sealed class McpEndpointTests(McpWebApplicationFactory factory) :
    IClassFixture<McpWebApplicationFactory>
{
    [Fact]
    public async Task HealthEndpoints_AreAvailableAndReady()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task McpInfo_RequiresTrustedHeaderAndAllowlistedRole()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/mcp/info")).StatusCode);

        using var unknownRole = Request("/mcp/info", "owner");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(unknownRole)).StatusCode);

        using var admin = Request("/mcp/info", McpRoles.Admin);
        var response = await client.SendAsync(admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var info = await response.Content.ReadFromJsonAsync<McpServiceInfoResponse>();
        Assert.Equal(6, Assert.IsType<McpServiceInfoResponse>(info).AllowlistedToolCount);
    }

    [Fact]
    public void ToolMethods_HaveMcpAndRoleAuthorizationMetadata()
    {
        var methods = typeof(FinancialAssistantMcpTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .ToArray();

        Assert.Equal(6, methods.Length);
        Assert.All(methods, method =>
            Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>()));
        var names = methods
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()!.Name!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new HashSet<string>(
                [
                    McpToolNames.SystemHealth,
                    McpToolNames.AiCostSummary,
                    McpToolNames.ParsingQuality,
                    McpToolNames.PromptEvalSummary,
                    McpToolNames.JiraIssueDraft,
                    McpToolNames.ArchitectureLookup
                ],
                StringComparer.Ordinal),
            names);
    }

    [Fact]
    public async Task ProtocolToolList_FiltersToolsByCallerRole()
    {
        using var httpClient = factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    [McpHeaders.Authentication] = McpWebApplicationFactory.SharedSecret,
                    [McpHeaders.Roles] = McpRoles.Developer,
                    [McpHeaders.CorrelationId] = "synthetic-protocol-correlation"
                }
            },
            httpClient,
            loggerFactory: null,
            ownsHttpClient: false);
        await using var client = await McpClient.CreateAsync(transport);

        var tools = await client.ListToolsAsync();
        var names = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            new HashSet<string>(
                [
                    McpToolNames.SystemHealth,
                    McpToolNames.PromptEvalSummary,
                    McpToolNames.JiraIssueDraft,
                    McpToolNames.ArchitectureLookup
                ],
                StringComparer.Ordinal),
            names);
        Assert.DoesNotContain(McpToolNames.AiCostSummary, names);
        Assert.DoesNotContain(McpToolNames.ParsingQuality, names);
    }

    private static HttpRequestMessage Request(string path, string roles)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(McpHeaders.Authentication, McpWebApplicationFactory.SharedSecret);
        request.Headers.Add(McpHeaders.Roles, roles);
        request.Headers.Add(McpHeaders.CorrelationId, "synthetic-endpoint-correlation");
        return request;
    }
}
