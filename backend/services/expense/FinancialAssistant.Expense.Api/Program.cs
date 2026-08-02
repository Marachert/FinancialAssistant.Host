using FinancialAssistant.Expense.Api.Endpoints;
using FinancialAssistant.Expense.Api.Security;
using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Contracts;
using FinancialAssistant.Expense.Infrastructure;
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
            Title = "Financial Assistant Expense API",
            Version = "v1",
            Description = "Confirmed expense records owned by the Expense Service."
        });
});
builder.Services.AddExpenseApplication();
builder.Services.AddExpenseInfrastructure();
builder.Services.AddSingleton<ExpenseGatewayAuthenticator>();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Expense service process is running."),
        tags: new[] { "live", "ready" });

var app = builder.Build();

_ = app.Services.GetRequiredService<ExpenseGatewayAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Expense API v1"));
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
    "/expense/info",
    (IHostEnvironment environment) => Results.Ok(
        new ExpenseServiceInfoResponse(
            "financial-assistant-expense-service",
            "running",
            environment.EnvironmentName,
            "in-memory",
            "confirmed_or_manual")));

app.MapExpenseEndpoints();

app.Run();

public partial class Program
{
}
