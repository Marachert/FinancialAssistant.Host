using FinancialAssistant.Category.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FinancialAssistant.Category.Tests;

public sealed class CategoryContractWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GatewaySecret = "synthetic-category-gateway-secret-2026";
    private readonly bool nativeStorage;
    public CategoryContractWebApplicationFactory() : this(false) { }
    internal CategoryContractWebApplicationFactory(bool nativeStorage) { this.nativeStorage = nativeStorage; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Category:Gateway:SharedSecret"] = GatewaySecret
                }));
        if (nativeStorage)
        {
            builder.ConfigureTestServices(services =>
            {
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Category:Storage:Postgres:Database"] = "synthetic",
                    ["Category:Storage:Postgres:Port"] = "5432",
                    ["Category:Storage:Postgres:Password"] = new string('x', 40),
                    ["Category:Storage:Postgres:IndexKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
                    ["Category:Storage:Postgres:ActiveEncryptionKeyId"] = "v1",
                    ["Category:Storage:Postgres:EncryptionKeys:v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray())
                }).Build();
                services.AddCategoryInfrastructure(configuration, true);
                // Invalid-input HTTP tests must never connect a maintenance worker to a discovered database.
                foreach (var worker in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.Name == "CategoryOutboxRetentionWorker").ToArray())
                { services.Remove(worker); }
            });
        }
    }
}
