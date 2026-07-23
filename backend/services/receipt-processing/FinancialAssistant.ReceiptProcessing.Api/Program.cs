using FinancialAssistant.ReceiptProcessing.Api.Endpoints;
using FinancialAssistant.ReceiptProcessing.Api.Security;
using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = ReceiptProcessingService.MaximumReceiptSizeBytes + (1024 * 1024));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Financial Assistant Receipt Processing API",
            Version = "v1",
            Description = "Secure receipt intake and asynchronous OCR draft-candidate processing."
        });
});
builder.Services.AddReceiptProcessingApplication();
builder.Services.AddReceiptProcessingInfrastructure();
builder.Services.AddSingleton<ReceiptGatewayAuthenticator>();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Receipt Processing service process is running."),
        tags: new[] { "live", "ready" });

var app = builder.Build();

_ = app.Services.GetRequiredService<ReceiptGatewayAuthenticator>();
_ = app.Services.GetRequiredService<IOcrCompletedPublisher>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Receipt Processing API v1"));
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
app.MapReceiptProcessingEndpoints();

app.Run();

public partial class Program
{
}
