using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Infrastructure;
using FinancialAssistant.Income.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinancialAssistant.Income.Tests;

public sealed class NativeIncomeCompositionTests
{
    [Fact]
    public void MissingNativeConfigurationNeverFallsBack()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddIncomeInfrastructure(windowsNative: true));
        Assert.ThrowsAny<Exception>(() => new ServiceCollection().AddIncomeInfrastructure(new ConfigurationBuilder().Build(), true));
        using var development = new ServiceCollection().AddIncomeInfrastructure().BuildServiceProvider();
        Assert.IsType<InMemoryIncomeRecordStore>(development.GetRequiredService<IIncomeRecordStore>());
        Assert.IsType<InMemoryIncomeRecordEventPublisher>(development.GetRequiredService<IIncomeRecordEventPublisher>());
    }

    [Fact]
    public async Task NativeUsesOwnedAtomicStoreOutboxAndConsumer()
    {
        await using var provider = NativeProvider();
        Assert.IsType<PostgresIncomeRecordStore>(provider.GetRequiredService<IIncomeRecordStore>());
        Assert.IsType<PostgresIncomeRecordEventPublisher>(provider.GetRequiredService<IIncomeRecordEventPublisher>());
        Assert.EndsWith("TransactionalIncomeManagementService", provider.GetRequiredService<IIncomeManagementService>().GetType().FullName);
        Assert.EndsWith("TransactionalIncomeConfirmedConsumer", provider.GetRequiredService<ITransactionConfirmedConsumer>().GetType().FullName);
        Assert.Null(provider.GetService<InMemoryIncomeRecordStore>());
        Assert.Null(provider.GetService<InMemoryIncomeRecordEventPublisher>());
    }

    [Fact]
    public async Task NullAndInvalidConfirmationIsRejectedBeforeStorage()
    {
        await using var provider = NativeProvider();
        var consumer = provider.GetRequiredService<ITransactionConfirmedConsumer>();
        await Assert.ThrowsAsync<ArgumentNullException>(() => consumer.ConsumeAsync(null!, default));
        var value = new TransactionConfirmedIntegrationEvent("event", "record", null!, "draft", "income",
            10m, "USD", "income.salary", null, new DateOnly(2026, 10, 12), DateTimeOffset.UtcNow, "correlation");
        await Assert.ThrowsAsync<ArgumentException>(() => consumer.ConsumeAsync(value, default));
    }

    [Fact]
    public async Task InvalidManualInputIsRejectedBeforeDatabaseAccess()
    {
        await using var provider = NativeProvider();
        var service = provider.GetRequiredService<IIncomeManagementService>();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync("synthetic-owner",
            new FinancialAssistant.Income.Contracts.CreateIncomeRequest(0m, "USD", "income.salary", null,
                DateOnly.FromDateTime(DateTime.UtcNow)), default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAsync("synthetic-owner", null!, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync("synthetic-owner", "record",
            new FinancialAssistant.Income.Contracts.UpdateIncomeRequest(10m, "XYZ", "income.salary", null,
                DateOnly.FromDateTime(DateTime.UtcNow)), default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ArchiveAsync("synthetic-owner", "", default));
    }

    private static ServiceProvider NativeProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Income:Storage:Postgres:Database"] = "synthetic",
            ["Income:Storage:Postgres:Port"] = "5432",
            ["Income:Storage:Postgres:Password"] = new string('x', 40),
            ["Income:Storage:Postgres:IndexKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
            ["Income:Storage:Postgres:ActiveEncryptionKeyId"] = "v1",
            ["Income:Storage:Postgres:EncryptionKeys:v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray())
        }).Build();
        return new ServiceCollection().AddLogging().AddIncomeApplication().AddIncomeInfrastructure(configuration, true).BuildServiceProvider();
    }
}
