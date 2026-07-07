using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/health"));

app.MapHealthChecks("/health");

app.MapGet("/gateway/info", (IHostEnvironment environment) => Results.Ok(new
{
    service = "financial-assistant-public-api-gateway",
    status = "running",
    environment = environment.EnvironmentName,
    traceId = Activity.Current?.TraceId.ToString()
}));

app.Run();
