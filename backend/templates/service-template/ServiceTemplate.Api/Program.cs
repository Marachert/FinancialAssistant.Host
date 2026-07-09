using FinancialAssistant.ServiceTemplate.Api.Configuration;
using FinancialAssistant.ServiceTemplate.Api.Health;
using FinancialAssistant.ServiceTemplate.Api.Middleware;
using FinancialAssistant.ServiceTemplate.Contracts;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddOptions<ServiceOptions>()
    .Bind(builder.Configuration.GetRequiredSection(ServiceOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Name), "Service name is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Version), "Service version is required.")
    .ValidateOnStart();

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("The service process is running."),
        tags: ["live"])
    .AddCheck<ServiceReadinessHealthCheck>(
        "service_configuration",
        tags: ["ready"]);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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
        (IOptions<ServiceOptions> options, IHostEnvironment environment) =>
            Results.Ok(
                new ServiceInfoResponse(
                    options.Value.Name,
                    options.Value.Version,
                    environment.EnvironmentName)))
    .WithName("GetServiceInfo")
    .WithOpenApi();

app.Run();

public partial class Program;
