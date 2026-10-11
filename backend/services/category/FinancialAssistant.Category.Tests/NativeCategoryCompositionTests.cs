using FinancialAssistant.Category.Application;
using FinancialAssistant.Category.Application.Abstractions;
using FinancialAssistant.Category.Application.Categories;
using FinancialAssistant.Category.Infrastructure;
using FinancialAssistant.Category.Infrastructure.Events;
using FinancialAssistant.Category.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Category.Tests;

public sealed class NativeCategoryCompositionTests
{
    [Fact]
    public void NativeRejectsMissingConfigurationInsteadOfFallingBack()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCategoryInfrastructure(windowsNative: true));
        Assert.ThrowsAny<Exception>(() => new ServiceCollection().AddCategoryInfrastructure(new ConfigurationBuilder().Build(), true));
        using var development = new ServiceCollection().AddCategoryInfrastructure().BuildServiceProvider();
        Assert.IsType<InMemoryCategoryStore>(development.GetRequiredService<ICategoryStore>());
        Assert.IsType<InMemoryCategoryEventPublisher>(development.GetRequiredService<ICategoryEventPublisher>());
    }

    [Fact]
    public async Task NativeUsesTransactionalServiceAndRealPendingOutboxOnly()
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
        await using var provider = new ServiceCollection().AddLogging().AddCategoryApplication().AddCategoryInfrastructure(configuration, true).BuildServiceProvider();
        Assert.IsType<PostgresCategoryStore>(provider.GetRequiredService<ICategoryStore>());
        Assert.Equal("FinancialAssistant.Category.Infrastructure.Storage.TransactionalCategoryService", provider.GetRequiredService<ICategoryService>().GetType().FullName);
        Assert.IsType<PostgresCategoryEventPublisher>(provider.GetRequiredService<ICategoryEventPublisher>());
        Assert.Null(provider.GetService<InMemoryCategoryEventPublisher>());
    }
}
