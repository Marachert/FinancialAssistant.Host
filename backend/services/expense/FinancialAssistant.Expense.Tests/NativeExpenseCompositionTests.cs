using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Infrastructure;
using FinancialAssistant.Expense.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinancialAssistant.Expense.Tests;

public sealed class NativeExpenseCompositionTests
{
    [Fact]
    public void MissingNativeConfigurationNeverFallsBack()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddExpenseInfrastructure(windowsNative: true));
        Assert.ThrowsAny<Exception>(() => new ServiceCollection().AddExpenseInfrastructure(new ConfigurationBuilder().Build(), true));
        using var development = new ServiceCollection().AddExpenseInfrastructure().BuildServiceProvider();
        Assert.IsType<InMemoryExpenseRecordStore>(development.GetRequiredService<IExpenseRecordStore>());
        Assert.IsType<InMemoryExpenseRecordEventPublisher>(development.GetRequiredService<IExpenseRecordEventPublisher>());
    }

    [Fact]
    public async Task NativeUsesOwnedAtomicStoreOutboxAndConsumer()
    {
        await using var provider = NativeProvider();
        Assert.IsType<PostgresExpenseRecordStore>(provider.GetRequiredService<IExpenseRecordStore>());
        Assert.IsType<PostgresExpenseRecordEventPublisher>(provider.GetRequiredService<IExpenseRecordEventPublisher>());
        Assert.EndsWith("TransactionalExpenseManagementService", provider.GetRequiredService<IExpenseManagementService>().GetType().FullName);
        Assert.EndsWith("TransactionalExpenseConfirmedConsumer", provider.GetRequiredService<ITransactionConfirmedConsumer>().GetType().FullName);
        Assert.Null(provider.GetService<InMemoryExpenseRecordStore>());
        Assert.Null(provider.GetService<InMemoryExpenseRecordEventPublisher>());
    }

    [Fact]
    public async Task NullAndInvalidConfirmationIsRejectedBeforeStorage()
    {
        await using var provider = NativeProvider();
        var consumer = provider.GetRequiredService<ITransactionConfirmedConsumer>();
        await Assert.ThrowsAsync<ArgumentNullException>(() => consumer.ConsumeAsync(null!, default));
        var value = new TransactionConfirmedIntegrationEvent("event", "record", null!, "draft", "expense",
            10m, "USD", "expense.groceries", null, new DateOnly(2026, 10, 12), DateTimeOffset.UtcNow, "correlation");
        await Assert.ThrowsAsync<ArgumentException>(() => consumer.ConsumeAsync(value, default));
    }

    [Fact]
    public async Task InvalidManualInputIsRejectedBeforeDatabaseAccess()
    {
        await using var provider = NativeProvider();
        var service = provider.GetRequiredService<IExpenseManagementService>();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync("synthetic-owner",
            new FinancialAssistant.Expense.Contracts.CreateExpenseRequest(0m, "USD", "expense.groceries", null,
                DateOnly.FromDateTime(DateTime.UtcNow)), default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAsync("synthetic-owner", null!, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync("synthetic-owner", "record",
            new FinancialAssistant.Expense.Contracts.UpdateExpenseRequest(10m, "XYZ", "expense.groceries", null,
                DateOnly.FromDateTime(DateTime.UtcNow)), default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ArchiveAsync("synthetic-owner", "", default));
    }

    private static ServiceProvider NativeProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Expense:Storage:Postgres:Database"] = "synthetic",
            ["Expense:Storage:Postgres:Port"] = "5432",
            ["Expense:Storage:Postgres:Password"] = new string('x', 40),
            ["Expense:Storage:Postgres:IndexKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
            ["Expense:Storage:Postgres:ActiveEncryptionKeyId"] = "v1",
            ["Expense:Storage:Postgres:EncryptionKeys:v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray())
        }).Build();
        return new ServiceCollection().AddLogging().AddExpenseApplication().AddExpenseInfrastructure(configuration, true).BuildServiceProvider();
    }
}
