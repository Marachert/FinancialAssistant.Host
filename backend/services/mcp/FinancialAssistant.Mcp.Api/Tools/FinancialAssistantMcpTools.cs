using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using FinancialAssistant.Mcp.Api.Security;
using FinancialAssistant.Mcp.Application;
using FinancialAssistant.Mcp.Contracts;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace FinancialAssistant.Mcp.Api.Tools;

[McpServerToolType]
[Authorize]
public sealed class FinancialAssistantMcpTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = McpToolNames.SystemHealth)]
    [Description("Return privacy-safe aggregate service, RabbitMQ, and Elasticsearch health.")]
    [Authorize(Roles = "admin,operator,developer,qa")]
    public static async Task<string> GetSystemHealthAsync(
        ClaimsPrincipal caller,
        IHttpContextAccessor contextAccessor,
        McpToolExecutor executor,
        CancellationToken cancellationToken) =>
        Serialize(await executor.GetSystemHealthAsync(
            CreateContext(caller, contextAccessor),
            cancellationToken));

    [McpServerTool(Name = McpToolNames.AiCostSummary)]
    [Description("Return aggregate AI requests, tokens, and estimated cost without prompts or user data.")]
    [Authorize(Roles = "admin,operator")]
    public static async Task<string> GetAiCostSummaryAsync(
        ClaimsPrincipal caller,
        IHttpContextAccessor contextAccessor,
        McpToolExecutor executor,
        CancellationToken cancellationToken) =>
        Serialize(await executor.GetAiCostSummaryAsync(
            CreateContext(caller, contextAccessor),
            cancellationToken));

    [McpServerTool(Name = McpToolNames.ParsingQuality)]
    [Description("Return aggregate parsing success, review-required, and failure counters.")]
    [Authorize(Roles = "admin,operator,qa")]
    public static async Task<string> GetParsingQualityAsync(
        ClaimsPrincipal caller,
        IHttpContextAccessor contextAccessor,
        McpToolExecutor executor,
        CancellationToken cancellationToken) =>
        Serialize(await executor.GetParsingQualityAsync(
            CreateContext(caller, contextAccessor),
            cancellationToken));

    [McpServerTool(Name = McpToolNames.PromptEvalSummary)]
    [Description("Return aggregate prompt evaluation status; never return prompts or provider responses.")]
    [Authorize(Roles = "admin,developer,qa")]
    public static async Task<string> GetPromptEvalSummaryAsync(
        ClaimsPrincipal caller,
        IHttpContextAccessor contextAccessor,
        McpToolExecutor executor,
        CancellationToken cancellationToken) =>
        Serialize(await executor.GetPromptEvalSummaryAsync(
            CreateContext(caller, contextAccessor),
            cancellationToken));

    [McpServerTool(Name = McpToolNames.JiraIssueDraft)]
    [Description("Create a local FIN Jira issue draft. This tool never submits or mutates Jira.")]
    [Authorize(Roles = "admin,developer")]
    public static async Task<string> CreateJiraIssueDraftAsync(
        [Description("Bounded issue summary, at most 120 characters.")] string summary,
        [Description("Bounded implementation goal, at most 1000 characters.")] string goal,
        ClaimsPrincipal caller,
        IHttpContextAccessor contextAccessor,
        McpToolExecutor executor,
        CancellationToken cancellationToken) =>
        Serialize(await executor.CreateJiraIssueDraftAsync(
            CreateContext(caller, contextAccessor),
            summary,
            goal,
            cancellationToken));

    [McpServerTool(Name = McpToolNames.ArchitectureLookup)]
    [Description("Return an allowlisted architecture reference by exact key.")]
    [Authorize(Roles = "admin,operator,developer,qa")]
    public static async Task<string> GetArchitectureReferenceAsync(
        [Description("One of architecture-root, event-envelope, service-catalog, system-diagrams.")] string key,
        ClaimsPrincipal caller,
        IHttpContextAccessor contextAccessor,
        McpToolExecutor executor,
        CancellationToken cancellationToken) =>
        Serialize(await executor.GetArchitectureReferenceAsync(
            CreateContext(caller, contextAccessor),
            key,
            cancellationToken));

    private static McpCallContext CreateContext(
        ClaimsPrincipal caller,
        IHttpContextAccessor contextAccessor)
    {
        var context = contextAccessor.HttpContext
            ?? throw new InvalidOperationException("MCP HTTP context is unavailable.");
        var roles = caller.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();
        return new McpCallContext(McpRequestAuditMiddleware.GetCorrelationId(context), roles);
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
}
