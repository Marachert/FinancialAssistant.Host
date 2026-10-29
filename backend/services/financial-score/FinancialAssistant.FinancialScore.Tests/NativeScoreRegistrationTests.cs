using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Infrastructure;
using FinancialAssistant.FinancialScore.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FinancialAssistant.FinancialScore.Tests;

public sealed class NativeScoreRegistrationTests
{
    [Fact]
    public void MissingOrInvalidNativeConfigurationCannotSelectMemoryFallback()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddFinancialScoreInfrastructure(new ConfigurationBuilder().Build(), true));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["FinancialScore:Storage:Postgres:Database"] = "synthetic_invalid" }).Build();
        Assert.ThrowsAny<Exception>(() => new ServiceCollection().AddFinancialScoreInfrastructure(configuration, true));
    }

    [Fact]
    public async Task NativeRegistrationsAreOwnedAndExcludeMemoryAndLegacyConsumers()
    {
        var values = new Dictionary<string, string?>
        {
            ["FinancialScore:Storage:Postgres:Database"] = "synthetic_score",
            ["FinancialScore:Storage:Postgres:Port"] = "5432",
            ["FinancialScore:Storage:Postgres:Password"] = new string('s', 48),
            ["FinancialScore:Storage:Postgres:IndexKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)41, 32).ToArray()),
            ["FinancialScore:Storage:Postgres:ActiveEncryptionKeyId"] = "synthetic_key",
            ["FinancialScore:Storage:Postgres:EncryptionKeys:synthetic_key"] = Convert.ToBase64String(Enumerable.Repeat((byte)43, 32).ToArray()),
            ["FinancialScore:Events:Mode"] = "RabbitMq"
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFinancialScoreApplication();
        services.AddFinancialScoreInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), true);
        await using var provider = services.BuildServiceProvider();
        Assert.IsType<PostgresFinancialScoreStore>(provider.GetRequiredService<IFinancialScoreStore>());
        Assert.IsType<ScorePersistenceContext>(provider.GetRequiredService<IFinancialScoreTransaction>());
        Assert.IsType<PostgresFinancialScoreEventPublisher>(provider.GetRequiredService<IFinancialScoreEventPublisher>());
        Assert.IsType<PostgresFinancialScoreProfileSettingsProvider>(provider.GetRequiredService<IFinancialScoreProfileSettingsProvider>());
        Assert.Equal("financial-score", provider.GetRequiredService<OwnedPostgresStore>().ServiceId);
        Assert.Null(provider.GetService<InMemoryFinancialScoreStore>());
        Assert.DoesNotContain(provider.GetServices<IHostedService>(), service => service is FinancialScoreFinancialEventConsumer);
    }
}
