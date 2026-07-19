using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Infrastructure;
using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Infrastructure;
using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Tests;

public sealed class FinancialRecordConsumerTests
{
    [Fact]
    public async Task IncomeConsumer_RejectsInvalidIncomeEvent()
    {
        var store = new InMemoryIncomeRecordStore();
        var consumer = new IncomeTransactionConfirmedConsumer(store);
        var integrationEvent = CreateEvent("income", 0, "income.salary");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            consumer.ConsumeAsync(integrationEvent, CancellationToken.None));
        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task ExpenseConsumer_RejectsMismatchedCategoryOwnership()
    {
        var store = new InMemoryExpenseRecordStore();
        var consumer = new ExpenseTransactionConfirmedConsumer(store);
        var integrationEvent = CreateEvent("expense", 10, "income.salary");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            consumer.ConsumeAsync(integrationEvent, CancellationToken.None));
        Assert.Empty(store.Records);
    }

    private static TransactionConfirmedIntegrationEvent CreateEvent(
        string type,
        decimal amount,
        string categoryId) =>
        new(
            "event_synthetic_consumer",
            "txn_synthetic_consumer",
            "synthetic-consumer-user",
            "draft_synthetic_consumer",
            type,
            amount,
            "USD",
            categoryId,
            null,
            new DateOnly(2026, 7, 19),
            new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero),
            "synthetic-consumer-correlation");
}
