using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Contracts;
using FinancialAssistant.Income.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.TransactionIntake.Contracts;
using Xunit;

namespace FinancialAssistant.Income.Tests;

public sealed class IncomeRecordEventPublishingTests
{
    [Fact]
    public async Task ManualLifecycle_PublishesVersionedPrivacySafeEvents()
    {
        var store = new InMemoryIncomeRecordStore();
        var publisher = new InMemoryIncomeRecordEventPublisher();
        var service = new IncomeManagementService(store, TimeProvider.System, publisher);
        const string userId = "synthetic-income-event-owner";
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var created = await service.CreateAsync(
            userId,
            new CreateIncomeRequest(125.50m, "USD", "income.salary", "Synthetic Merchant", date),
            CancellationToken.None);
        _ = await service.UpdateAsync(
            userId,
            created.Id,
            new UpdateIncomeRequest(130m, "EUR", "income.salary", null, date),
            CancellationToken.None);
        _ = await service.ArchiveAsync(userId, created.Id, CancellationToken.None);
        _ = await service.ArchiveAsync(userId, created.Id, CancellationToken.None);
        _ = await service.RestoreAsync(userId, created.Id, CancellationToken.None);

        var messages = publisher.Published;
        Assert.Equal(4, messages.Count);
        Assert.Contains(messages, message => message.EventType == FinancialRecordEventTypes.IncomeCreated);
        Assert.Contains(messages, message => message.EventType == FinancialRecordEventTypes.IncomeUpdated);
        Assert.Contains(messages, message => message.EventType == FinancialRecordEventTypes.IncomeArchived);
        Assert.Contains(messages, message => message.EventType == FinancialRecordEventTypes.IncomeRestored);
        Assert.All(messages, message =>
        {
            Assert.Equal(1, message.SchemaVersion);
            Assert.Equal("income-service", message.Producer);
            Assert.NotEqual(userId, message.UserIdHash);
            Assert.Equal(64, message.UserIdHash!.Length);
            Assert.Equal(created.Id, message.Payload.RecordId);
            Assert.DoesNotContain("Merchant", message.Payload.GetType().GetProperties().Select(property => property.Name));
        });
    }

    [Fact]
    public async Task ConfirmedEventReplay_PublishesOneCreatedMessageWithCorrelation()
    {
        var store = new InMemoryIncomeRecordStore();
        var publisher = new InMemoryIncomeRecordEventPublisher();
        var consumer = new IncomeTransactionConfirmedConsumer(store, publisher);
        var confirmed = new TransactionConfirmedIntegrationEvent(
            "synthetic-upstream-event",
            "synthetic-income-record",
            "synthetic-income-owner",
            "synthetic-draft",
            "income",
            42m,
            "USD",
            "income.salary",
            null,
            new DateOnly(2026, 8, 1),
            new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
            "synthetic-correlation");

        await consumer.ConsumeAsync(confirmed, CancellationToken.None);
        await consumer.ConsumeAsync(confirmed, CancellationToken.None);

        var message = Assert.Single(publisher.Published);
        Assert.Equal(FinancialRecordEventTypes.IncomeCreated, message.EventType);
        Assert.Equal("synthetic-correlation", message.CorrelationId);
        Assert.Equal("synthetic-upstream-event", message.CausationId);
        Assert.Equal("synthetic-income-record", message.Payload.RecordId);
    }
}
