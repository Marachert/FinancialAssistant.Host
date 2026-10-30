using FinancialAssistant.Profile.Api.Endpoints;
using FinancialAssistant.Profile.Api.Security;
using FinancialAssistant.Profile.Application;
using FinancialAssistant.Profile.Infrastructure;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.Observability;
using FinancialAssistant.Shared.Persistence;
using Microsoft.OpenApi.Models;

var builder = FinancialAssistantHost.CreateBuilder(args, "profile");

builder.AddFinancialAssistantObservability();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Financial Assistant Profile API",
            Version = "v1",
            Description = "User profile and preference contracts owned by the Profile Service."
        });
});
builder.Services.AddProfileApplication();
var windowsNative = builder.Environment.IsEnvironment(FinancialAssistantHost.NativeEnvironment);
builder.Services.AddProfileInfrastructure(builder.Configuration, windowsNative);
if (windowsNative) { builder.Services.AddSingleton<ProfileFinancialReaderAuthenticator>(); }
builder.Services.AddFinancialAssistantHealthChecks();

var app = builder.Build();

Func<HttpContext, bool>? internalAuthorization = null;
if (windowsNative) { internalAuthorization = app.Services.GetRequiredService<ProfileFinancialReaderAuthenticator>().IsAuthenticated; }
app.UseWindowsNativeRequestBoundary(internalAuthorization);
if (windowsNative) { app.UseOwnedStorageFailureBoundary(); }

app.UseFinancialAssistantCorrelation();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Financial Assistant Profile API v1"));
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapFinancialAssistantHealthEndpoints();

app.MapGet("/profile/info", (IHostEnvironment environment) => Results.Ok(new
{
    service = "financial-assistant-profile-service",
    status = "running",
    environment = environment.EnvironmentName,
    storageProvider = windowsNative ? "PostgreSQL" : "in-memory"
}));

app.MapProfileEndpoints();
if (windowsNative) { app.MapNativeProfileFinancialSettings(); }

app.Run();

public partial class Program
{
}
