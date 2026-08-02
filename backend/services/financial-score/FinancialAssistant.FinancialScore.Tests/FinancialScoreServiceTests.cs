using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.FinancialScore.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using Xunit;

namespace FinancialAssistant.FinancialScore.Tests;

public sealed class FinancialScoreServiceTests
{
    [Fact]
    public async Task Apply_PersistsHistoryPublishesContractAndRepublishesDuplicateSafely()
    {
        var store = new InMemoryFinancialScoreStore();
        var publisher = new InMemoryFinancialScoreEventPublisher();
        var service = new FinancialScoreService(store, publisher, new FinancialScoreCalculator());
        var source = CreateEvent("expense-1", 0, FinancialRecordEventTypes.ExpenseCreated, 25m);

        var first = await service.ApplyAsync(source, null, CancellationToken.None);
        var replay = await service.ApplyAsync(source, null, CancellationToken.None);
        var history = await service.GetHistoryAsync(
            "synthetic-owner-hash",
            "USD",
            null,
            20,
            CancellationToken.None);

        Assert.Equal(first, replay);
        Assert.Single(history);
        Assert.Equal(2, publisher.Published.Count);
        Assert.All(publisher.Published, item =>
        {
            Assert.Equal(FinancialScoreEventTypes.ScoreCalculated, item.EventType);
            Assert.Equal(FinancialScoreEventTypes.SchemaVersion, item.SchemaVersion);
            Assert.Equal(first!.CalculationId, item.EventId);
            Assert.Equal(FinancialScoreFormula.Version, item.Payload.FormulaVersion);
        });
    }

    [Fact]
    public async Task Apply_UsesNewestRevisionAndIgnoresStaleEvents()
    {
        var publisher = new InMemoryFinancialScoreEventPublisher();
        var service = new FinancialScoreService(
            new InMemoryFinancialScoreStore(),
            publisher,
            new FinancialScoreCalculator());

        await service.ApplyAsync(
            CreateEvent("expense-1", 1, FinancialRecordEventTypes.ExpenseUpdated, 40m),
            null,
            CancellationToken.None);
        var stale = await service.ApplyAsync(
            CreateEvent("expense-1", 0, FinancialRecordEventTypes.ExpenseCreated, 25m),
            null,
            CancellationToken.None);

        Assert.Null(stale);
        Assert.Single(publisher.Published);
    }

    internal static IntegrationEventEnvelope<FinancialRecordChangedV1> CreateEvent(
        string recordId,
        long revision,
        string eventType,
        decimal amount,
        string userIdHash = "synthetic-owner-hash")
    {
        var changedAt = new DateTimeOffset(2026, 8, 20, 12, revision, 0, TimeSpan.Zero);
        return new IntegrationEventEnvelope<FinancialRecordChangedV1>(
            $"event-{recordId}-{revision}",
            $"occurrence-{recordId}-{revision}",
            eventType,
            changedAt,
            eventType.StartsWith("income", StringComparison.Ordinal)
                ? "income-service"
                : "expense-service",
            FinancialRecordEventTypes.SchemaVersion,
            "synthetic-correlation",
            "synthetic-causation",
            userIdHash,
            new FinancialRecordChangedV1(
                recordId,
                amount,
                "USD",
                "expense.synthetic",
                new DateOnly(2026, 8, 20),
                "active",
                revision,
                "manual",
                changedAt));
    }
}
