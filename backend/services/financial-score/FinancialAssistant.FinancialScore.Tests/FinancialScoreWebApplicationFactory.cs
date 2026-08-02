using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.FinancialScore.Tests;

public sealed class FinancialScoreWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GatewaySecret = "synthetic-financial-score-gateway-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["FinancialScore:Gateway:SharedSecret"] = GatewaySecret
                }));
    }
}
