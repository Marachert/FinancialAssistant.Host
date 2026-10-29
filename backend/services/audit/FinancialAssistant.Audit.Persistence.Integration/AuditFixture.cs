using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Audit.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;
using Npgsql;

internal static class AuditFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);

    public static async Task VerifyAsync(NativePostgresSettings settings, NpgsqlConnection admin, string mode)
    {
        using var protection = settings.CreateProtection();
        await using var database = new OwnedPostgresStore(settings.CreateOptions("audit"), protection);
        await using var secondDatabase = new OwnedPostgresStore(settings.CreateOptions("audit"), protection);
        Check(await database.IsReadyAsync());
        var clock = new FixtureClock { Now = At.AddHours(mode == "initial" ? 1 : 49) };
        var days = Days(3); var policy = Policy(days);
        var store = new PostgresAuditRecordStore(database, policy, days, clock);
        var secondStore = new PostgresAuditRecordStore(secondDatabase, policy, days, clock);
        var service = new AuditEventService(store, policy, clock, store);
        var second = new AuditEventService(secondStore, policy, clock, secondStore);
        var main = Source("synthetic-main", "synthetic-main-correlation");
        var expired = Source("synthetic-expiring", "synthetic-expiring-correlation", standard: true);
        if (mode == "initial")
        {
            var concurrent = await Task.WhenAll(service.ConsumeAsync(main, default), second.ConsumeAsync(main, default));
            Check(concurrent[0] == concurrent[1]);
            var original = (await service.FindByCorrelationAsync(main.CorrelationId, default)).Single();
            Check(original.RecordedAtUtc == At.AddHours(1) && original.ExpiresAtUtc == At.AddDays(10));
            Check(original.SubjectIdHash == new string('b', 64) && original.ActorIdHash == new string('a', 64));
            await Conflict(() => service.ConsumeAsync(Copy(main, occurrence: "synthetic-conflicting-occurrence"), default));
            await Conflict(() => service.ConsumeAsync(Copy(main, correlation: "synthetic-foreign-correlation"), default));
            await Conflict(() => service.ConsumeAsync(Copy(main, payload: main.Payload with { Outcome = "failed" }), default));
            await Conflict(() => service.ConsumeAsync(Copy(main, subject: new string('c', 64)), default));
            Check((await service.FindByCorrelationAsync("synthetic-foreign-correlation", default)).Count == 0);
            await Fails<StorageUnavailableException>(() => store.AppendAsync(original, default));

            var missing = Source("synthetic-no-append", "synthetic-rollback-correlation");
            await Fails<StorageUnavailableException>(() => store.ExecuteAsync(missing, _ => Task.FromResult("synthetic-fake-id"), default));
            Check(await MissingReceipt(database, missing.EventId));
            var rolled = Source("synthetic-rolled-back", "synthetic-rollback-correlation");
            await Fails<FixtureFailure>(() => store.ExecuteAsync(rolled, async token =>
            {
                await new AuditEventService(store, policy, clock).ConsumeAsync(rolled, token);
                throw new FixtureFailure();
            }, default));
            Check(await MissingReceipt(database, rolled.EventId));
            Check((await service.FindByCorrelationAsync(rolled.CorrelationId, default)).Count == 0);
            await service.ConsumeAsync(rolled, default);

            await Sql(admin, "CREATE FUNCTION fa_audit.synthetic_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'synthetic rejection'; END $$; CREATE TRIGGER synthetic_fail BEFORE INSERT ON fa_audit.records FOR EACH ROW WHEN(NEW.bucket='audit_entries') EXECUTE FUNCTION fa_audit.synthetic_fail()");
            var sqlFailure = Source("synthetic-sql-rollback", "synthetic-sql-correlation");
            try { await Fails<StorageUnavailableException>(() => service.ConsumeAsync(sqlFailure, default)); }
            finally { await Sql(admin, "DROP TRIGGER synthetic_fail ON fa_audit.records; DROP FUNCTION fa_audit.synthetic_fail()"); }
            Check(await MissingReceipt(database, sqlFailure.EventId));
            Check((await service.FindByCorrelationAsync(sqlFailure.CorrelationId, default)).Count == 0);
            await service.ConsumeAsync(sqlFailure, default);

            await service.ConsumeAsync(expired, default);
            for (var i = 0; i < 30; i++)
            { await service.ConsumeAsync(Source("synthetic-expiring-" + i, "synthetic-expiring-correlation", standard: true), default); }
            Check((await service.FindByCorrelationAsync(expired.CorrelationId, default)).Count == 31);
            clock.Now = At.AddHours(36);
            var lowerDays = Days(1); var lowerPolicy = Policy(lowerDays);
            var lowerStore = new PostgresAuditRecordStore(secondDatabase, lowerPolicy, lowerDays, clock);
            var lower = new AuditEventService(lowerStore, lowerPolicy, clock, lowerStore);
            Check((await lower.FindByCorrelationAsync(expired.CorrelationId, default)).Count == 0);
            Check((await service.FindByCorrelationAsync(expired.CorrelationId, default)).Count == 0);
            Check(await service.ConsumeAsync(expired, default) == AuditId(expired.EventId));
            var newShort = Source("synthetic-clamped-new", "synthetic-clamped-correlation", standard: true, occurred: clock.Now);
            await service.ConsumeAsync(newShort, default);
            Check((await service.FindByCorrelationAsync(newShort.CorrelationId, default)).Single().ExpiresAtUtc == clock.Now.AddDays(1));

            for (var i = 0; i < 1000; i++)
            { await service.ConsumeAsync(Source("synthetic-capacity-" + i, "synthetic-capacity-correlation"), default); }
            Check((await service.FindByCorrelationAsync("synthetic-capacity-correlation", default)).Count == 1000);
            var capacity = Source("synthetic-capacity-1000", "synthetic-capacity-correlation");
            await Fails<StorageUnavailableException>(() => service.ConsumeAsync(capacity, default));
            Check(await MissingReceipt(database, capacity.EventId));

            var removed = 0;
            for (var i = 0; i < 100; i++)
            {
                var cleanup = await store.CleanupExpiredAsync(); Check(cleanup.Scanned <= 25 && cleanup.Deleted <= cleanup.Scanned);
                removed += cleanup.Deleted; if (cleanup.NextCursor is null) { break; }
            }
            Check(removed == 31);
            Check(!await MissingReceipt(database, expired.EventId));
            Check((await service.FindByCorrelationAsync(expired.CorrelationId, default)).Count == 0);
        }
        Check(await service.ConsumeAsync(main, default) == AuditId(main.EventId));
        var recovered = (await service.FindByCorrelationAsync(main.CorrelationId, default)).Single();
        Check(recovered.RecordedAtUtc == At.AddHours(1) && recovered.OccurredAtUtc == At && recovered.ExpiresAtUtc == At.AddDays(10));
        Check(await service.ConsumeAsync(expired, default) == AuditId(expired.EventId));
        Check((await service.FindByCorrelationAsync(expired.CorrelationId, default)).Count == 0);
        Check((await service.FindByCorrelationAsync("synthetic-capacity-correlation", default)).Count == 1000);
        Check((await service.FindByCorrelationAsync("synthetic-clamped-correlation", default)).Single().ExpiresAtUtc == At.AddHours(60));
        await Conflict(() => service.ConsumeAsync(Copy(expired, occurrence: "synthetic-recreated-expired"), default));
        var count = await database.ExecuteAsync([], async (records, _) => (await records.ReadPageAsync("audit_entries", "audit", null, 25)).Count);
        Check(count == 25);
        var wrong = new NativePostgresSettings
        {
            Port = settings.Port,
            Database = settings.Database,
            Password = settings.Password,
            IndexKey = settings.IndexKey,
            ActiveEncryptionKeyId = "v1",
            EncryptionKeys = new Dictionary<string, string> { ["v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)99, 32).ToArray()) }
        };
        using var wrongProtection = wrong.CreateProtection();
        await using var wrongStore = new OwnedPostgresStore(wrong.CreateOptions("audit"), wrongProtection);
        Check(!await wrongStore.IsReadyAsync());
    }

    private static Dictionary<string, int> Days(int standard) => new() { ["standard"] = standard, ["regulatory"] = 10 };
    private static AuditPolicy Policy(IReadOnlyDictionary<string, int> days) => new(new[] { "expense-service" }, days);
    private static IntegrationEventEnvelope<AuditEventV1> Source(string id, string correlation, bool standard = false, DateTimeOffset? occurred = null) =>
        new(id, id + "-occurrence", AuditEventTypes.Recorded, occurred ?? At, "expense-service", 1, correlation, "synthetic-cause", new string('b', 64),
            new AuditEventV1("business", "expense.created", "succeeded", "expense", null,
                standard ? "standard" : "regulatory", standard ? null : "user", standard ? null : new string('a', 64)));
    private static IntegrationEventEnvelope<AuditEventV1> Copy(IntegrationEventEnvelope<AuditEventV1> e, string? occurrence = null,
        string? correlation = null, AuditEventV1? payload = null, string? subject = null) =>
        new(e.EventId, occurrence ?? e.OccurrenceId, e.EventType, e.OccurredAtUtc, e.Producer, e.SchemaVersion,
            correlation ?? e.CorrelationId, e.CausationId, subject ?? e.UserIdHash, payload ?? e.Payload);
    private static string AuditId(string source) => "audit_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    private static Task<bool> MissingReceipt(OwnedPostgresStore database, string source) =>
        database.ExecuteAsync([], async (records, _) => await records.GetAsync("audit_receipts", "audit", source) is null);
    private static async Task Conflict(Func<Task> operation) => await Fails<StorageConflictException>(operation);
    private static async Task Fails<T>(Func<Task> operation) where T : Exception
    { try { await operation(); } catch (T) { return; } throw new FixtureFailure(); }
    private static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
    private static async Task Sql(NpgsqlConnection connection, string sql)
    { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
    private sealed class FixtureClock : TimeProvider
    { public DateTimeOffset Now { get; set; } public override DateTimeOffset GetUtcNow() => Now; }
}
