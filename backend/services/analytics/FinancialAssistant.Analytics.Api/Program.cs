using FinancialAssistant.Analytics.Api.Endpoints;
using FinancialAssistant.Analytics.Api.Security;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Contracts;
using FinancialAssistant.Analytics.Infrastructure;
using FinancialAssistant.Shared.Observability;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddFinancialAssistantObservability();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Financial Assistant Analytics API",
            Version = "v1",
            Description = "Deterministic event-derived dashboard analytics."
        });
});
builder.Services.AddAnalyticsApplication();
builder.Services.AddAnalyticsInfrastructure(builder.Configuration);
builder.Services.AddSingleton<AnalyticsGatewayAuthenticator>();
builder.Services.AddFinancialAssistantHealthChecks();

var app = builder.Build();

app.UseFinancialAssistantCorrelation();
_ = app.Services.GetRequiredService<AnalyticsGatewayAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Analytics API v1"));
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapFinancialAssistantHealthEndpoints();
app.MapGet(
    "/analytics/info",
    (IHostEnvironment environment) => Results.Ok(
        new AnalyticsServiceInfoResponse(
            "financial-assistant-analytics-service",
            "running",
            environment.EnvironmentName,
            "in-memory",
            "confirmed-income-expense-events")));

app.MapAnalyticsEndpoints();

app.Run();

public partial class Program
{
}
