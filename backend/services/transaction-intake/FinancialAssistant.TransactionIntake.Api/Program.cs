using FinancialAssistant.Expense.Infrastructure;
using FinancialAssistant.Income.Infrastructure;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.Observability;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Api.Endpoints;
using FinancialAssistant.TransactionIntake.Api.Security;
using FinancialAssistant.TransactionIntake.Application;
using FinancialAssistant.TransactionIntake.Infrastructure;
using Microsoft.OpenApi.Models;

var builder = FinancialAssistantHost.CreateBuilder(args, "transaction-intake");

builder.AddFinancialAssistantObservability();
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
var windowsNative = builder.Environment.IsEnvironment(FinancialAssistantHost.NativeEnvironment);
if (!windowsNative)
{
    builder.Services.AddIncomeInfrastructure();
    builder.Services.AddExpenseInfrastructure();
}
builder.Services.AddTransactionIntakeInfrastructure(builder.Configuration, windowsNative);
builder.Services.AddSingleton<TransactionIntakeGatewayAuthenticator>();
builder.Services.AddSingleton<ReceiptEventAuthenticator>();
builder.Services.AddFinancialAssistantHealthChecks();

var app = builder.Build();
if (windowsNative) { app.UseOwnedStorageFailureBoundary(); }

app.UseFinancialAssistantCorrelation();
_ = app.Services.GetRequiredService<TransactionIntakeGatewayAuthenticator>();
_ = app.Services.GetRequiredService<ReceiptEventAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Transaction Intake API v1"));
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapFinancialAssistantHealthEndpoints();
app.MapGet("/transaction-intake/info", (IHostEnvironment environment) => Results.Ok(new
{
    service = "financial-assistant-transaction-intake-service",
    status = "running",
    environment = environment.EnvironmentName,
    parserProvider = "deterministic-development-adapter",
    storageProvider = windowsNative ? "PostgreSQL" : "in-memory"
}));

app.MapTransactionIntakeEndpoints();
app.MapOcrCompletedEventEndpoint();

app.Run();

public partial class Program
{
}
