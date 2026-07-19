using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.Category.Tests;

public sealed class CategoryContractWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GatewaySecret = "synthetic-category-gateway-secret-2026";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Category:Gateway:SharedSecret"] = GatewaySecret
                }));
    }
}
