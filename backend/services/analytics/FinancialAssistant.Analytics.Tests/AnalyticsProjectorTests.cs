using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Contracts;
using FinancialAssistant.Analytics.Domain;
using FinancialAssistant.Analytics.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using Xunit;

namespace FinancialAssistant.Analytics.Tests;

public sealed class AnalyticsProjectorTests
{
    private static readonly string UserIdHash = new('a', 64);
    private static readonly DateTimeOffset ChangedAt =
        new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConfirmedEvents_DeriveLimitProgressCategoriesAndTrend()
    {
        var projector = new AnalyticsProjector(new InMemoryAnalyticsReadModelStore());
        await projector.ApplyAsync(CreateEvent("income", FinancialRecordEventTypes.IncomeCreated, 500m, "income.salary", new DateOnly(2026, 8, 20)), CancellationToken.None);
        await projector.ApplyAsync(CreateEvent("expense-day", FinancialRecordEventTypes.ExpenseCreated, 40m, "expense.groceries", new DateOnly(2026, 8, 20)), CancellationToken.None);
        await projector.ApplyAsync(CreateEvent("expense-prior", FinancialRecordEventTypes.ExpenseCreated, 60m, "expense.utilities", new DateOnly(2026, 8, 18)), CancellationToken.None);

        var result = await projector.GetDashboardAsync(
            UserIdHash,
            "usd",
            new DateOnly(2026, 8, 20),
            new AnalyticsExpenseLimits(50m, 120m, 300m),
            3,
            ChangedAt.AddMinutes(30),
            TimeSpan.FromHours(2),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 8, 20), result.DailySummary.PeriodStart);
        Assert.Equal(new DateOnly(2026, 8, 20), result.DailySummary.PeriodEnd);
        Assert.Equal(500m, result.DailySummary.Income);
        Assert.Equal(40m, result.DailySummary.Expense);
        Assert.Equal(460m, result.DailySummary.BalanceDelta);
        Assert.Equal(new DateOnly(2026, 8, 17), result.WeeklySummary.PeriodStart);
        Assert.Equal(new DateOnly(2026, 8, 23), result.WeeklySummary.PeriodEnd);
        Assert.Equal(500m, result.WeeklySummary.Income);
        Assert.Equal(100m, result.WeeklySummary.Expense);
        Assert.Equal(400m, result.WeeklySummary.BalanceDelta);
        Assert.Equal(new DateOnly(2026, 8, 1), result.MonthlySummary.PeriodStart);
        Assert.Equal(new DateOnly(2026, 8, 31), result.MonthlySummary.PeriodEnd);
        Assert.Equal(500m, result.MonthlySummary.Income);
        Assert.Equal(100m, result.MonthlySummary.Expense);
        Assert.Equal(400m, result.MonthlySummary.BalanceDelta);
        Assert.Equal(40m, result.DailyLimit.Spent);
        Assert.Equal(10m, result.DailyLimit.Remaining);
        Assert.Equal(80m, result.DailyLimit.UsedPercent);
        Assert.Equal(80m, result.LimitsProgress.Daily.UsedPercent);
        Assert.Equal(83.33m, result.LimitsProgress.Weekly.UsedPercent);
        Assert.Equal(33.33m, result.LimitsProgress.Monthly.UsedPercent);
        Assert.Equal(1, result.LimitsProgress.TrackingStreak.CurrentDays);
        Assert.Equal(new DateOnly(2026, 8, 20), result.LimitsProgress.TrackingStreak.LastTrackedDate);
        Assert.Equal(500m, result.MonthlyProgress.Income);
        Assert.Equal(100m, result.MonthlyProgress.Expense);
        Assert.Equal(20m, result.MonthlyProgress.ExpenseToIncomePercent);
        Assert.Equal(3, result.RecentTrend.Count);
        Assert.Equal(3, result.CategoryTotals.Count);
        Assert.False(result.IsStale);
    }

    [Fact]
    public async Task TrackingStreak_PreservesLatestTrackedDateAfterGap()
    {
        var projector = new AnalyticsProjector(new InMemoryAnalyticsReadModelStore());
        await projector.ApplyAsync(
            CreateEvent(
                "expense-before-gap",
                FinancialRecordEventTypes.ExpenseCreated,
                25m,
                "expense.groceries",
                new DateOnly(2026, 8, 18)),
            CancellationToken.None);

        var result = await projector.GetDashboardAsync(
            UserIdHash,
            "USD",
            new DateOnly(2026, 8, 20),
            AnalyticsExpenseLimits.Unconfigured,
            3,
            ChangedAt.AddMinutes(30),
            TimeSpan.FromHours(2),
            CancellationToken.None);

        Assert.Equal(0, result.LimitsProgress.TrackingStreak.CurrentDays);
        Assert.Equal(
            new DateOnly(2026, 8, 18),
            result.LimitsProgress.TrackingStreak.LastTrackedDate);
    }

    [Fact]
    public async Task ReplayArchiveAndOwnerCurrencyBoundaries_AreDeterministic()
    {
        var projector = new AnalyticsProjector(new InMemoryAnalyticsReadModelStore());
        var created = CreateEvent("expense", FinancialRecordEventTypes.ExpenseCreated, 40m, "expense.groceries", new DateOnly(2026, 8, 20));
        var archived = CreateEvent("expense", FinancialRecordEventTypes.ExpenseArchived, 40m, "expense.groceries", new DateOnly(2026, 8, 20), revision: 1, status: "archived");

        await projector.ApplyAsync(archived, CancellationToken.None);
        await projector.ApplyAsync(created, CancellationToken.None);
        await projector.ApplyAsync(created, CancellationToken.None);

        var result = await projector.GetDashboardAsync(
            UserIdHash,
            "USD",
            new DateOnly(2026, 8, 20),
            AnalyticsExpenseLimits.Unconfigured,
            7,
            ChangedAt.AddHours(3),
            TimeSpan.FromHours(2),
            CancellationToken.None);

        Assert.False(result.DailyLimit.IsConfigured);
        Assert.False(result.LimitsProgress.Daily.IsConfigured);
        Assert.False(result.LimitsProgress.Weekly.IsConfigured);
        Assert.False(result.LimitsProgress.Monthly.IsConfigured);
        Assert.Equal(0, result.LimitsProgress.TrackingStreak.CurrentDays);
        Assert.Null(result.LimitsProgress.TrackingStreak.LastTrackedDate);
        Assert.Equal(0m, result.DailySummary.Income);
        Assert.Equal(0m, result.DailySummary.Expense);
        Assert.Equal(0m, result.DailySummary.BalanceDelta);
        Assert.Equal(0m, result.WeeklySummary.Income);
        Assert.Equal(0m, result.WeeklySummary.Expense);
        Assert.Equal(0m, result.WeeklySummary.BalanceDelta);
        Assert.Equal(0m, result.MonthlySummary.Income);
        Assert.Equal(0m, result.MonthlySummary.Expense);
        Assert.Equal(0m, result.MonthlySummary.BalanceDelta);
        Assert.Equal(0m, result.DailyLimit.Spent);
        Assert.Empty(result.CategoryTotals);
        Assert.True(result.IsStale);
    }

    [Fact]
    public async Task Store_MaterializesDailyMonthlyAndCurrencyScopedAggregates()
    {
        var store = new InMemoryAnalyticsReadModelStore();
        var projector = new AnalyticsProjector(store);
        await projector.ApplyAsync(
            CreateEvent(
                "movable-expense",
                FinancialRecordEventTypes.ExpenseCreated,
                40m,
                "expense.groceries",
                new DateOnly(2026, 8, 20)),
            CancellationToken.None);
        await projector.ApplyAsync(
            CreateEvent(
                "movable-expense",
                FinancialRecordEventTypes.ExpenseUpdated,
                45m,
                "expense.groceries",
                new DateOnly(2026, 8, 20),
                revision: 1,
                currency: "EUR"),
            CancellationToken.None);

        var usd = await store.GetAsync(UserIdHash, "USD", CancellationToken.None);
        var eur = await store.GetAsync(UserIdHash, "EUR", CancellationToken.None);

        Assert.Empty(usd.DailyTotals);
        Assert.Empty(usd.WeeklyTotals);
        Assert.Empty(usd.MonthlyTotals);
        Assert.Equal(45m, eur.DailyTotals[new DateOnly(2026, 8, 20)].Expense);
        Assert.Equal(
            45m,
            eur.WeeklyTotals[new DateOnly(2026, 8, 17)].Expense);
        Assert.Equal(
            45m,
            eur.MonthlyTotals[new DateOnly(2026, 8, 1)].Totals.Expense);
    }

    [Fact]
    public async Task AcceptedProjection_PublishesIdempotentAnalyticsSnapshot()
    {
        var publisher = new InMemoryAnalyticsEventPublisher();
        var projector = new AnalyticsProjector(
            new InMemoryAnalyticsReadModelStore(),
            publisher);
        var created = CreateEvent(
            "published-expense",
            FinancialRecordEventTypes.ExpenseCreated,
            40m,
            "expense.groceries",
            new DateOnly(2026, 8, 20));

        await projector.ApplyAsync(created, CancellationToken.None);
        await projector.ApplyAsync(created, CancellationToken.None);

        var published = Assert.Single(publisher.Published);
        Assert.Equal(AnalyticsEventTypes.AnalyticsUpdated, published.EventType);
        Assert.Equal(40m, published.Payload.MonthlyExpenseTotal);
        Assert.Equal(40m, published.Payload.DailyExpenseSpent);
        Assert.Equal("expense.groceries", published.Payload.TopExpenseCategoryId);
        Assert.Equal(40m, published.Payload.TopExpenseCategoryAmount);
        Assert.Equal(0m, published.Payload.UncategorizedExpenseTotal);
        Assert.Null(published.Payload.DailyExpenseLimit);
    }

    [Fact]
    public async Task UncategorizedExpenseFact_IsPublished()
    {
        var publisher = new InMemoryAnalyticsEventPublisher();
        var projector = new AnalyticsProjector(
            new InMemoryAnalyticsReadModelStore(),
            publisher);

        await projector.ApplyAsync(
            CreateEvent(
                "published-uncategorized",
                FinancialRecordEventTypes.ExpenseCreated,
                25m,
                null,
                new DateOnly(2026, 8, 20)),
            CancellationToken.None);

        var published = Assert.Single(publisher.Published);
        Assert.Equal(
            AnalyticsCategoryIds.Uncategorized,
            published.Payload.TopExpenseCategoryId);
        Assert.Equal(25m, published.Payload.TopExpenseCategoryAmount);
        Assert.Equal(25m, published.Payload.UncategorizedExpenseTotal);
    }

    [Fact]
    public async Task CurrencyMove_PublishesBothAffectedScopes()
    {
        var publisher = new InMemoryAnalyticsEventPublisher();
        var projector = new AnalyticsProjector(
            new InMemoryAnalyticsReadModelStore(),
            publisher);
        await projector.ApplyAsync(
            CreateEvent(
                "published-move",
                FinancialRecordEventTypes.ExpenseCreated,
                40m,
                "expense.groceries",
                new DateOnly(2026, 8, 20)),
            CancellationToken.None);
        await projector.ApplyAsync(
            CreateEvent(
                "published-move",
                FinancialRecordEventTypes.ExpenseUpdated,
                45m,
                "expense.groceries",
                new DateOnly(2026, 8, 20),
                revision: 1,
                currency: "EUR"),
            CancellationToken.None);

        var latest = publisher.Published.Skip(1).ToArray();
        Assert.Equal(new[] { "EUR", "USD" }, latest.Select(item => item.Payload.Currency));
        Assert.Equal(45m, latest[0].Payload.MonthlyExpenseTotal);
        Assert.Equal(0m, latest[1].Payload.MonthlyExpenseTotal);
    }

    [Fact]
    public async Task FailedCurrencyPublication_IsRetriedWithoutRepublishingCompletedScopes()
    {
        var publisher = new FailOnceAnalyticsEventPublisher();
        var projector = new AnalyticsProjector(
            new InMemoryAnalyticsReadModelStore(),
            publisher);
        await projector.ApplyAsync(
            CreateEvent(
                "retry-move",
                FinancialRecordEventTypes.ExpenseCreated,
                40m,
                "expense.groceries",
                new DateOnly(2026, 8, 20)),
            CancellationToken.None);
        var moved = CreateEvent(
            "retry-move",
            FinancialRecordEventTypes.ExpenseUpdated,
            45m,
            "expense.groceries",
            new DateOnly(2026, 8, 20),
            revision: 1,
            currency: "EUR");
        publisher.FailNextForCurrency = "USD";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            projector.ApplyAsync(moved, CancellationToken.None));
        await projector.ApplyAsync(moved, CancellationToken.None);
        await projector.ApplyAsync(moved, CancellationToken.None);

        Assert.Equal(
            new[] { "USD", "EUR", "USD" },
            publisher.Published.Select(item => item.Payload.Currency));
    }

    [Fact]
    public async Task HistoricalChange_PublishesCurrentReportingPeriod()
    {
        var publisher = new InMemoryAnalyticsEventPublisher();
        var projector = new AnalyticsProjector(
            new InMemoryAnalyticsReadModelStore(),
            publisher);

        await projector.ApplyAsync(
            CreateEvent(
                "historical-expense",
                FinancialRecordEventTypes.ExpenseCreated,
                40m,
                "expense.groceries",
                new DateOnly(2026, 7, 10)),
            CancellationToken.None);

        var published = Assert.Single(publisher.Published);
        Assert.Equal(new DateOnly(2026, 8, 20), published.Payload.ReferenceDate);
        Assert.Equal(0m, published.Payload.MonthlyExpenseTotal);
        Assert.Equal(0m, published.Payload.DailyExpenseSpent);
        Assert.Null(published.Payload.TopExpenseCategoryId);
    }

    [Fact]
    public async Task ConfiguredDailyLimit_IsIncludedInPublishedAnalytics()
    {
        var publisher = new InMemoryAnalyticsEventPublisher();
        var limits = new InMemoryAnalyticsDailyLimitProvider();
        limits.Set(UserIdHash, "USD", 50m);
        var projector = new AnalyticsProjector(
            new InMemoryAnalyticsReadModelStore(),
            publisher,
            limits);

        await projector.ApplyAsync(
            CreateEvent(
                "limit-expense",
                FinancialRecordEventTypes.ExpenseCreated,
                60m,
                "expense.groceries",
                new DateOnly(2026, 8, 20)),
            CancellationToken.None);

        var published = Assert.Single(publisher.Published);
        Assert.Equal(50m, published.Payload.DailyExpenseLimit);
        Assert.Equal(60m, published.Payload.DailyExpenseSpent);
    }

    [Fact]
    public async Task CategoryBreakdown_SupportsPeriodsSharesTopAndUncategorized()
    {
        var projector = new AnalyticsProjector(new InMemoryAnalyticsReadModelStore());
        var referenceDate = new DateOnly(2026, 8, 20);
        await projector.ApplyAsync(
            CreateEvent(
                "breakdown-income",
                FinancialRecordEventTypes.IncomeCreated,
                1000m,
                "income.salary",
                referenceDate),
            CancellationToken.None);
        await projector.ApplyAsync(
            CreateEvent(
                "breakdown-groceries",
                FinancialRecordEventTypes.ExpenseCreated,
                100m,
                "expense.groceries",
                referenceDate),
            CancellationToken.None);
        await projector.ApplyAsync(
            CreateEvent(
                "breakdown-utilities",
                FinancialRecordEventTypes.ExpenseCreated,
                75m,
                "expense.utilities",
                referenceDate),
            CancellationToken.None);
        await projector.ApplyAsync(
            CreateEvent(
                "breakdown-uncategorized",
                FinancialRecordEventTypes.ExpenseCreated,
                50m,
                " ",
                referenceDate),
            CancellationToken.None);
        await projector.ApplyAsync(
            CreateEvent(
                "breakdown-prior",
                FinancialRecordEventTypes.ExpenseCreated,
                25m,
                "expense.entertainment",
                new DateOnly(2026, 8, 10)),
            CancellationToken.None);

        var daily = await projector.GetCategoryBreakdownAsync(
            UserIdHash,
            "usd",
            referenceDate,
            AnalyticsBreakdownPeriods.Daily,
            2,
            ChangedAt.AddHours(1),
            TimeSpan.FromHours(2),
            CancellationToken.None);

        Assert.Equal(referenceDate, daily.PeriodStart);
        Assert.Equal(referenceDate, daily.PeriodEnd);
        Assert.False(daily.IsStale);
        Assert.Equal(ChangedAt, daily.LastEventAtUtc);
        Assert.Equal(4, daily.Categories.Count);
        var salary = Assert.Single(
            daily.Categories,
            item => item.CategoryId == "income.salary");
        Assert.Equal(100m, salary.IncomeSharePercent);
        Assert.Equal(0m, salary.ExpenseSharePercent);
        var groceries = Assert.Single(
            daily.Categories,
            item => item.CategoryId == "expense.groceries");
        Assert.Equal(44.44m, groceries.ExpenseSharePercent);
        var uncategorized = Assert.Single(
            daily.Categories,
            item => item.CategoryId == AnalyticsCategoryIds.Uncategorized);
        Assert.Equal(22.22m, uncategorized.ExpenseSharePercent);
        Assert.Equal(
            new[] { "income.salary" },
            daily.TopIncomeCategories.Select(item => item.CategoryId));
        Assert.Equal(
            new[] { "expense.groceries", "expense.utilities" },
            daily.TopExpenseCategories.Select(item => item.CategoryId));

        var weekly = await projector.GetCategoryBreakdownAsync(
            UserIdHash,
            "USD",
            referenceDate,
            AnalyticsBreakdownPeriods.Weekly,
            2,
            ChangedAt.AddHours(1),
            TimeSpan.FromHours(2),
            CancellationToken.None);
        Assert.Equal(new DateOnly(2026, 8, 17), weekly.PeriodStart);
        Assert.Equal(new DateOnly(2026, 8, 23), weekly.PeriodEnd);
        Assert.Equal(4, weekly.Categories.Count);

        var monthly = await projector.GetCategoryBreakdownAsync(
            UserIdHash,
            "USD",
            referenceDate,
            AnalyticsBreakdownPeriods.Monthly,
            2,
            ChangedAt.AddHours(1),
            TimeSpan.FromHours(2),
            CancellationToken.None);
        Assert.Equal(new DateOnly(2026, 8, 1), monthly.PeriodStart);
        Assert.Equal(new DateOnly(2026, 8, 31), monthly.PeriodEnd);
        Assert.Equal(5, monthly.Categories.Count);

        var empty = await projector.GetCategoryBreakdownAsync(
            UserIdHash,
            "USD",
            referenceDate.AddDays(1),
            AnalyticsBreakdownPeriods.Daily,
            2,
            ChangedAt.AddHours(1),
            TimeSpan.FromHours(2),
            CancellationToken.None);
        Assert.Empty(empty.Categories);
        Assert.Empty(empty.TopIncomeCategories);
        Assert.Empty(empty.TopExpenseCategories);
        Assert.False(empty.IsStale);

        var neverProjected = await new AnalyticsProjector(
            new InMemoryAnalyticsReadModelStore()).GetCategoryBreakdownAsync(
                UserIdHash,
                "USD",
                referenceDate,
                AnalyticsBreakdownPeriods.Daily,
                2,
                ChangedAt.AddHours(1),
                TimeSpan.FromHours(2),
                CancellationToken.None);
        Assert.True(neverProjected.IsStale);
        Assert.Null(neverProjected.LastEventAtUtc);
    }

    private static IntegrationEventEnvelope<FinancialRecordChangedV1> CreateEvent(
        string recordId,
        string eventType,
        decimal amount,
        string? categoryId,
        DateOnly date,
        long revision = 0,
        string status = "active",
        string currency = "USD") =>
        new(
            $"event-{recordId}-{revision}",
            $"occurrence-{recordId}-{revision}",
            eventType,
            ChangedAt.AddMinutes(revision),
            eventType.StartsWith("income", StringComparison.Ordinal) ? "income-service" : "expense-service",
            FinancialRecordEventTypes.SchemaVersion,
            "synthetic-correlation",
            "synthetic-causation",
            UserIdHash,
            new FinancialRecordChangedV1(
                recordId,
                amount,
                currency,
                categoryId,
                date,
                status,
                revision,
                "manual",
                ChangedAt.AddMinutes(revision)));

    private sealed class FailOnceAnalyticsEventPublisher : IAnalyticsEventPublisher
    {
        private readonly List<IntegrationEventEnvelope<AnalyticsUpdatedV1>> published = [];

        public string? FailNextForCurrency { get; set; }

        public IReadOnlyList<IntegrationEventEnvelope<AnalyticsUpdatedV1>> Published => published;

        public Task PublishAsync(
            IntegrationEventEnvelope<AnalyticsUpdatedV1> envelope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (envelope.Payload.Currency == FailNextForCurrency)
            {
                FailNextForCurrency = null;
                throw new InvalidOperationException("Synthetic transient publication failure.");
            }

            published.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
