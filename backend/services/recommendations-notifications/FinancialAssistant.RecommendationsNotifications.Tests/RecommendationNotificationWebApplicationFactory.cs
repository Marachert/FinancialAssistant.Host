using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.RecommendationsNotifications.Tests;

public sealed class RecommendationNotificationWebApplicationFactory :
    WebApplicationFactory<Program>
{
    public const string SharedSecret =
        "recommendations-notifications-test-secret-32-characters";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RecommendationsNotifications:Gateway:SharedSecret"] = SharedSecret,
                    ["RecommendationsNotifications:Events:Mode"] = "InMemoryDevelopment"
                });
        });
    }
}
