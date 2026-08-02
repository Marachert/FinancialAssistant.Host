using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.Income.Tests;

public sealed class IncomeWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GatewaySecret = "synthetic-income-gateway-secret-value";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Income:Gateway:SharedSecret"] = GatewaySecret
                }));
    }
}
