using FinancialAssistant.FinancialSummary.Application;
using FinancialAssistant.FinancialSummary.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using Xunit;

namespace FinancialAssistant.FinancialSummary.Tests;

public sealed class FinancialSummaryProjectorTests
{
    private static readonly string UserIdHash = new('a', 64);
    private static readonly DateTimeOffset ChangedAt =
        new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActiveRecords_DeriveDailyWeeklyMonthlyAndCategoryTotals()
    {
        var projector = CreateProjector();

        await projector.ApplyAsync(
            CreateEvent("income-day", FinancialRecordEventTypes.IncomeCreated, 100m, "income.salary", new DateOnly(2026, 8, 2)),
            CancellationToken.None);
        await projector.ApplyAsync(
            CreateEvent("expense-day", FinancialRecordEventTypes.ExpenseCreated, 40m, "expense.groceries", new DateOnly(2026, 8, 2)),
            CancellationToken.None);
        await projector.ApplyAsync(
            CreateEvent("income-week", FinancialRecordEventTypes.IncomeCreated, 200m, "income.freelance", new DateOnly(2026, 7, 28)),
            CancellationToken.None);
        await projector.ApplyAsync(
            CreateEvent("expense-month", FinancialRecordEventTypes.ExpenseCreated, 30m, "expense.utilities", new DateOnly(2026, 7, 15)),
            CancellationToken.None);

        var summary = await projector.GetAsync(
            UserIdHash,
            "usd",
            new DateOnly(2026, 8, 2),
            ChangedAt.AddHours(1),
            TimeSpan.FromHours(2),
            CancellationToken.None);

        Assert.Equal(100m, summary.Daily.Income);
        Assert.Equal(40m, summary.Daily.Expense);
        Assert.Equal(300m, summary.Weekly.Income);
        Assert.Equal(40m, summary.Weekly.Expense);
        Assert.Equal(300m, summary.Monthly.Income);
        Assert.Equal(40m, summary.Monthly.Expense);
        Assert.Equal(260m, summary.BalanceDelta);
        Assert.Equal(3, summary.CategoryBreakdown.Count);
        Assert.False(summary.IsStale);
    }

    [Fact]
    public async Task ArchiveAndOutOfOrderReplay_KeepLatestRevisionOnly()
    {
        var projector = CreateProjector();
        var created = CreateEvent(
            "expense-record",
            FinancialRecordEventTypes.ExpenseCreated,
            40m,
            "expense.groceries",
            new DateOnly(2026, 8, 2));
        var archived = CreateEvent(
            "expense-record",
            FinancialRecordEventTypes.ExpenseArchived,
            40m,
            "expense.groceries",
            new DateOnly(2026, 8, 2),
            revision: 1,
            status: "archived",
            changedAtUtc: ChangedAt.AddMinutes(5));

        await projector.ApplyAsync(archived, CancellationToken.None);
        await projector.ApplyAsync(created, CancellationToken.None);

        var summary = await projector.GetAsync(
            UserIdHash,
            "USD",
            new DateOnly(2026, 8, 2),
            ChangedAt.AddHours(1),
            TimeSpan.FromHours(2),
            CancellationToken.None);

        Assert.Equal(0m, summary.Daily.Expense);
        Assert.Empty(summary.CategoryBreakdown);
        Assert.Equal(ChangedAt.AddMinutes(5), summary.LastEventAtUtc);
    }

    [Fact]
    public async Task Rebuild_IsDeterministicAndStalenessIsExplicit()
    {
        var projector = CreateProjector();
        var created = CreateEvent(
            "income-record",
            FinancialRecordEventTypes.IncomeCreated,
            75m,
            "income.salary",
            new DateOnly(2026, 8, 2));
        var updated = CreateEvent(
            "income-record",
            FinancialRecordEventTypes.IncomeUpdated,
            90m,
            "income.salary",
            new DateOnly(2026, 8, 2),
            revision: 1,
            changedAtUtc: ChangedAt.AddMinutes(10));

        await projector.RebuildAsync(new[] { updated, created }, CancellationToken.None);
        var summary = await projector.GetAsync(
            UserIdHash,
            "USD",
            new DateOnly(2026, 8, 2),
            ChangedAt.AddHours(4),
            TimeSpan.FromHours(2),
            CancellationToken.None);

        Assert.Equal(90m, summary.Daily.Income);
        Assert.True(summary.IsStale);
    }

    [Fact]
    public async Task EmptyPeriod_ReturnsZeroTotalsAndStaleMarker()
    {
        var summary = await CreateProjector().GetAsync(
            UserIdHash,
            "USD",
            new DateOnly(2026, 8, 2),
            ChangedAt,
            TimeSpan.FromHours(2),
            CancellationToken.None);

        Assert.Equal(0m, summary.Daily.Income);
        Assert.Equal(0m, summary.Daily.Expense);
        Assert.Equal(0m, summary.BalanceDelta);
        Assert.Empty(summary.CategoryBreakdown);
        Assert.Null(summary.LastEventAtUtc);
        Assert.True(summary.IsStale);
    }

    private static FinancialSummaryProjector CreateProjector() =>
        new(new InMemoryFinancialSummaryReadModelStore());

    private static IntegrationEventEnvelope<FinancialRecordChangedV1> CreateEvent(
        string recordId,
        string eventType,
        decimal amount,
        string categoryId,
        DateOnly date,
        long revision = 0,
        string status = "active",
        DateTimeOffset? changedAtUtc = null)
    {
        var changedAt = changedAtUtc ?? ChangedAt;
        var eventId = $"{eventType}-{recordId}-{revision}";
        return new IntegrationEventEnvelope<FinancialRecordChangedV1>(
            eventId,
            eventId,
            eventType,
            changedAt,
            eventType.StartsWith("income.", StringComparison.Ordinal)
                ? "income-service"
                : "expense-service",
            1,
            "synthetic-correlation",
            "synthetic-causation",
            UserIdHash,
            new FinancialRecordChangedV1(
                recordId,
                amount,
                "USD",
                categoryId,
                date,
                status,
                revision,
                "manual",
                changedAt));
    }
}
