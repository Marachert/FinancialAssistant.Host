using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Contracts;
using FinancialAssistant.Analytics.Domain;
using FinancialAssistant.Analytics.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

internal static class AnalyticsRebuildFixture
{
    public static async Task VerifyAsync(NativePostgresSettings settings, string mode)
    {
        using var protection = settings.CreateProtection();
        await using var database = new OwnedPostgresStore(settings.CreateOptions("analytics"), protection);
        var context = new AnalyticsPersistenceContext(database);
        var store = new PostgresAnalyticsReadModelStore(context);
        var clock = new FixtureClock();
        var limits = new PostgresAnalyticsLimitProvider(context, clock);
        var publisher = new PostgresAnalyticsEventPublisher(context);
        var projector = new AnalyticsProjector(store, publisher, limits, context);
        var owner = new string('7', 64);
        var other = new string('2', 64);
        var day = new DateOnly(2026, 10, 20);
        var first = Projection(owner, "expense", "synthetic-analytics-rebuild-000", 4.5m);
        var before = Projection(owner, "income", "synthetic-analytics-before", 9.001m) with { Date = day.AddDays(-1) };
        var after = Projection(owner, "expense", "synthetic-analytics-after", 4.11m) with { Date = day.AddDays(1), Currency = "EUR" };
        var live = Projection(owner, "income", "synthetic-analytics-rebuild-live", 3.11m) with { Date = day.AddDays(2) };
        var obsolete = Projection(owner, "expense", "synthetic-analytics-obsolete", 12m) with { Revision = 2 };
        var sibling = Projection(other, "income", "synthetic-analytics-rebuild-sibling", 8m);
        var source = Enumerable.Range(0, 101).Select(i => Projection(owner, "expense", "synthetic-analytics-rebuild-" + i.ToString("D3"), 0.0013m)
            with
        { Currency = i % 2 == 0 ? "USD" : "EUR" }).ToArray();
        source[0] = first with { Amount = 12.4501m, Revision = 1, EventId = "synthetic-analytics-rebuild-new-first", ChangedAtUtc = first.ChangedAtUtc.AddMinutes(1) };
        var digest = PostgresAnalyticsReadModelStore.CalculateSourceDigest(source);
        var request = new AnalyticsRebuildRequest(owner, day, day, "synthetic-analytics-snapshot-v2", first.ChangedAtUtc);
        if (mode == "initial")
        {
            foreach (var currency in new[] { "USD", "EUR" })
            { await limits.ApplySnapshotAsync(new AnalyticsLimitSnapshot(owner, currency, "synthetic-rebuild-settings", 0, AnalyticsExpenseLimits.Unconfigured, first.ChangedAtUtc)); }
            await limits.ApplySnapshotAsync(new AnalyticsLimitSnapshot(other, "USD", "synthetic-rebuild-settings", 0, AnalyticsExpenseLimits.Unconfigured, first.ChangedAtUtc));
            foreach (var value in new[] { first, before, after, obsolete, sibling }) { await projector.ApplyAsync(Event(value), default); }
            var firstRequest = request with { SourceSnapshotVersion = "synthetic-analytics-snapshot-v1" };
            var initialJob = await store.BeginRebuildAsync(firstRequest, 101, digest);
            Check(initialJob.JobKey == new AnalyticsRebuildPlanner().Create(firstRequest).JobKey && initialJob.Stage == AnalyticsRebuildStages.RebuildAnalytics);
            var invalidPage = source.Take(100).ToArray(); invalidPage[^1] = invalidPage[^1] with { Date = day.AddDays(1) };
            await Conflict(() => store.StageRebuildPageAsync(owner, initialJob.GenerationId, 0, invalidPage));
            await store.StageRebuildPageAsync(owner, initialJob.GenerationId, 0, source.Take(100).ToArray());
            await Conflict(() => store.CommitRebuildAsync(owner, initialJob.GenerationId));
            await projector.ApplyAsync(Event(live), default);
            await store.StageRebuildPageAsync(owner, initialJob.GenerationId, 1, source.Skip(100).ToArray());
            await Conflict(() => store.CommitRebuildAsync(owner, initialJob.GenerationId));
            Check((await store.GetAsync(owner, "USD", default)).DailyTotals[day].Expense == 16.5m);
            await store.CancelRebuildAsync(owner, initialJob.GenerationId);
            await Cleanup(store, owner, initialJob.GenerationId);
            var interrupted = await store.BeginRebuildAsync(request, 101, digest);
            await store.StageRebuildPageAsync(owner, interrupted.GenerationId, 0, source.Take(100).ToArray());
            Check((await store.GetRebuildAsync(owner)) is { Status: "running", StagedRecords: 100 });
            return;
        }
        var recovered = await store.GetRebuildAsync(owner) ?? throw new VerificationFailure();
        Check(recovered.Status == "running" && recovered.StagedRecords == 100 && recovered.JobKey == new AnalyticsRebuildPlanner().Create(request).JobKey);
        await store.StageRebuildPageAsync(owner, recovered.GenerationId, 0, source.Take(100).ToArray());
        await store.StageRebuildPageAsync(owner, recovered.GenerationId, 1, source.Skip(100).ToArray());
        var commit = store.CommitRebuildAsync(owner, recovered.GenerationId);
        var read = store.GetAsync(owner, "USD", default);
        await Task.WhenAll(commit, read);
        Check((await read).DailyTotals[day].Expense is 16.5m or 12.5151m);
        var committed = await commit;
        Check(committed.Status == "succeeded" && committed.RefreshPending && committed.Stage == AnalyticsRebuildStages.RebuildAnalytics);
        var usd = await store.GetAsync(owner, "USD", default);
        var eur = await store.GetAsync(owner, "EUR", default);
        Check(usd.DailyTotals[day].Expense == 12.5151m && eur.DailyTotals[day].Expense == 0.065m);
        Check(usd.MonthlyTotals[new DateOnly(2026, 10, 1)].Totals.Income == 12.111m && eur.MonthlyTotals[new DateOnly(2026, 10, 1)].Totals.Expense == 4.175m);
        Check((await store.GetAsync(other, "USD", default)).DailyTotals[day].Income == 8m);
        var pending = await store.ReadPendingRebuildRefreshAsync(owner) ?? throw new VerificationFailure();
        Check(pending.Currencies.SequenceEqual(new[] { "EUR", "USD" }) && pending.SourceDigest == digest);
        await Conflict(() => store.MarkRebuildRefreshCompletedAsync(pending with { Currencies = new[] { "USD" } }));
        var later = Projection(owner, "expense", "synthetic-analytics-after-swap", 7.111m) with { Date = day.AddDays(3), ChangedAtUtc = first.ChangedAtUtc.AddMinutes(3) };
        await projector.ApplyAsync(Event(later), default);
        await Conflict(() => store.MarkRebuildRefreshCompletedAsync(pending));
        var fresh = await store.ReadPendingRebuildRefreshAsync(owner) ?? throw new VerificationFailure();
        await store.MarkRebuildRefreshCompletedAsync(fresh);
        await store.MarkRebuildRefreshCompletedAsync(fresh);
        Check(await store.ReadPendingRebuildRefreshAsync(owner) is null);
        await Cleanup(store, owner, recovered.GenerationId);
        var emptyRequest = request with { SourceSnapshotVersion = "synthetic-analytics-empty-v3" };
        var empty = await store.BeginRebuildAsync(emptyRequest, 0, PostgresAnalyticsReadModelStore.CalculateSourceDigest(Array.Empty<AnalyticsRecordProjection>()));
        await store.CommitRebuildAsync(owner, empty.GenerationId);
        Check(!(await store.GetAsync(owner, "USD", default)).DailyTotals.ContainsKey(day));
        await projector.ApplyAsync(Event(source[0]), default);
        await projector.ApplyAsync(Event(obsolete with { Revision = 1, EventId = "synthetic-analytics-stale-omitted" }), default);
        await projector.ApplyAsync(Event(obsolete), default);
        Check(!(await store.GetAsync(owner, "USD", default)).DailyTotals.ContainsKey(day));
        var newest = source[0] with { Revision = 2, EventId = "synthetic-analytics-after-empty", ChangedAtUtc = first.ChangedAtUtc.AddMinutes(4) };
        await projector.ApplyAsync(Event(newest), default);
        Check((await store.GetAsync(owner, "USD", default)).DailyTotals[day].Expense == 12.4501m);
        var refresh = await store.ReadPendingRebuildRefreshAsync(owner) ?? throw new VerificationFailure();
        await store.MarkRebuildRefreshCompletedAsync(refresh);
        await Cleanup(store, owner, empty.GenerationId);
        var originals = await publisher.ReadPendingAsync();
        Check(originals.Single(message => message.Event.CausationId == first.EventId).Event.Payload.MonthlyExpenseTotal == 4.5m);
        Check((await store.GetAsync(other, "USD", default)).DailyTotals[day].Income == 8m);
        Console.WriteLine("PASS Analytics scoped rebuild: interrupted pages recovered, stale source rejected, canonical plan identity, atomic period swap, exact totals, omitted-record replay guard and distinct durable refresh handoff. Synthetic refresh acknowledgement is not cross-owner execution.");
    }

