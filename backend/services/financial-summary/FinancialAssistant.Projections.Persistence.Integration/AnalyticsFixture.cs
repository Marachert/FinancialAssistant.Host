using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Domain;
using FinancialAssistant.Analytics.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

internal static class AnalyticsFixture
{
    public static async Task VerifyAsync(NativePostgresSettings settings, string mode)
    {
        using var protection = settings.CreateProtection();
        await using var database = new OwnedPostgresStore(settings.CreateOptions("analytics"), protection);
        await using var secondDatabase = new OwnedPostgresStore(settings.CreateOptions("analytics"), protection);
        var context = new AnalyticsPersistenceContext(database);
        var secondContext = new AnalyticsPersistenceContext(secondDatabase);
        var store = new PostgresAnalyticsReadModelStore(context);
        var publisher = new PostgresAnalyticsEventPublisher(context);
        var clock = new FixtureClock { Now = new DateTimeOffset(2026, 10, 20, mode == "initial" ? 13 : 15, 0, 0, TimeSpan.Zero) };
        var limits = new PostgresAnalyticsLimitProvider(context, clock);
        var projector = new AnalyticsProjector(store, publisher, limits, context);
        var second = new AnalyticsProjector(new PostgresAnalyticsReadModelStore(secondContext),
            new PostgresAnalyticsEventPublisher(secondContext), new PostgresAnalyticsLimitProvider(secondContext, clock), secondContext);
        var owner = new string('8', 64);
        var other = new string('9', 64);
        var expense = Source(owner, "expense", "synthetic-analytics-expense", 12.3401m);
        var income = Source(owner, "income", "synthetic-analytics-income", 50.0002m);
        var moved = Copy(expense, "synthetic-analytics-moved", expense.Payload with
        {
            Currency = "EUR",
            Revision = 1,
            ChangedAtUtc = expense.Payload.ChangedAtUtc.AddMinutes(1)
        }, FinancialRecordEventTypes.ExpenseUpdated);
        var archived = Copy(moved, "synthetic-analytics-archived", moved.Payload with
        {
            Status = "archived",
            Revision = 2,
            ChangedAtUtc = expense.Payload.ChangedAtUtc.AddMinutes(2)
        }, FinancialRecordEventTypes.ExpenseArchived);
        var sibling = Source(other, "income", "synthetic-analytics-other", 33.0042m, "EUR");
        var failed = Source(owner, "expense", "synthetic-analytics-failed", 2.11m);
        if (mode == "initial")
        {
            try { await limits.GetExpenseLimitsAsync(owner, "USD", expense.Payload.Date, default); throw new VerificationFailure(); }
            catch (StorageUnavailableException) { }
            foreach (var currency in new[] { "USD", "EUR" })
            {
                Check(await limits.ApplySnapshotAsync(new AnalyticsLimitSnapshot(owner, currency, "synthetic-settings-v1", 1,
                    new AnalyticsExpenseLimits(currency == "USD" ? 50m : 60m, 200m, 1000m), expense.OccurredAtUtc)));
            }
            Check(await limits.ApplySnapshotAsync(new AnalyticsLimitSnapshot(other, "EUR", "synthetic-settings-v1", 1,
                AnalyticsExpenseLimits.Unconfigured, expense.OccurredAtUtc)));
            await Task.WhenAll(projector.ApplyAsync(expense, default), second.ApplyAsync(expense, default));
            var firstPublication = (await Pending(publisher, owner, other)).Single();
            Check(firstPublication.Event.Payload.MonthlyIncomeTotal == 0 && firstPublication.Event.Payload.MonthlyExpenseTotal == 12.3401m &&
                firstPublication.Event.Payload.DailyExpenseLimit == 50m);
            await projector.ApplyAsync(income, default);
            await projector.ApplyAsync(moved, default);
            await projector.ApplyAsync(archived, default);
            await projector.ApplyAsync(sibling, default);
            await projector.ApplyAsync(expense, default);
            await projector.ApplyAsync(moved, default);
            var pending = await Pending(publisher, owner, other);
            Check(pending.Count == 6 && !publisher.ConfirmsDelivery);
            Check(pending.Single(message => message.Event.EventId == firstPublication.Event.EventId) == firstPublication);
            var movedEur = pending.Single(message => message.Event.CausationId == moved.EventId && message.Event.Payload.Currency == "EUR");
            Check(movedEur.Event.Payload.MonthlyExpenseTotal == 12.3401m && movedEur.Event.Payload.DailyExpenseLimit == 60m);
            Check(pending.Single(message => message.Event.CausationId == sibling.EventId).Event.Payload.DailyExpenseLimit is null);
            var fakePayload = firstPublication.Event.Payload with { MonthlyExpenseTotal = 13.3401m };
            var fakeEvent = new IntegrationEventEnvelope<AnalyticsUpdatedV1>(firstPublication.Event.EventId, firstPublication.Event.OccurrenceId,
                firstPublication.Event.EventType, firstPublication.Event.OccurredAtUtc, firstPublication.Event.Producer,
                firstPublication.Event.SchemaVersion, firstPublication.Event.CorrelationId, firstPublication.Event.CausationId,
                firstPublication.Event.UserIdHash, fakePayload);
            await Conflict(() => publisher.MarkPublishedAsync(new AnalyticsOutboxMessage(fakeEvent, null), expense.OccurredAtUtc.AddHours(1)));
            await publisher.MarkPublishedAsync(firstPublication, expense.OccurredAtUtc.AddHours(1));
            await publisher.MarkPublishedAsync(firstPublication, expense.OccurredAtUtc.AddHours(2));
            Check(await publisher.CleanupPublishedBatchAsync(expense.OccurredAtUtc.AddDays(32)) == 1);
            await projector.ApplyAsync(expense, default);
            Check((await Pending(publisher, owner, other)).Count == 5);
            var movedUsd = (await Pending(publisher, owner, other)).Single(message => message.Event.CausationId == moved.EventId && message.Event.Payload.Currency == "USD");
            await publisher.MarkPublishedAsync(movedUsd, moved.OccurredAtUtc.AddHours(1));
            await Conflict(() => projector.ApplyAsync(Copy(expense, expense.EventId, expense.Payload with { Amount = 99m }), default));
            var changedCorrelation = new IntegrationEventEnvelope<FinancialRecordChangedV1>(expense.EventId, expense.OccurrenceId, expense.EventType,
                expense.OccurredAtUtc, expense.Producer, expense.SchemaVersion, "synthetic-changed-correlation", expense.CausationId, owner, expense.Payload);
            await Conflict(() => projector.ApplyAsync(changedCorrelation, default));
            await Conflict(() => context.ExecuteAsync(Source(owner, "expense", "synthetic-cross-owner-scope", 1m), token => store.UpsertIfNewerAsync(
                AnalyticsPersistenceContext.ReadSource(sibling), token), default));
            try
            {
                await new AnalyticsProjector(store, new FailedPublisher(), limits, context).ApplyAsync(failed, default);
                throw new VerificationFailure();
            }
            catch (SyntheticPublisherFailure) { }
            var noPublisher = Source(owner, "expense", "synthetic-analytics-no-publisher", 1m);
            try { await new AnalyticsProjector(store, null, null, context).ApplyAsync(noPublisher, default); throw new VerificationFailure(); }
            catch (NotSupportedException) { }
            await context.WithRecordsAsync(owner, async (records, _) =>
            {
                foreach (var source in new[] { failed, noPublisher })
                {
                    Check(await records.GetAsync("event_receipts", owner, source.EventId) is null);
                    Check(await records.GetAsync("source_receipts", owner, source.EventId) is null);
                    Check(await records.GetAsync("projection_heads", owner, "expense:" + source.Payload.RecordId) is null);
                }
                return true;
            });
            clock.Now = clock.Now.AddHours(2);
            try { await limits.GetExpenseLimitsAsync(owner, "USD", expense.Payload.Date, default); throw new VerificationFailure(); }
            catch (StorageUnavailableException) { }
            await projector.ApplyAsync(moved, default);
            var expired = Source(owner, "expense", "synthetic-expired-limit-input", 1m);
            try { await projector.ApplyAsync(expired, default); throw new VerificationFailure(); }
            catch (StorageUnavailableException) { }
            foreach (var currency in new[] { "USD", "EUR" })
            {
                var fresh = new AnalyticsLimitSnapshot(owner, currency, "synthetic-settings-v1", 1,
                    new AnalyticsExpenseLimits(currency == "USD" ? 50m : 60m, 200m, 1000m), clock.Now);
                Check(await limits.ApplySnapshotAsync(fresh));
                Check(!await limits.ApplySnapshotAsync(fresh));
                Check(!await limits.ApplySnapshotAsync(fresh with { SourceRevision = 0 }));
                await Conflict(() => limits.ApplySnapshotAsync(fresh with { Limits = new AnalyticsExpenseLimits(99m, 200m, 1000m) }));
            }
            Check(await limits.ApplySnapshotAsync(new AnalyticsLimitSnapshot(other, "EUR", "synthetic-settings-v1", 1,
                AnalyticsExpenseLimits.Unconfigured, clock.Now)));
        }
        await projector.ApplyAsync(expense, default);
        await projector.ApplyAsync(moved, default);
        var currentUsd = await store.GetAsync(owner, "USD", default);
        var currentEur = await store.GetAsync(owner, "EUR", default);
        var day = expense.Payload.Date;
        Check(currentUsd.DailyTotals[day].Income == 50.0002m && currentUsd.DailyTotals[day].Expense == 0 && currentEur.DailyTotals.Count == 0);
        Check(currentEur.LastEventAtUtc == archived.Payload.ChangedAtUtc);
        Check((await store.GetAsync(other, "EUR", default)).DailyTotals[day].Income == 33.0042m);
        Check((await limits.GetExpenseLimitsAsync(owner, "USD", day, default)).Daily == 50m);
        Check(await limits.GetDailyExpenseLimitAsync(other, "EUR", day, default) is null);
        try { await limits.GetExpenseLimitsAsync(other, "USD", day, default); throw new VerificationFailure(); }
        catch (StorageUnavailableException) { }
        var outcome = await store.UpsertIfNewerAsync(AnalyticsPersistenceContext.ReadSource(moved), default);
        Check(!outcome.Accepted && outcome.PendingPublicationCurrencies.SequenceEqual(new[] { "EUR" }));
        var stillPending = await Pending(publisher, owner, other);
        Check(stillPending.Count == 4 && stillPending.All(message => message.Event.CausationId != expense.EventId));
        Check(stillPending.Single(message => message.Event.CausationId == moved.EventId).Event.Payload.MonthlyExpenseTotal == 12.3401m);
        if (mode == "restart")
        {
            var originalEur = stillPending.Single(message => message.Event.CausationId == moved.EventId);
            await publisher.MarkPublishedAsync(originalEur, moved.OccurredAtUtc.AddHours(2));
            Check((await store.UpsertIfNewerAsync(AnalyticsPersistenceContext.ReadSource(moved), default)).PendingPublicationCurrencies.Count == 0);
            await projector.ApplyAsync(moved, default);
            Check((await Pending(publisher, owner, other)).Count == 3);
            await projector.ApplyAsync(failed, default);
            Check((await store.GetAsync(owner, "USD", default)).DailyTotals[day].Expense == 2.11m);
            Check(await publisher.CleanupPublishedBatchAsync(expense.OccurredAtUtc.AddDays(32)) == 2);
            Check((await Pending(publisher, owner, other)).Count == 4);
        }
        Console.WriteLine("PASS Analytics owned PostgreSQL mode=" + mode + ": exact totals, atomic source/projection/outbox, immutable replay after newer state, two-currency pending completion, exact acknowledgement, pending-safe retention and failure rollback. No broker delivery claimed.");
    }

