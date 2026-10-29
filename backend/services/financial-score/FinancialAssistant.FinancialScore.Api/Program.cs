using FinancialAssistant.FinancialScore.Api.Endpoints;
using FinancialAssistant.FinancialScore.Api.Security;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Contracts;
using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.FinancialScore.Infrastructure;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.Observability;
using FinancialAssistant.Shared.Persistence;
using Microsoft.OpenApi.Models;

var builder = FinancialAssistantHost.CreateBuilder(args, "financial-score");

builder.AddFinancialAssistantObservability();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Financial Assistant Financial Score API",
            Version = "v1",
            Description = "Deterministic financial score and transparent factor history."
        });
});
builder.Services.AddFinancialScoreApplication();
var windowsNative = builder.Environment.IsEnvironment(FinancialAssistantHost.NativeEnvironment);
builder.Services.AddFinancialScoreInfrastructure(builder.Configuration, windowsNative);
builder.Services.AddSingleton<FinancialScoreGatewayAuthenticator>();
builder.Services.AddFinancialAssistantHealthChecks();

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
_ = app.Services.GetRequiredService<FinancialScoreGatewayAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Financial Score API v1"));
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapFinancialAssistantHealthEndpoints();
app.MapGet(
    "/financial-score/info",
    (IHostEnvironment environment) => Results.Ok(
        new FinancialScoreServiceInfoResponse(
            "financial-assistant-financial-score-service",
            "running",
            environment.EnvironmentName,
            windowsNative ? "PostgreSQL" : "in-memory",
            FinancialScoreFormula.Version)));

app.MapFinancialScoreEndpoints();
app.Run();

public partial class Program
{
}
