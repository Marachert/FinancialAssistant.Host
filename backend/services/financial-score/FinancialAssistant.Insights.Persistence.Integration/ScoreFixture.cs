using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.FinancialScore.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;
using Npgsql;

internal static class ScoreFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    public static async Task VerifyAsync(NativePostgresSettings settings, NpgsqlConnection admin, string mode)
    {
        using var protection = settings.CreateProtection();
        await using var database = new OwnedPostgresStore(settings.CreateOptions("financial-score"), protection);
        await using var secondDatabase = new OwnedPostgresStore(settings.CreateOptions("financial-score"), protection);
        Check(await database.IsReadyAsync());
        var context = new ScorePersistenceContext(database);
        var otherContext = new ScorePersistenceContext(secondDatabase);
        var clock = new FixtureClock { Now = At.AddHours(mode == "initial" ? 1 : 4) };
        var store = new PostgresFinancialScoreStore(context);
        var publisher = new PostgresFinancialScoreEventPublisher(context);
        var profile = new PostgresFinancialScoreProfileSettingsProvider(context, clock);
        var service = new FinancialScoreService(store, publisher, new FinancialScoreCalculator(), profile, context);
        var second = new FinancialScoreService(new PostgresFinancialScoreStore(otherContext),
            new PostgresFinancialScoreEventPublisher(otherContext), new FinancialScoreCalculator(),
            new PostgresFinancialScoreProfileSettingsProvider(otherContext, clock), otherContext);
        var owner = new string('a', 64);
        var other = new string('b', 64);
        var expense = Source(owner, "synthetic-score-expense", 12.3401m);
        var income = Source(owner, "synthetic-score-income", 50.0002m, type: FinancialRecordEventTypes.IncomeCreated);
        var moved = Copy(expense, "synthetic-score-move", expense.Payload with { Currency = "EUR", Revision = 1, ChangedAtUtc = At.AddMinutes(1) });
        var archived = Copy(moved, "synthetic-score-archive", moved.Payload with { Status = "archived", Revision = 2, ChangedAtUtc = At.AddMinutes(2) }, FinancialRecordEventTypes.ExpenseArchived);
        if (mode == "initial")
        {
            await Unavailable(() => service.GetCurrentOrCreateDefaultAsync(other, "USD", default));
            foreach (var currency in new[] { "USD", "EUR" })
            {
                Check(await profile.ApplySnapshotAsync(new ScoreProfileSnapshot(owner, currency, "synthetic-v1", 1,
                    new FinancialScoreProfileSettings(1000m, true, false), At)));
            }
            Check(await profile.ApplySnapshotAsync(new ScoreProfileSnapshot(other, "USD", "synthetic-v1", 1,
                FinancialScoreProfileSettings.Unconfigured, At)));
            var defaults = await Task.WhenAll(service.GetCurrentOrCreateDefaultAsync(other, "USD", default), second.GetCurrentOrCreateDefaultAsync(other, "USD", default));
            Check(defaults.All(c => c.Score == 50 && c.CalculationId == defaults[0].CalculationId && c.CalculatedAtUtc == defaults[0].CalculatedAtUtc));
            Check((await publisher.ReadPendingAsync(other)).Count == 0 && (await store.GetHistoryAsync(other, "USD", null, null, null, null, 101, default)).Count == 1);
            var concurrent = await Task.WhenAll(service.ApplyAsync(expense, null, default), second.ApplyAsync(expense, null, default));
            Check(concurrent[0]!.CalculationId == concurrent[1]!.CalculationId && concurrent[0]!.Score == concurrent[1]!.Score);
            var first = (await publisher.ReadPendingAsync(owner)).Single();
            Check(first.PublishedAtUtc is null && first.Event.CausationId == expense.EventId);
            await service.ApplyAsync(income, null, default);
            Check((await store.GetSnapshotAsync(owner, "USD", default)).Records.Single(p => p.RecordType == "expense").Amount == 12.3401m);
            await service.ApplyAsync(moved, null, default);
            await service.ApplyAsync(archived, null, default);
            await service.ApplyAsync(expense, null, default);
            await service.ApplyAsync(moved, null, default);
            Check((await publisher.ReadPendingAsync(owner)).Count == 5);
            var forged = new IntegrationEventEnvelope<ScoreCalculatedV1>(first.Event.EventId, first.Event.OccurrenceId,
                first.Event.EventType, first.Event.OccurredAtUtc, first.Event.Producer, first.Event.SchemaVersion,
                first.Event.CorrelationId, first.Event.CausationId, owner, first.Event.Payload with { Score = 99 });
            await Conflict(() => publisher.MarkPublishedAsync(new ScoreOutboxMessage(forged, null), At.AddHours(1)));
            Check(await publisher.MarkPublishedAsync(first, At.AddHours(1)));
            Check(!await publisher.MarkPublishedAsync(first, At.AddHours(2)));
            await Conflict(() => service.ApplyAsync(Copy(expense, expense.EventId, expense.Payload with { Amount = 999m }), null, default));
            var changedCorrelation = new IntegrationEventEnvelope<FinancialRecordChangedV1>(expense.EventId, expense.OccurrenceId,
                expense.EventType, expense.OccurredAtUtc, expense.Producer, expense.SchemaVersion, "synthetic-changed", expense.CausationId, owner, expense.Payload);
            await Conflict(() => service.ApplyAsync(changedCorrelation, null, default));
            await Conflict(() => service.ApplyAsync(Copy(expense, "synthetic-foreign-record", expense.Payload, owner: other), null, default));
            await Conflict(() => service.ApplyAsync(Copy(expense, expense.EventId, expense.Payload with { RecordId = "synthetic-other-record" }, owner: other), null, default));
            await Conflict(() => service.ApplyAsync(Copy(archived, "synthetic-equal-revision", archived.Payload with { Amount = 1m }), null, default));
            Check(await service.ApplyAsync(Copy(expense, "synthetic-stale", expense.Payload), null, default) is null);
            var failed = Source(owner, "synthetic-score-failed", 1m);
            var broken = new FinancialScoreService(store, new MissingPublisher(), new FinancialScoreCalculator(), profile, context);
            await Unavailable(() => broken.ApplyAsync(failed, null, default));
            Check(!(await store.GetSnapshotAsync(owner, "USD", default)).Records.Any(p => p.RecordId == failed.Payload.RecordId));
            Check(await database.ExecuteAsync([], async (records, _) => await records.GetAsync("score_claims", "financial-score", "event:" + failed.EventId) is null));
            await Sql(admin, """
                CREATE FUNCTION fa_financial_score.synthetic_abort() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN IF NEW.bucket='score_state' THEN RAISE EXCEPTION 'synthetic rollback'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER synthetic_abort BEFORE INSERT OR UPDATE ON fa_financial_score.records
                  FOR EACH ROW EXECUTE FUNCTION fa_financial_score.synthetic_abort();
                """);
            try { await Unavailable(() => service.ApplyAsync(failed, null, default)); }
            finally { await Sql(admin, "DROP TRIGGER synthetic_abort ON fa_financial_score.records; DROP FUNCTION fa_financial_score.synthetic_abort()"); }
            Check(!(await store.GetSnapshotAsync(owner, "USD", default)).Records.Any(p => p.RecordId == failed.Payload.RecordId));
            Check(await database.ExecuteAsync([], async (records, _) => await records.GetAsync("score_claims", "financial-score", "event:" + failed.EventId) is null));
            await Conflict(() => profile.ApplySnapshotAsync(new ScoreProfileSnapshot(owner, "USD", "synthetic-v1", 1,
                FinancialScoreProfileSettings.Unconfigured, At.AddMinutes(1))));
            Check(!await profile.ApplySnapshotAsync(new ScoreProfileSnapshot(owner, "USD", "synthetic-old", 0,
                FinancialScoreProfileSettings.Unconfigured, At)));
        }
        // Restart's clock makes cached inputs expire; committed replay must still preserve its first facts and acknowledgements.
        if (mode == "restart") { await Unavailable(() => profile.GetAsync(owner, "USD", default)); }
        var replay = await service.ApplyAsync(expense, null, default);
        await service.ApplyAsync(moved, null, default);
        await service.ApplyAsync(archived, null, default);
        Check(replay is not null && (await publisher.ReadPendingAsync(owner)).Count == 4);
        Check((await publisher.GetAsync(owner, replay!.CalculationId))!.PublishedAtUtc == At.AddHours(1));
        if (mode == "restart")
        {
            var expired = Source(owner, "synthetic-expired-input", 3m);
            await Unavailable(() => service.ApplyAsync(expired, null, default));
            Check(!(await store.GetSnapshotAsync(owner, "USD", default)).Records.Any(p => p.RecordId == expired.Payload.RecordId));
            Check(await database.ExecuteAsync([], async (records, _) => await records.GetAsync("score_claims", "financial-score", "event:" + expired.EventId) is null));
        }
        Check((await store.GetSnapshotAsync(owner, "USD", default)).Records.Single().Amount == 50.0002m);
        Check((await store.GetSnapshotAsync(owner, "EUR", default)).Records.Single().Status == "archived");
        Check((await store.GetCurrentAsync(owner, "EUR", default))!.Score == 50);
        Check((await store.GetCurrentAsync(other, "USD", default))!.Score == 50);
        Check((await store.GetSnapshotAsync(other, "EUR", default)).Records.Count == 0);
        var ownerPage = await context.ReadOwnerPageAsync(pageSize: 1);
        Check(ownerPage.Owners.Count == 1 && ownerPage.NextCursor is not null);
        var nextOwners = await context.ReadOwnerPageAsync(ownerPage.NextCursor, pageSize: 1);
        Check(nextOwners.Owners.Count == 1 && nextOwners.Owners[0] != ownerPage.Owners[0]);
        await Unavailable(() => store.GetBySourceEventIdAsync(expense.EventId, default));
        var history = await store.GetHistoryAsync(owner, "USD", At, At.AddHours(1), null, null, 1, default);
        Check(history.Count == 1);
        var next = await store.GetHistoryAsync(owner, "USD", At, At.AddHours(1), history[0].CalculatedAtUtc, history[0].CalculationId, 101, default);
        Check(next.All(c => c.CalculationId != history[0].CalculationId));
        if (mode == "initial") { await VerifyPublicationOrderAsync(database); }
        await VerifyGlobalOutboxAsync(database, publisher, profile, service, mode);
    }

    private static async Task VerifyPublicationOrderAsync(OwnedPostgresStore database)
    {
        var owner = new string('c', 64); var locks = new[] { "owner:" + owner };
        await database.ExecuteAsync(locks, async (records, _) =>
        {
            Check(await OwnedPublicationOrder.AllocateAsync(records, "fixture", owner, "synthetic-first") == 1);
            Check(await OwnedPublicationOrder.AllocateAsync(records, "fixture", owner, "synthetic-second") == 2);
            Check(await OwnedPublicationOrder.AllocateAsync(records, "fixture", owner, "synthetic-first") == 1);
            return true;
        });
        await Unavailable(() => database.ExecuteAsync<bool>(locks, async (records, _) =>
        {
            Check(await OwnedPublicationOrder.AllocateAsync(records, "fixture", owner, "synthetic-rolled-back") == 3);
            throw new StorageUnavailableException();
        }));
        await database.ExecuteAsync(locks, async (records, _) =>
        {
            await Unavailable(() => OwnedPublicationOrder.ReadRequiredAsync(records, "fixture", owner, "synthetic-rolled-back"));
            Check(await OwnedPublicationOrder.AllocateAsync(records, "fixture", owner, "synthetic-third") == 3);
            Check(await OwnedPublicationOrder.ReadRequiredAsync(records, "fixture", owner, "synthetic-first") == 1);
            var head = await records.GetAsync("publication_order_head", owner, "fixture") ?? throw new FixtureFailure();
            Check(await records.DeleteAsync("publication_order_head", owner, "fixture", head.Revision));
            return true;
        });
        await Conflict(() => database.ExecuteAsync(locks, (records, _) =>
            OwnedPublicationOrder.AllocateAsync(records, "fixture", owner, "synthetic-reused-ordinal")));
    }

    private static async Task VerifyGlobalOutboxAsync(OwnedPostgresStore database, PostgresFinancialScoreEventPublisher publisher,
        PostgresFinancialScoreProfileSettingsProvider profile, FinancialScoreService service, string mode)
    {
        var owners = Enumerable.Range(0, 31).Select(index => Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes("synthetic-native-scan-" + index))).ToLowerInvariant()).ToArray();
        if (mode == "initial")
        {
            for (var index = 0; index < owners.Length; index++)
            {
                await profile.ApplySnapshotAsync(new ScoreProfileSnapshot(owners[index], "USD", "synthetic-scan-v1", 1,
                    FinancialScoreProfileSettings.Unconfigured, At));
                await service.ApplyAsync(Source(owners[index], "synthetic-native-scan-" + index, 1m), null, default);
            }
        }
        else
        {
            Check(await database.ExecuteAsync([], async (records, _) =>
                await records.GetAsync("score_outbox_scan", "financial-score", "pending") is not null));
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var pass = 0; pass < 8 && seen.Count < owners.Length; pass++)
        {
            var batch = await publisher.ReadPendingBatchAsync();
            Check(batch.Count <= 25 && batch.Select(message => message.Event.EventId).Distinct(StringComparer.Ordinal).Count() == batch.Count);
            if (mode == "initial" && pass == 0) { Check(batch.Count == 25); }
            foreach (var message in batch.Where(message => owners.Contains(message.Event.UserIdHash, StringComparer.Ordinal)))
            {
                Check(message.SourceSequence == 1);
                seen.Add(message.Event.UserIdHash!);
                if (mode == "restart")
                {
                    await Conflict(() => publisher.MarkPublishedAsync(message with { SourceSequence = 99 }, At.AddHours(4)));
                    Check(await publisher.MarkPublishedAsync(message, At.AddHours(4)));
                }
            }
        }
        Check(seen.Count == owners.Length);
        foreach (var owner in owners)
        { Check((await publisher.ReadPendingAsync(owner)).Count == (mode == "initial" ? 1 : 0)); }
        if (mode == "restart")
        {
            var remaining = await publisher.ReadPendingBatchAsync();
            Check(remaining.Count == 4 && remaining.All(message => message.Event.UserIdHash == new string('a', 64)));
        }
    }

    private static IntegrationEventEnvelope<FinancialRecordChangedV1> Source(string owner, string id, decimal amount,
        string type = FinancialRecordEventTypes.ExpenseCreated) => new(id, id + "-occurrence", type, At,
            type == FinancialRecordEventTypes.IncomeCreated ? "income-service" : "expense-service", 1,
            "synthetic-correlation", id + "-cause", owner,
            new FinancialRecordChangedV1(id, amount, "USD", type == FinancialRecordEventTypes.IncomeCreated ? "income.salary" : "expense.food",
                new DateOnly(2026, 10, 21), "active", 0, "manual", At));
    private static IntegrationEventEnvelope<FinancialRecordChangedV1> Copy(IntegrationEventEnvelope<FinancialRecordChangedV1> source,
        string id, FinancialRecordChangedV1 payload, string type = FinancialRecordEventTypes.ExpenseUpdated, string? owner = null) =>
        new(id, id + "-occurrence", id == source.EventId ? source.EventType : type, payload.ChangedAtUtc,
            source.Producer, 1, source.CorrelationId, source.CausationId, owner ?? source.UserIdHash, payload);
    private static async Task Conflict(Func<Task> action) { try { await action(); throw new FixtureFailure(); } catch (StorageConflictException) { } }
    private static async Task Unavailable(Func<Task> action) { try { await action(); throw new FixtureFailure(); } catch (StorageUnavailableException) { } }
    private static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
    private static async Task Sql(NpgsqlConnection connection, string sql) { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
    private sealed class FixtureClock : TimeProvider { public DateTimeOffset Now { get; set; } public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class MissingPublisher : IFinancialScoreEventPublisher
    { public Task PublishAsync(IntegrationEventEnvelope<ScoreCalculatedV1> envelope, CancellationToken cancellationToken) => Task.CompletedTask; }
}
