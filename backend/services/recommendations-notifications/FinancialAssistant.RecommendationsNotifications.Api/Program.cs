using FinancialAssistant.RecommendationsNotifications.Api.Endpoints;
using FinancialAssistant.RecommendationsNotifications.Api.Security;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Contracts;
using FinancialAssistant.RecommendationsNotifications.Infrastructure;
using FinancialAssistant.Shared.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
            Title = "Financial Assistant Recommendations and Notifications API",
            Version = "v1",
            Description =
                "Deterministic financial recommendations and provider-neutral notification preparation."
        });
});
builder.Services.AddRecommendationNotificationApplication();
builder.Services.AddRecommendationNotificationInfrastructure(builder.Configuration);
builder.Services.AddSingleton<RecommendationNotificationGatewayAuthenticator>();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(
            "Recommendations and notifications service process is running."),
        tags: new[] { "live", "ready" });

var app = builder.Build();

app.UseFinancialAssistantCorrelation();
_ = app.Services.GetRequiredService<RecommendationNotificationGatewayAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Financial Assistant Recommendations and Notifications API v1"));
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
    "/recommendations-notifications/info",
    (IHostEnvironment environment) => Results.Ok(
        new RecommendationNotificationServiceInfoResponse(
            "financial-assistant-recommendations-notifications-service",
            "running",
            environment.EnvironmentName,
            "in-memory",
            "analytics-and-score-events",
            "provider-neutral-preparation")));

app.MapRecommendationNotificationEndpoints();
app.Run();

public partial class Program
{
}
