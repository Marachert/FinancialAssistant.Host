using FinancialAssistant.Income.Api.Endpoints;
using FinancialAssistant.Income.Api.Security;
using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Contracts;
using FinancialAssistant.Income.Infrastructure;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.Observability;
using FinancialAssistant.Shared.Persistence;
using Microsoft.OpenApi.Models;

var builder = FinancialAssistantHost.CreateBuilder(args, "income");

builder.AddFinancialAssistantObservability();
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
var windowsNative = builder.Environment.IsEnvironment(FinancialAssistantHost.NativeEnvironment);
builder.Services.AddIncomeInfrastructure(builder.Configuration, windowsNative);
builder.Services.AddSingleton<IncomeGatewayAuthenticator>();
builder.Services.AddFinancialAssistantHealthChecks();

var app = builder.Build();
if (windowsNative) { app.UseOwnedStorageFailureBoundary(); }

app.UseFinancialAssistantCorrelation();
_ = app.Services.GetRequiredService<IncomeGatewayAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Income API v1"));
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapFinancialAssistantHealthEndpoints();

app.MapGet(
    "/income/info",
    (IHostEnvironment environment) => Results.Ok(
        new IncomeServiceInfoResponse(
            "financial-assistant-income-service",
            "running",
            environment.EnvironmentName,
            windowsNative ? "PostgreSQL" : "in-memory",
            "confirmed_or_manual")));

app.MapIncomeEndpoints();

app.Run();

public partial class Program
{
}
