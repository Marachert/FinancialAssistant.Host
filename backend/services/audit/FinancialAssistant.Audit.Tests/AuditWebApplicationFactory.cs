using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.Audit.Tests;

public sealed class AuditWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GatewaySecret = "synthetic-audit-gateway-secret-2026";
    public const string ServiceSecret = "synthetic-audit-service-secret-2026";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Audit:Gateway:SharedSecret"] = GatewaySecret,
                ["Audit:Services:SharedSecret"] = ServiceSecret
            }));
    }
}