    private static async Task Cleanup(PostgresAnalyticsReadModelStore store, string owner, string generation)
    {
        for (var i = 0; i < 42; i++) { if (!(await store.CleanupRebuildAsync(owner, generation)).CleanupPending) { return; } }
        throw new VerificationFailure();
    }
    private static AnalyticsRecordProjection Projection(string owner, string type, string id, decimal amount) =>
        new(type, id, owner, amount, "USD", type + ".synthetic", new DateOnly(2026, 10, 20), "active", 0,
            new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero), id + "-event");
    private static IntegrationEventEnvelope<FinancialRecordChangedV1> Event(AnalyticsRecordProjection value) =>
        new(value.EventId, value.EventId, value.RecordType + (value.Status == "archived" ? ".archived.v1" : value.Revision == 0 ? ".created.v1" : ".updated.v1"),
            value.ChangedAtUtc, "financial-assistant-" + value.RecordType + "-service", 1, "synthetic-rebuild-correlation", "synthetic-rebuild-cause", value.UserIdHash,
            new FinancialRecordChangedV1(value.RecordId, value.Amount, value.Currency, value.CategoryId, value.Date, value.Status,
                value.Revision, "manual", value.ChangedAtUtc));
    private static async Task Conflict(Func<Task> operation)
    { try { await operation(); throw new VerificationFailure(); } catch (StorageConflictException) { } }
    private static void Check(bool value) { if (!value) { throw new VerificationFailure(); } }
    private sealed class FixtureClock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 10, 20, 13, 0, 0, TimeSpan.Zero); }
    private sealed class VerificationFailure : Exception;
}
