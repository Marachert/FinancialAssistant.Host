using FinancialAssistant.Identity.Application;
using FinancialAssistant.Identity.Infrastructure;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Identity.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
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
    app.MapOpenApi();
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
    "/identity/info",
    (IHostEnvironment environment, IOptions<IdentityServiceOptions> options) => Results.Ok(new
    {
        service = options.Value.ServiceName,
        status = "running",
        environment = environment.EnvironmentName,
        storageProvider = options.Value.Storage.Provider,
        eventPublishingMode = options.Value.Events.Mode
    }));

app.Run();

public partial class Program
{
}
