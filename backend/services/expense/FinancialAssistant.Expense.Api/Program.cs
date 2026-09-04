using FinancialAssistant.Expense.Api.Endpoints;
using FinancialAssistant.Expense.Api.Security;
using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Contracts;
using FinancialAssistant.Expense.Infrastructure;
using FinancialAssistant.Shared.Observability;
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
            Title = "Financial Assistant Expense API",
            Version = "v1",
            Description = "Confirmed expense records owned by the Expense Service."
        });
});
builder.Services.AddExpenseApplication();
builder.Services.AddExpenseInfrastructure();
builder.Services.AddSingleton<ExpenseGatewayAuthenticator>();
builder.Services.AddFinancialAssistantHealthChecks();

var app = builder.Build();

app.UseFinancialAssistantCorrelation();
_ = app.Services.GetRequiredService<ExpenseGatewayAuthenticator>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Expense API v1"));
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapFinancialAssistantHealthEndpoints();

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
