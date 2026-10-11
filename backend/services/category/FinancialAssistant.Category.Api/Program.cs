using FinancialAssistant.Category.Api.Endpoints;
using FinancialAssistant.Category.Api.Security;
using FinancialAssistant.Category.Application;
using FinancialAssistant.Category.Infrastructure;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.Observability;
using FinancialAssistant.Shared.Persistence;
using Microsoft.OpenApi.Models;

var builder = FinancialAssistantHost.CreateBuilder(args, "category");

builder.AddFinancialAssistantObservability();
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
var windowsNative = builder.Environment.IsEnvironment(FinancialAssistantHost.NativeEnvironment);
builder.Services.AddCategoryInfrastructure(builder.Configuration, windowsNative);
builder.Services.AddSingleton<CategoryGatewayAuthenticator>();
builder.Services.AddFinancialAssistantHealthChecks();

var app = builder.Build();
if (windowsNative) { app.UseOwnedStorageFailureBoundary(); }

app.UseFinancialAssistantCorrelation();
_ = app.Services.GetRequiredService<CategoryGatewayAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Category API v1"));
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapFinancialAssistantHealthEndpoints();

app.MapGet("/category/info", (IHostEnvironment environment) => Results.Ok(new
{
    service = "financial-assistant-category-service",
    status = "running",
    environment = environment.EnvironmentName,
    storageProvider = windowsNative ? "PostgreSQL" : "in-memory",
    eventPublisher = windowsNative ? "durable-outbox-pending-transport" : "in-memory"
}));

app.MapCategoryEndpoints();

app.Run();

public partial class Program
{
}
