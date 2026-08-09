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

    [Fact]
    public async Task Apply_CurrencyMoveRecalculatesOldAndNewScopes()
    {
        var store = new InMemoryFinancialScoreStore();
        var publisher = new InMemoryFinancialScoreEventPublisher();
        var service = new FinancialScoreService(store, publisher, new FinancialScoreCalculator());

        await service.ApplyAsync(
            CreateEvent("movable-expense", 0, FinancialRecordEventTypes.ExpenseCreated, 25m),
            null,
            CancellationToken.None);
        await service.ApplyAsync(
            CreateEvent(
                "movable-expense",
                1,
                FinancialRecordEventTypes.ExpenseUpdated,
                25m,
                currency: "EUR"),
            null,
            CancellationToken.None);

        var usd = await service.GetCurrentAsync(
            "synthetic-owner-hash",
            "USD",
            CancellationToken.None);
        var eur = await service.GetCurrentAsync(
            "synthetic-owner-hash",
            "EUR",
            CancellationToken.None);

        Assert.NotNull(usd);
        Assert.NotNull(eur);
        Assert.Equal(50, usd.Score);
        Assert.NotEqual(usd.Score, eur.Score);
        Assert.Equal(3, publisher.Published.Count);
    }

    [Fact]
    public async Task Current_TracksAcceptedArrivalAfterLateOlderTimestamp()
    {
        var service = new FinancialScoreService(
            new InMemoryFinancialScoreStore(),
            new InMemoryFinancialScoreEventPublisher(),
            new FinancialScoreCalculator());
        var newerTime = new DateTimeOffset(2026, 8, 20, 14, 0, 0, TimeSpan.Zero);
        var olderTime = newerTime.AddHours(-2);

        await service.ApplyAsync(
            CreateEvent(
                "first-expense",
                0,
                FinancialRecordEventTypes.ExpenseCreated,
                25m,
                changedAt: newerTime),
            null,
            CancellationToken.None);
        var late = await service.ApplyAsync(
            CreateEvent(
                "late-expense",
                0,
                FinancialRecordEventTypes.ExpenseCreated,
                25m,
                changedAt: olderTime),
            null,
            CancellationToken.None);
        var current = await service.GetCurrentAsync(
            "synthetic-owner-hash",
            "USD",
            CancellationToken.None);

        Assert.Equal(late, current);
    }

    [Fact]
    public async Task ReplayAfterNewerRevision_RepublishesStoredCalculation()
    {
        var publisher = new InMemoryFinancialScoreEventPublisher();
        var service = new FinancialScoreService(
            new InMemoryFinancialScoreStore(),
            publisher,
            new FinancialScoreCalculator());
        var original = CreateEvent(
            "replayed-expense",
            0,
            FinancialRecordEventTypes.ExpenseCreated,
            25m);
        var originalCalculation = await service.ApplyAsync(
            original,
            null,
            CancellationToken.None);
        await service.ApplyAsync(
            CreateEvent(
                "replayed-expense",
                1,
                FinancialRecordEventTypes.ExpenseUpdated,
                30m),
            null,
            CancellationToken.None);

        var replay = await service.ApplyAsync(original, null, CancellationToken.None);

        Assert.Equal(originalCalculation, replay);
        Assert.Equal(3, publisher.Published.Count);
        Assert.Equal(
            publisher.Published.First().EventId,
            publisher.Published.Last().EventId);
    }

    [Fact]
    public async Task HistoryCursor_RetainsEqualTimestampCalculations()
    {
        var service = new FinancialScoreService(
            new InMemoryFinancialScoreStore(),
            new InMemoryFinancialScoreEventPublisher(),
            new FinancialScoreCalculator());
        var timestamp = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        await service.ApplyAsync(
            CreateEvent("tie-a", 0, FinancialRecordEventTypes.ExpenseCreated, 10m, changedAt: timestamp),
            null,
            CancellationToken.None);
        await service.ApplyAsync(
            CreateEvent("tie-b", 0, FinancialRecordEventTypes.ExpenseCreated, 20m, changedAt: timestamp),
            null,
            CancellationToken.None);
        var firstPage = await service.GetHistoryAsync(
            "synthetic-owner-hash",
            "USD",
            null,
            null,
            1,
            CancellationToken.None);
        var first = firstPage[0];

        var secondPage = await service.GetHistoryAsync(
            "synthetic-owner-hash",
            "USD",
            first.CalculatedAtUtc,
            first.CalculationId,
            1,
            CancellationToken.None);

        Assert.Equal(2, firstPage.Count);
        Assert.Single(secondPage);
        Assert.NotEqual(first.CalculationId, secondPage[0].CalculationId);
    }

    [Fact]
    public async Task Apply_UsesProfileSettingsAndRejectsSemanticAdjustment()
    {
        var profileSettings = new InMemoryFinancialScoreProfileSettingsProvider();
        profileSettings.Set(
            "synthetic-owner-hash",
            "USD",
            new FinancialScoreProfileSettings(100m, true, true));
        var service = new FinancialScoreService(
            new InMemoryFinancialScoreStore(),
            new InMemoryFinancialScoreEventPublisher(),
            new FinancialScoreCalculator(),
            profileSettings);

        var result = await service.ApplyAsync(
            CreateEvent("profile-expense", 0, FinancialRecordEventTypes.ExpenseCreated, 25m),
            null,
            CancellationToken.None);

        Assert.Equal(
            15m,
            result!.Factors.Single(item => item.Code == "budget_usage").Contribution);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ApplyAsync(
            CreateEvent("semantic-expense", 0, FinancialRecordEventTypes.ExpenseCreated, 25m),
            new[] { new FinancialScoreSemanticFactor("opaque", 1m) },
            CancellationToken.None));
    }

    [Fact]
    public async Task CurrentDefault_IsPersistedAndIncludedByInclusivePeriod()
    {
        var publisher = new InMemoryFinancialScoreEventPublisher();
        var service = new FinancialScoreService(
            new InMemoryFinancialScoreStore(),
            publisher,
            new FinancialScoreCalculator());

        var first = await service.GetCurrentOrCreateDefaultAsync(
            "synthetic-new-owner-hash",
            "usd",
            CancellationToken.None);
        var second = await service.GetCurrentOrCreateDefaultAsync(
            "synthetic-new-owner-hash",
            "USD",
            CancellationToken.None);
        var history = await service.GetHistoryAsync(
            "synthetic-new-owner-hash",
            "USD",
            first.CalculatedAtUtc,
            first.CalculatedAtUtc,
            beforeUtc: null,
            beforeCalculationId: null,
            limit: 20,
            CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal(FinancialScoreFormula.NewUserDefault, first.Score);
        Assert.Single(history);
        Assert.Equal(first, history[0]);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task HistoryPeriod_ExcludesCalculationsOutsideRequestedRange()
    {
        var service = new FinancialScoreService(
            new InMemoryFinancialScoreStore(),
            new InMemoryFinancialScoreEventPublisher(),
            new FinancialScoreCalculator());
        var firstTimestamp = new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        var secondTimestamp = firstTimestamp.AddDays(1);
        await service.ApplyAsync(
            CreateEvent("period-a", 0, FinancialRecordEventTypes.ExpenseCreated, 10m, changedAt: firstTimestamp),
            null,
            CancellationToken.None);
        var expected = await service.ApplyAsync(
            CreateEvent("period-b", 0, FinancialRecordEventTypes.ExpenseCreated, 20m, changedAt: secondTimestamp),
            null,
            CancellationToken.None);

        var history = await service.GetHistoryAsync(
            "synthetic-owner-hash",
            "USD",
            secondTimestamp,
            secondTimestamp,
            beforeUtc: null,
            beforeCalculationId: null,
            limit: 20,
            CancellationToken.None);

        Assert.Single(history);
        Assert.Equal(expected, history[0]);
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.GetHistoryAsync(
                "synthetic-owner-hash",
                "USD",
                secondTimestamp,
                firstTimestamp,
                beforeUtc: null,
                beforeCalculationId: null,
                limit: 20,
                CancellationToken.None));
    }

    internal static IntegrationEventEnvelope<FinancialRecordChangedV1> CreateEvent(
        string recordId,
        long revision,
        string eventType,
        decimal amount,
        string userIdHash = "synthetic-owner-hash",
        string currency = "USD",
        DateTimeOffset? changedAt = null)
    {
        var effectiveChangedAt = changedAt ?? new DateTimeOffset(
            2026,
            8,
            20,
            12,
            checked((int)revision),
            0,
            TimeSpan.Zero);
        return new IntegrationEventEnvelope<FinancialRecordChangedV1>(
            $"event-{recordId}-{revision}",
            $"occurrence-{recordId}-{revision}",
            eventType,
            effectiveChangedAt,
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
                currency,
                "expense.synthetic",
                new DateOnly(2026, 8, 20),
                "active",
                revision,
                "manual",
                effectiveChangedAt));
    }
}
