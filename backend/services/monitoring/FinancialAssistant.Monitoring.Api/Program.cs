using FinancialAssistant.Monitoring.Api.Endpoints;
using FinancialAssistant.Monitoring.Api.Health;
using FinancialAssistant.Monitoring.Api.Security;
using FinancialAssistant.Monitoring.Contracts;
using FinancialAssistant.Monitoring.Infrastructure;
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
            Title = "Financial Assistant Monitoring API",
            Version = "v1",
            Description = "Privacy-safe aggregate operational health and quality signals."
        });
});
builder.Services.AddMonitoringInfrastructure(builder.Configuration);
builder.Services.AddSingleton<MonitoringGatewayAuthenticator>();
builder.Services.AddSingleton<MonitoringSignalAuthenticator>();
builder.Services.AddSingleton<MonitoringReadinessHealthCheck>();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Monitoring service process is running."),
        tags: ["live", "ready"])
    .AddCheck<MonitoringReadinessHealthCheck>("configuration", tags: ["ready"]);

var app = builder.Build();

_ = app.Services.GetRequiredService<MonitoringGatewayAuthenticator>();
_ = app.Services.GetRequiredService<MonitoringSignalAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Monitoring API v1"));
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapHealthChecks("/health");
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("live") });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("ready") });
app.MapGet(
    "/monitoring/info",
    (IHostEnvironment environment) => Results.Ok(
        new MonitoringServiceInfoResponse(
            "financial-assistant-monitoring-service",
            "running",
            environment.EnvironmentName,
            "aggregate-operational-only")));
app.MapMonitoringEndpoints();

app.Run();

public partial class Program
{
}
