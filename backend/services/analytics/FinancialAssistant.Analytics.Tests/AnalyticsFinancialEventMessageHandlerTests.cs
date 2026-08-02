using System.Text.Json;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using Xunit;

namespace FinancialAssistant.Analytics.Tests;

public sealed class AnalyticsFinancialEventMessageHandlerTests
{
    [Fact]
    public async Task HandleAsync_ProjectsSerializedConfirmedFinancialEvent()
    {
        var store = new InMemoryAnalyticsReadModelStore();
        var projector = new AnalyticsProjector(store);
        var handler = new AnalyticsFinancialEventMessageHandler(projector);
        var changedAt = DateTimeOffset.Parse("2026-08-20T12:00:00Z");
        var envelope = new IntegrationEventEnvelope<FinancialRecordChangedV1>(
            "synthetic-message-event",
            "synthetic-message-occurrence",
            FinancialRecordEventTypes.IncomeCreated,
            changedAt,
            "income-service",
            FinancialRecordEventTypes.SchemaVersion,
            "synthetic-correlation",
            "synthetic-causation",
            new string('b', 64),
            new FinancialRecordChangedV1(
                "synthetic-income-record",
                125m,
                "USD",
                "income.salary",
                new DateOnly(2026, 8, 20),
                "active",
                0,
                "manual",
                changedAt));

        await handler.HandleAsync(
            JsonSerializer.SerializeToUtf8Bytes(
                envelope,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CancellationToken.None);

        var snapshot = await store.GetAsync(new string('b', 64), "USD", CancellationToken.None);
        Assert.Equal(125m, snapshot.DailyTotals[new DateOnly(2026, 8, 20)].Income);
        Assert.Equal(125m, snapshot.WeeklyTotals[new DateOnly(2026, 8, 17)].Income);
        Assert.Equal(125m, snapshot.MonthlyTotals[new DateOnly(2026, 8, 1)].Totals.Income);
    }
}
