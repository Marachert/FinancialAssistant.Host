using FinancialAssistant.Audit.Api.Endpoints;
using FinancialAssistant.Audit.Api.Health;
using FinancialAssistant.Audit.Api.Security;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Audit.Infrastructure;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.Observability;
using FinancialAssistant.Shared.Persistence;
using Microsoft.OpenApi.Models;

var builder = FinancialAssistantHost.CreateBuilder(args, "audit");

builder.AddFinancialAssistantObservability();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Financial Assistant Audit API",
            Version = "v1",
            Description = "Append-only privacy-safe event trace metadata."
        }));
var windowsNative = builder.Environment.IsEnvironment(FinancialAssistantHost.NativeEnvironment);
builder.Services.AddAuditInfrastructure(builder.Configuration, windowsNative);
builder.Services.AddSingleton<AuditGatewayAuthenticator>();
builder.Services.AddSingleton<AuditServiceAuthenticator>();
builder.Services.AddSingleton<AuditReadinessHealthCheck>();
builder.Services
    .AddFinancialAssistantHealthChecks()
    .AddCheck<AuditReadinessHealthCheck>("configuration", tags: ["ready"]);

var app = builder.Build();
if (windowsNative)
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        await next(context);
    });
    app.UseOwnedStorageFailureBoundary();
}

app.UseFinancialAssistantCorrelation();
_ = app.Services.GetRequiredService<AuditGatewayAuthenticator>();
_ = app.Services.GetRequiredService<AuditServiceAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Audit API v1"));
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapFinancialAssistantHealthEndpoints();
app.MapGet(
    "/audit/info",
    (IHostEnvironment environment) => Results.Ok(new AuditServiceInfoResponse(
        "financial-assistant-audit-service",
        "running",
        environment.EnvironmentName,
        windowsNative ? "append-only-PostgreSQL" : "append-only-in-memory-poc",
        "pseudonymous-audit-metadata-only")));
app.MapAuditEndpoints();

app.Run();

public partial class Program
{
}
