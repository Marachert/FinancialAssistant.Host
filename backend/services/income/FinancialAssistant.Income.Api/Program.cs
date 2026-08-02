using FinancialAssistant.Income.Api.Endpoints;
using FinancialAssistant.Income.Api.Security;
using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Contracts;
using FinancialAssistant.Income.Infrastructure;
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
            Title = "Financial Assistant Income API",
            Version = "v1",
            Description = "Confirmed income records owned by the Income Service."
        });
});
builder.Services.AddIncomeApplication();
builder.Services.AddIncomeInfrastructure();
builder.Services.AddSingleton<IncomeGatewayAuthenticator>();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Income service process is running."),
        tags: new[] { "live", "ready" });

var app = builder.Build();

_ = app.Services.GetRequiredService<IncomeGatewayAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Income API v1"));
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
    "/income/info",
    (IHostEnvironment environment) => Results.Ok(
        new IncomeServiceInfoResponse(
            "financial-assistant-income-service",
            "running",
            environment.EnvironmentName,
            "in-memory",
            "confirmed_or_manual")));

app.MapIncomeEndpoints();

app.Run();

public partial class Program
{
}
