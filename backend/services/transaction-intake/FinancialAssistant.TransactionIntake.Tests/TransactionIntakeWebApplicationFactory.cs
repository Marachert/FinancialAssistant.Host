using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.TransactionIntake.Tests;

public sealed class TransactionIntakeWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GatewaySecret = "synthetic-transaction-intake-gateway-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["TransactionIntake:Gateway:SharedSecret"] = GatewaySecret
                }));
    }
}
