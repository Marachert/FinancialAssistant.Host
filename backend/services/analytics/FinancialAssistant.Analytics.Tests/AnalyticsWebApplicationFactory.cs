using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.Analytics.Tests;

public sealed class AnalyticsWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GatewaySecret = "synthetic-analytics-gateway-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Analytics:Gateway:SharedSecret"] = GatewaySecret
                }));
    }
}
