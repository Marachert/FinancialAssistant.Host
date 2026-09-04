using FinancialAssistant.AiOrchestration.Api.Configuration;
using FinancialAssistant.AiOrchestration.Api.Health;
using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Contracts;
using FinancialAssistant.AiOrchestration.Infrastructure;
using FinancialAssistant.AiOrchestration.Infrastructure.Prompts;
using FinancialAssistant.Shared.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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
            Title = "Financial Assistant AI Orchestration API",
            Version = "v1",
            Description = "Suggestion-only AI orchestration service boundary."
        });
});
builder.Services
    .AddOptions<AiOrchestrationOptions>()
    .Bind(builder.Configuration.GetRequiredSection(AiOrchestrationOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Name),
        "AI service name is required.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Version),
        "AI service version is required.")
    .Validate(
        options => string.Equals(
            options.OutputAuthority,
            AiOrchestrationOptions.SuggestionAuthority,
            StringComparison.Ordinal),
        "AI output authority must remain suggestion-only.")
    .Validate(
        options => options.Provider.IsValidConfiguration,
        "The AI provider must either be disabled with empty placeholders or enabled with a valid mode, identity, HTTPS endpoint, and credential environment-variable reference.")
    .Validate(
        options => options.Provider.HasValidResilienceSettings,
        "AI provider timeout and retry settings are outside the allowed range.")
    .Validate(
        options => options.Provider.UsageCostControls.IsValid,
        "AI usage cost-control settings are outside the allowed range or disable required admin visibility.")
    .ValidateOnStart();

var configuredProvider = builder.Configuration
    .GetSection($"{AiOrchestrationOptions.SectionName}:Provider")
    .Get<AiProviderClientOptions>() ?? new AiProviderClientOptions();
if (configuredProvider.IsConfigured)
{
    builder.Services.AddSingleton(
        configuredProvider.CreateRoute(TransactionParsingPromptCatalog.PromptName));
}
builder.Services.AddSingleton(configuredProvider.UsageCostControls.CreatePolicy());

builder.Services.AddAiOrchestrationApplication();
builder.Services.AddAiOrchestrationInfrastructure();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("The AI orchestration process is running."),
        tags: new[] { "live" })
    .AddCheck<AiOrchestrationReadinessHealthCheck>(
        "ai_boundary_configuration",
        tags: new[] { "ready" });

var app = builder.Build();

var runtimeProviderOptions = app.Services
    .GetRequiredService<IOptions<AiOrchestrationOptions>>()
    .Value
    .Provider;
if (runtimeProviderOptions.Enabled &&
    !app.Services.GetServices<ILlmProvider>().Any(provider =>
        string.Equals(
            provider.Name,
            runtimeProviderOptions.Name,
            StringComparison.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException(
        "The configured AI provider adapter is not registered.");
}

app.UseFinancialAssistantCorrelation();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Financial Assistant AI Orchestration API v1"));
}

app.MapGet("/", () => Results.Redirect("/health/ready"));
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
        "/service/info",
        (IOptions<AiOrchestrationOptions> options, IHostEnvironment environment) =>
            Results.Ok(
                new AiOrchestrationServiceInfoResponse(
                    options.Value.Name,
                    options.Value.Version,
                    environment.EnvironmentName,
                    options.Value.OutputAuthority,
                    options.Value.Provider.IsConfigured)))
    .WithName("GetAiOrchestrationServiceInfo")
    .Produces<AiOrchestrationServiceInfoResponse>();

app.Run();

public partial class Program
{
}
