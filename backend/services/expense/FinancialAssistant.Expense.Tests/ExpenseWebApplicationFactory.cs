using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.Expense.Tests;

public sealed class ExpenseWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GatewaySecret = "synthetic-expense-gateway-secret-value";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Expense:Gateway:SharedSecret"] = GatewaySecret
                }));
    }
}
