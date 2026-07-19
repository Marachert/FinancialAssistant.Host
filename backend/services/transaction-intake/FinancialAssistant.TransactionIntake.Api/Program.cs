using FinancialAssistant.TransactionIntake.Api.Endpoints;
using FinancialAssistant.TransactionIntake.Api.Security;
using FinancialAssistant.TransactionIntake.Application;
using FinancialAssistant.TransactionIntake.Infrastructure;
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
            Title = "Financial Assistant Transaction Intake API",
            Version = "v1",
            Description = "Natural-language transaction intake and reviewable draft contracts."
        });
});
builder.Services.AddTransactionIntakeApplication();
builder.Services.AddTransactionIntakeInfrastructure();
builder.Services.AddSingleton<TransactionIntakeGatewayAuthenticator>();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Transaction Intake service process is running."),
        tags: new[] { "live", "ready" });

var app = builder.Build();

_ = app.Services.GetRequiredService<TransactionIntakeGatewayAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Transaction Intake API v1"));
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
app.MapGet("/transaction-intake/info", (IHostEnvironment environment) => Results.Ok(new
{
    service = "financial-assistant-transaction-intake-service",
    status = "running",
    environment = environment.EnvironmentName,
    parserProvider = "deterministic-development-adapter",
    storageProvider = "in-memory"
}));

app.MapTransactionIntakeEndpoints();

app.Run();

public partial class Program
{
}
