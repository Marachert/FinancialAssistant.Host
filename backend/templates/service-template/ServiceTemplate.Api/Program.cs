using FinancialAssistant.ServiceTemplate.Api.Configuration;
using FinancialAssistant.ServiceTemplate.Api.Health;
using FinancialAssistant.ServiceTemplate.Contracts;
using FinancialAssistant.Shared.Observability;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddFinancialAssistantObservability();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddOptions<ServiceOptions>()
    .Bind(builder.Configuration.GetRequiredSection(ServiceOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Name), "Service name is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Version), "Service version is required.")
    .ValidateOnStart();

builder.Services
    .AddFinancialAssistantHealthChecks()
    .AddCheck<ServiceReadinessHealthCheck>(
        "service_configuration",
        tags: ["ready"]);

var app = builder.Build();

app.UseFinancialAssistantCorrelation();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapFinancialAssistantHealthEndpoints();

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
