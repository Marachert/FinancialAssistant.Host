using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Contracts;
using FinancialAssistant.Expense.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.TransactionIntake.Contracts;
using Xunit;

namespace FinancialAssistant.Expense.Tests;

public sealed class ExpenseRecordEventPublishingTests
{
    [Fact]
    public async Task ManualLifecycle_PublishesVersionedPrivacySafeEvents()
    {
        var store = new InMemoryExpenseRecordStore();
        var publisher = new InMemoryExpenseRecordEventPublisher();
        var service = new ExpenseManagementService(store, TimeProvider.System, publisher);
        const string userId = "synthetic-expense-event-owner";
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var created = await service.CreateAsync(
            userId,
            new CreateExpenseRequest(125.50m, "USD", "expense.groceries", "Synthetic Merchant", date),
            CancellationToken.None);
        _ = await service.UpdateAsync(
            userId,
            created.Id,
            new UpdateExpenseRequest(130m, "EUR", "expense.groceries", null, date),
            CancellationToken.None);
        _ = await service.ArchiveAsync(userId, created.Id, CancellationToken.None);
        _ = await service.ArchiveAsync(userId, created.Id, CancellationToken.None);
        _ = await service.RestoreAsync(userId, created.Id, CancellationToken.None);

        var messages = publisher.Published;
        Assert.Equal(4, messages.Count);
        Assert.Contains(messages, message => message.EventType == FinancialRecordEventTypes.ExpenseCreated);
        Assert.Contains(messages, message => message.EventType == FinancialRecordEventTypes.ExpenseUpdated);
        Assert.Contains(messages, message => message.EventType == FinancialRecordEventTypes.ExpenseArchived);
        Assert.Contains(messages, message => message.EventType == FinancialRecordEventTypes.ExpenseRestored);
        Assert.All(messages, message =>
        {
            Assert.Equal(1, message.SchemaVersion);
            Assert.Equal("expense-service", message.Producer);
            Assert.NotEqual(userId, message.UserIdHash);
            Assert.Equal(64, message.UserIdHash!.Length);
            Assert.Equal(created.Id, message.Payload.RecordId);
            Assert.DoesNotContain("Merchant", message.Payload.GetType().GetProperties().Select(property => property.Name));
        });
    }

    [Fact]
    public async Task ConfirmedEventReplay_PublishesOneCreatedMessageWithCorrelation()
    {
        var store = new InMemoryExpenseRecordStore();
        var publisher = new InMemoryExpenseRecordEventPublisher();
        var consumer = new ExpenseTransactionConfirmedConsumer(store, publisher);
        var confirmed = new TransactionConfirmedIntegrationEvent(
            "synthetic-upstream-event",
            "synthetic-expense-record",
            "synthetic-expense-owner",
            "synthetic-draft",
            "expense",
            42m,
            "USD",
            "expense.groceries",
            null,
            new DateOnly(2026, 8, 1),
            new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
            "synthetic-correlation");

        await consumer.ConsumeAsync(confirmed, CancellationToken.None);
        await consumer.ConsumeAsync(confirmed, CancellationToken.None);

        var message = Assert.Single(publisher.Published);
        Assert.Equal(FinancialRecordEventTypes.ExpenseCreated, message.EventType);
        Assert.Equal("synthetic-correlation", message.CorrelationId);
        Assert.Equal("synthetic-upstream-event", message.CausationId);
        Assert.Equal("synthetic-expense-record", message.Payload.RecordId);
    }
}
