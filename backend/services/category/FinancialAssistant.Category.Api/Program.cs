using FinancialAssistant.Category.Api.Endpoints;
using FinancialAssistant.Category.Application;
using FinancialAssistant.Category.Infrastructure;
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
            Title = "Financial Assistant Category API",
            Version = "v1",
            Description = "Deterministic category taxonomy and user alias contracts owned by Category Service."
        });
});
builder.Services.AddCategoryApplication();
builder.Services.AddCategoryInfrastructure();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Category service process is running."),
        tags: new[] { "live", "ready" });

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Category API v1"));
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

app.MapGet("/category/info", (IHostEnvironment environment) => Results.Ok(new
{
    service = "financial-assistant-category-service",
    status = "running",
    environment = environment.EnvironmentName,
    storageProvider = "in-memory",
    eventPublisher = "in-memory"
}));

app.MapCategoryEndpoints();

app.Run();

public partial class Program
{
}
