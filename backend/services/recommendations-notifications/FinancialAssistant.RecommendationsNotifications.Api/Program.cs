using FinancialAssistant.RecommendationsNotifications.Api.Endpoints;
using FinancialAssistant.RecommendationsNotifications.Api.Security;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Contracts;
using FinancialAssistant.RecommendationsNotifications.Infrastructure;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.Observability;
using FinancialAssistant.Shared.Persistence;
using Microsoft.OpenApi.Models;

var builder = FinancialAssistantHost.CreateBuilder(args, "recommendations-notifications");

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
var windowsNative = builder.Environment.IsEnvironment(FinancialAssistantHost.NativeEnvironment);
builder.Services.AddRecommendationNotificationInfrastructure(builder.Configuration, windowsNative);
builder.Services.AddSingleton<RecommendationNotificationGatewayAuthenticator>();
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
app.MapFinancialAssistantHealthEndpoints();
app.MapGet(
    "/recommendations-notifications/info",
    (IHostEnvironment environment) => Results.Ok(
        new RecommendationNotificationServiceInfoResponse(
            "financial-assistant-recommendations-notifications-service",
            "running",
            environment.EnvironmentName,
            windowsNative ? "PostgreSQL" : "in-memory",
            "analytics-and-score-events",
            "provider-neutral-preparation")));

app.MapRecommendationNotificationEndpoints();
app.Run();

public partial class Program
{
}
