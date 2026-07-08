using FinancialAssistant.Identity.Api.Endpoints;
using FinancialAssistant.Identity.Api.RateLimiting;
using FinancialAssistant.Identity.Application;
using FinancialAssistant.Identity.Infrastructure;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Identity.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Financial Assistant Identity API",
            Version = "v1",
            Description = "Versioned client-facing authentication contracts for mobile and web clients."
        });
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Bearer access token issued by the Identity Service."
        });
});
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddIdentityRateLimiting(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Identity service process is running."),
        tags: new[] { "live" })
    .AddCheck<IdentityReadinessHealthCheck>(
        "identity-configuration",
        tags: new[] { "ready" });

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Identity API v1"));
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

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
    "/identity/info",
    (IHostEnvironment environment, IOptions<IdentityServiceOptions> options) => Results.Ok(new
    {
        service = options.Value.ServiceName,
        status = "running",
        environment = environment.EnvironmentName,
        storageProvider = options.Value.Storage.Provider,
        eventPublishingMode = options.Value.Events.Mode,
        rateLimitingEnabled = options.Value.RateLimiting.Enabled
    }));

app.MapIdentityContractEndpoints();

app.Run();

public partial class Program
{
}
