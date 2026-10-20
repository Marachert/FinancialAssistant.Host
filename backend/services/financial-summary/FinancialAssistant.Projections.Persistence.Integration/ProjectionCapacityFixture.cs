using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Contracts;
using FinancialAssistant.Analytics.Domain;
using FinancialAssistant.Analytics.Infrastructure.Persistence;
using FinancialAssistant.FinancialSummary.Domain;
using FinancialAssistant.FinancialSummary.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

internal static class ProjectionCapacityFixture
{
    public static async Task VerifyAsync(NativePostgresSettings summarySettings, NativePostgresSettings analyticsSettings, string mode)
    {
        using var summaryProtection = summarySettings.CreateProtection();
        await using var summaryDatabase = new OwnedPostgresStore(summarySettings.CreateOptions("financial-summary"), summaryProtection);
        var summary = new PostgresFinancialSummaryReadModelStore(summaryDatabase);
        var owner = new string('3', 64);
        var day = new DateOnly(2026, 10, 20);
        var time = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var summarySource = Enumerable.Range(0, 1000).Select(i => new FinancialRecordProjection("expense", "synthetic-summary-capacity-" + i.ToString("D4"),
            owner, 0.0001m, "USD", "expense.synthetic", day, "active", 0, "manual", time, "synthetic-summary-capacity-event-" + i.ToString("D4"))).ToArray();
        if (mode == "initial")
        {
            var request = new SummaryRebuildRequest(owner, day, day, "synthetic-capacity-v1", time, 1000,
                PostgresFinancialSummaryReadModelStore.CalculateSourceDigest(summarySource));
            var job = await summary.BeginRebuildAsync(request);
            for (var page = 0; page < 10; page++) { await summary.StageRebuildPageAsync(owner, job.JobKey, page, summarySource.Skip(page * 100).Take(100).ToArray()); }
            await summary.CommitRebuildAsync(owner, job.JobKey);
            Check(!(await summary.CleanupRebuildAsync(owner, job.JobKey)).CleanupPending);
        }
        var values = await summary.ListAsync(owner, "USD", default);
        Check(values.Count == 1000 && values.Sum(value => value.Amount) == 0.1m);
        await CapacityDenied(() => summary.UpsertIfNewerAsync(summarySource[0] with { RecordId = "synthetic-summary-overflow", EventId = "synthetic-summary-overflow-event" }, default));
        if (mode == "restart")
        {
            await summary.UpsertIfNewerAsync(summarySource[0] with
            {
                Revision = 1,
                Currency = "EUR",
                Amount = 0.0002m,
                EventId = "synthetic-summary-capacity-correction",
                ChangedAtUtc = time.AddMinutes(1)
            }, default);
            Check((await summary.ListAsync(owner, "USD", default)).Count == 999 && (await summary.ListAsync(owner, "EUR", default)).Single().Amount == 0.0002m);
        }

        using var analyticsProtection = analyticsSettings.CreateProtection();
        await using var analyticsDatabase = new OwnedPostgresStore(analyticsSettings.CreateOptions("analytics"), analyticsProtection);
        var context = new AnalyticsPersistenceContext(analyticsDatabase);
        var analytics = new PostgresAnalyticsReadModelStore(context);
        var analyticsOwner = new string('6', 64);
        var analyticsSource = Enumerable.Range(0, 1000).Select(i => new AnalyticsRecordProjection("expense", "synthetic-analytics-capacity-" + i.ToString("D4"),
            analyticsOwner, 0.0001m, "USD", "expense.synthetic", day, "active", 0, time, "synthetic-analytics-capacity-event-" + i.ToString("D4"))).ToArray();
        if (mode == "initial")
        {
            var request = new AnalyticsRebuildRequest(analyticsOwner, day, day, "synthetic-capacity-v1", time);
            var job = await analytics.BeginRebuildAsync(request, 1000, PostgresAnalyticsReadModelStore.CalculateSourceDigest(analyticsSource));
            for (var page = 0; page < 10; page++) { await analytics.StageRebuildPageAsync(analyticsOwner, job.GenerationId, page, analyticsSource.Skip(page * 100).Take(100).ToArray()); }
            await analytics.CommitRebuildAsync(analyticsOwner, job.GenerationId);
            await analytics.MarkRebuildRefreshCompletedAsync(await analytics.ReadPendingRebuildRefreshAsync(analyticsOwner) ?? throw new VerificationFailure());
            Check(!(await analytics.CleanupRebuildAsync(analyticsOwner, job.GenerationId)).CleanupPending);
        }
        Check((await analytics.GetAsync(analyticsOwner, "USD", default)).DailyTotals[day].Expense == 0.1m);
        await CapacityDenied(() => analytics.UpsertIfNewerAsync(analyticsSource[0] with { RecordId = "synthetic-analytics-overflow", EventId = "synthetic-analytics-overflow-event" }, default));
        if (mode == "restart")
        {
            var limits = new PostgresAnalyticsLimitProvider(context, new FixtureClock());
            foreach (var currency in new[] { "USD", "EUR" })
            { await limits.ApplySnapshotAsync(new AnalyticsLimitSnapshot(analyticsOwner, currency, "synthetic-capacity-settings", 0, AnalyticsExpenseLimits.Unconfigured, time)); }
            var change = new IntegrationEventEnvelope<FinancialRecordChangedV1>("synthetic-analytics-capacity-correction", "synthetic-analytics-capacity-correction",
                FinancialRecordEventTypes.ExpenseUpdated, time.AddMinutes(1), "financial-assistant-expense-service", 1, "synthetic-capacity-correlation",
                "synthetic-capacity-cause", analyticsOwner, new FinancialRecordChangedV1(analyticsSource[0].RecordId, 0.0002m, "EUR", "expense.synthetic",
                    day, "active", 1, "manual", time.AddMinutes(1)));
            await new AnalyticsProjector(analytics, new PostgresAnalyticsEventPublisher(context), limits, context).ApplyAsync(change, default);
            Check((await analytics.GetAsync(analyticsOwner, "USD", default)).DailyTotals[day].Expense == 0.0999m &&
                (await analytics.GetAsync(analyticsOwner, "EUR", default)).DailyTotals[day].Expense == 0.0002m);
        }
        Console.WriteLine("PASS bounded1000-record generations mode=" + mode + ": ten-page staging, bounded atomic commit/read, capacity refusal and corrections without cross-currency totals.");
    }

    private static async Task CapacityDenied(Func<Task> operation)
    { try { await operation(); throw new VerificationFailure(); } catch (StorageUnavailableException) { } }
    private static void Check(bool value) { if (!value) { throw new VerificationFailure(); } }
    private sealed class FixtureClock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 10, 20, 13, 0, 0, TimeSpan.Zero); }
    private sealed class VerificationFailure : Exception;
}
