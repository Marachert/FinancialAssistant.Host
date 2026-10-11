using FinancialAssistant.Profile.Application.Abstractions;
using FinancialAssistant.Profile.Infrastructure;
using FinancialAssistant.Profile.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Profile.Tests;

public sealed class NativeProfileCompositionTests
{
    [Fact]
    public void NativeRejectsMissingConfigurationInsteadOfFallingBack()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddProfileInfrastructure(windowsNative: true));
        Assert.ThrowsAny<Exception>(() => new ServiceCollection().AddProfileInfrastructure(new ConfigurationBuilder().Build(), true));
        using var development = new ServiceCollection().AddProfileInfrastructure().BuildServiceProvider();
        Assert.IsType<InMemoryProfileStore>(development.GetRequiredService<IProfileStore>());
    }

    [Fact]
    public async Task NativeSelectsOnlyItsOwnedDurableStore()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Profile:Storage:Postgres:Database"] = "synthetic",
            ["Profile:Storage:Postgres:Port"] = "5432",
            ["Profile:Storage:Postgres:Password"] = new string('x', 40),
            ["Profile:Storage:Postgres:IndexKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
            ["Profile:Storage:Postgres:ActiveEncryptionKeyId"] = "v1",
            ["Profile:Storage:Postgres:EncryptionKeys:v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray())
        }).Build();
        await using var provider = new ServiceCollection().AddLogging().AddProfileInfrastructure(configuration, true).BuildServiceProvider();
        Assert.IsType<PostgresProfileStore>(provider.GetRequiredService<IProfileStore>());
        Assert.Null(provider.GetService<InMemoryProfileStore>());
    }
}
