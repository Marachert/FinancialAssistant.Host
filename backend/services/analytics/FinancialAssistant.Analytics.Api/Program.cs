using FinancialAssistant.Analytics.Api.Endpoints;
using FinancialAssistant.Analytics.Api.Security;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Contracts;
using FinancialAssistant.Analytics.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Analytics service process is running."),
        tags: new[] { "live", "ready" });

var app = builder.Build();

_ = app.Services.GetRequiredService<AnalyticsGatewayAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Analytics API v1"));
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapHealthChecks("/health");
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live")
    });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready")
    });
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
