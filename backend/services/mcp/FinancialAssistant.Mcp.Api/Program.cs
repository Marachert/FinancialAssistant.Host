using FinancialAssistant.Mcp.Api.Health;
using FinancialAssistant.Mcp.Api.Security;
using FinancialAssistant.Mcp.Api.Tools;
using FinancialAssistant.Mcp.Application;
using FinancialAssistant.Mcp.Contracts;
using FinancialAssistant.Mcp.Infrastructure;
using FinancialAssistant.Shared.Observability;
using Microsoft.AspNetCore.Authentication;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.AddFinancialAssistantObservability();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMcpInfrastructure(builder.Configuration);
builder.Services
    .AddAuthentication(McpHeaderAuthenticationHandler.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, McpHeaderAuthenticationHandler>(
        McpHeaderAuthenticationHandler.AuthenticationScheme,
        _ => { });
builder.Services.AddAuthorization();
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .AddAuthorizationFilters()
    .WithTools<FinancialAssistantMcpTools>();
builder.Services.AddSingleton<McpReadinessHealthCheck>();
builder.Services
    .AddFinancialAssistantHealthChecks()
    .AddCheck<McpReadinessHealthCheck>("configuration", tags: ["ready"]);

var app = builder.Build();

app.UseFinancialAssistantCorrelation();
app.UseMiddleware<McpRequestAuditMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/health"));
app.MapFinancialAssistantHealthEndpoints();
app.MapGet(
        "/mcp/info",
        (IHostEnvironment environment, McpToolRegistry registry) => Results.Ok(
            new McpServiceInfoResponse(
                "financial-assistant-mcp-service",
                "running",
                environment.EnvironmentName,
                registry.All.Count,
                "aggregate-operational-only")))
    .RequireAuthorization();
app.MapMcp("/mcp").RequireAuthorization();

app.Run();

public partial class Program
{
}