    private static IntegrationEventEnvelope<FinancialRecordChangedV1> Source(string owner, string type, string recordId, decimal amount, string currency = "USD") =>
        new(recordId + "-event", recordId + "-event", type + ".created.v1", new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero),
            "financial-assistant-" + type + "-service", 1, "synthetic-analytics-correlation", "synthetic-financial-cause", owner,
            new FinancialRecordChangedV1(recordId, amount, currency, type + ".synthetic", new DateOnly(2026, 10, 20),
                "active", 0, "manual", new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero)));
    private static IntegrationEventEnvelope<FinancialRecordChangedV1> Copy(IntegrationEventEnvelope<FinancialRecordChangedV1> source,
        string id, FinancialRecordChangedV1 payload, string? type = null) => new(id, id, type ?? source.EventType,
            payload.ChangedAtUtc, source.Producer, source.SchemaVersion, source.CorrelationId, source.CausationId, source.UserIdHash, payload);
    private static async Task Conflict(Func<Task> operation)
    { try { await operation(); throw new VerificationFailure(); } catch (StorageConflictException) { } }
    private static async Task<IReadOnlyList<AnalyticsOutboxMessage>> Pending(PostgresAnalyticsEventPublisher publisher, string firstOwner, string secondOwner) =>
        (await publisher.ReadPendingAsync()).Where(message => message.Event.UserIdHash == firstOwner || message.Event.UserIdHash == secondOwner).ToArray();
    private static void Check(bool value) { if (!value) { throw new VerificationFailure(); } }
    private sealed class FixtureClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class FailedPublisher : IAnalyticsEventPublisher
    {
        public Task PublishAsync(IntegrationEventEnvelope<AnalyticsUpdatedV1> envelope, CancellationToken cancellationToken) =>
            throw new SyntheticPublisherFailure();
    }
    private sealed class SyntheticPublisherFailure : Exception;
    private sealed class VerificationFailure : Exception;
}
