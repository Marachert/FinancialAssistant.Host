using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace FinancialAssistant.Shared.Observability.Tests;

public sealed class HealthBaselineTests
{
    [Fact]
    public async Task Response_is_structured_and_excludes_sensitive_failure_details()
    {
        const string sensitiveMessage = "synthetic credential must not escape";
        var services = new ServiceCollection()
            .AddSingleton(new ObservabilityRuntimeIdentity("synthetic-service", "Testing"))
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() }
        };
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["required_store"] = new(
                    HealthStatus.Unhealthy,
                    sensitiveMessage,
                    TimeSpan.FromMilliseconds(2),
                    new InvalidOperationException(sensitiveMessage),
                    new Dictionary<string, object> { ["secret"] = sensitiveMessage })
            },
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(2));

        await FinancialAssistantHealthExtensions.WriteHealthResponseAsync(context, report);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var root = document.RootElement;
        var check = Assert.Single(root.GetProperty("checks").EnumerateArray());
        Assert.Equal("unavailable", root.GetProperty("status").GetString());
        Assert.Equal("synthetic-service", root.GetProperty("service").GetString());
        Assert.Equal("Testing", root.GetProperty("environment").GetString());
        Assert.Equal("required_store", check.GetProperty("name").GetString());
        Assert.Equal("check_failed", check.GetProperty("errorCategory").GetString());
        Assert.DoesNotContain(sensitiveMessage, root.GetRawText(), StringComparison.Ordinal);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public void Registration_adds_one_self_check_to_liveness_and_readiness()
    {
        var services = new ServiceCollection();

        services.AddFinancialAssistantHealthChecks();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
        var registration = Assert.Single(options.Value.Registrations);
        Assert.Equal("self", registration.Name);
        Assert.Contains(FinancialAssistantHealthExtensions.LiveTag, registration.Tags);
        Assert.Contains(FinancialAssistantHealthExtensions.ReadyTag, registration.Tags);
    }
}
