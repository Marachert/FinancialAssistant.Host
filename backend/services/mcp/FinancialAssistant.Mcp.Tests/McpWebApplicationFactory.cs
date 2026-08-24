using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.Mcp.Tests;

public sealed class McpWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SharedSecret = "synthetic-mcp-shared-secret-value-2026";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:Authentication:SharedSecret"] = SharedSecret,
                ["Mcp:Monitoring:BaseAddress"] = string.Empty,
                ["Mcp:Audit:Mode"] = "InMemory"
            }));
    }
}
