using System.Text;
using FinancialAssistant.FinancialSummary.Application;
using FinancialAssistant.FinancialSummary.Domain;
using FinancialAssistant.FinancialSummary.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using Npgsql;

var stage = "isolated fixture validation";
try
{
    var password = Setting("FA_PERSISTENCE_TEST_PASSWORD");
    var directory = Setting("FA_PERSISTENCE_TEST_DIRECTORY");
    var mode = Setting("FA_PERSISTENCE_TEST_MODE");
    Check(password.Length == 64 && password.All(char.IsAsciiHexDigit) && Path.IsPathFullyQualified(directory) &&
        Directory.Exists(directory) && mode is "initial" or "restart");
    Check(int.TryParse(Setting("FA_PERSISTENCE_TEST_PORT"), out var port) && port is >= 49152 and <= 65535);
    var repository = new DirectoryInfo(AppContext.BaseDirectory);
    while (!File.Exists(Path.Combine(repository.FullName, "FinancialAssistant.Backend.sln")))
    { repository = repository.Parent ?? throw new FixtureFailure(); }
    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    var data = new DirectoryInfo(Path.GetFullPath(directory));
    Check(data.Name == "data" && data.Parent is not null && Guid.TryParseExact(data.Parent.Name, "N", out _) &&
        string.Equals(data.Parent.Parent?.FullName, Path.Combine(repository.FullName, ".codex-runtime", "persistence-test"), comparison));
    for (DirectoryInfo? ancestor = data; ancestor is not null && ancestor.FullName.Length >= repository.FullName.Length; ancestor = ancestor.Parent)
    { Check((ancestor.Attributes & FileAttributes.ReparsePoint) == 0); }
    var connection = new NpgsqlConnectionStringBuilder
    {
        Host = "127.0.0.1",
        Port = port,
        Database = "postgres",
        Username = "fa_test_admin",
        Password = password,
        IncludeErrorDetail = false,
        LogParameters = false,
        Timeout = 3,
        CommandTimeout = 5
    };
    await using var bootstrap = new NpgsqlConnection(connection.ConnectionString);
    await bootstrap.OpenAsync();
    await using (var location = new NpgsqlCommand("SHOW data_directory", bootstrap))
    { Check(string.Equals(Path.GetFullPath((string)(await location.ExecuteScalarAsync())!), data.FullName, comparison)); }
    const string database = "fa_test_fin287";
    await using (var fresh = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin287') OR (datname='fa_test_fin287' AND $1='initial')", bootstrap))
    { fresh.Parameters.AddWithValue(mode); Check((long)(await fresh.ExecuteScalarAsync())! == 0); }
    var owners = new[] { new StorageIdentity("financial-summary"), new StorageIdentity("analytics") };
    if (mode == "initial")
    {
        stage = "independent module role provisioning";
        await Sql(bootstrap, $"CREATE DATABASE {database}");
        await Sql(bootstrap, $"REVOKE ALL ON DATABASE {database} FROM PUBLIC");
        foreach (var owner in owners)
        { await Sql(bootstrap, $"CREATE ROLE {owner.MigrationRole} LOGIN PASSWORD '{password}'; CREATE ROLE {owner.RuntimeRole} LOGIN PASSWORD '{password}'; GRANT CONNECT ON DATABASE {database} TO {owner.MigrationRole},{owner.RuntimeRole}"); }
    }
    connection.Database = database;
    await using var admin = new NpgsqlConnection(connection.ConnectionString);
    await admin.OpenAsync();
    var summarySettings = Settings(database, port, password, 11);
    var analyticsSettings = Settings(database, port, password, 13);
    if (mode == "initial")
    {
        stage = "separate repeated migrations";
        await Sql(admin, "REVOKE ALL ON SCHEMA public FROM PUBLIC");
        foreach (var owner in owners)
        {
            await Sql(admin, $"CREATE SCHEMA {owner.Schema} AUTHORIZATION {owner.MigrationRole}; REVOKE ALL ON SCHEMA {owner.Schema} FROM PUBLIC; GRANT USAGE ON SCHEMA {owner.Schema} TO {owner.RuntimeRole}");
            var settings = owner.ServiceId == "financial-summary" ? summarySettings : analyticsSettings;
            using var protection = settings.CreateProtection();
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(owner.ServiceId), protection);
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(owner.ServiceId), protection);
            await Sql(admin, $"GRANT SELECT,INSERT,UPDATE,DELETE ON {owner.Schema}.records TO {owner.RuntimeRole}; GRANT SELECT ON {owner.Schema}.schema_versions,{owner.Schema}.encryption_keys TO {owner.RuntimeRole}");
        }
    }
    stage = "Summary exact totals, owner isolation, currency moves, replay and rollback";
    await VerifySummary(summarySettings, admin, mode);
    stage = "Summary staged scoped rebuild, live-watermark protection and interrupted recovery";
    await VerifySummaryRebuild(summarySettings, admin, mode);
    stage = "Analytics atomic projections, immutable publication and acknowledged completion";
    await AnalyticsFixture.VerifyAsync(analyticsSettings, mode);
    stage = "Analytics scoped generation recovery and separate refresh handoff";
    await AnalyticsRebuildFixture.VerifyAsync(analyticsSettings, mode);
    stage = "bounded maximum projection generation capacity";
    await ProjectionCapacityFixture.VerifyAsync(summarySettings, analyticsSettings, mode);
    stage = "separate Summary and Analytics runtime role denial";
    foreach (var pair in new[] { (owners[0], owners[1]), (owners[1], owners[0]) })
    {
        var isolated = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Username = pair.Item1.RuntimeRole };
        await using var runtime = new NpgsqlConnection(isolated.ConnectionString);
        await runtime.OpenAsync();
        try { await Sql(runtime, $"SELECT * FROM {pair.Item2.Schema}.records"); throw new FixtureFailure(); }
        catch (PostgresException exception) when (exception.SqlState == "42501") { }
    }
    stage = "encrypted independent projection records";
    foreach (var owner in owners)
    {
        await using var raw = new NpgsqlCommand($"SELECT payload FROM {owner.Schema}.records", admin);
        await using var reader = await raw.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { Check(!Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0)).Contains("synthetic", StringComparison.Ordinal)); }
    }
    Console.WriteLine("PASS separate Summary/Analytics PostgreSQL mode=" + mode + ": exact totals, atomic owned state/publication, bounded scoped rebuild recovery and role isolation. Broker, cross-owner refresh execution and installed acceptance remain unqualified.");
    return 0;
}
catch
{
    Console.Error.WriteLine("FAIL Projections PostgreSQL at " + stage + ". Sensitive details suppressed.");
    return 1;
}

static async Task VerifySummary(NativePostgresSettings settings, NpgsqlConnection admin, string mode)
{
    using var protection = settings.CreateProtection();
    await using var database = new OwnedPostgresStore(settings.CreateOptions("financial-summary"), protection);
    await using var secondDatabase = new OwnedPostgresStore(settings.CreateOptions("financial-summary"), protection);
    Check(await database.IsReadyAsync());
    var store = new PostgresFinancialSummaryReadModelStore(database);
    var second = new PostgresFinancialSummaryReadModelStore(secondDatabase);
    var owner = new string('a', 64);
    var other = new string('b', 64);
    var income = Projection(owner, "income", "synthetic-income", 100.1234m);
    var expense = Projection(owner, "expense", "synthetic-expense", 40.0001m);
    var moved = expense with
    {
        Currency = "EUR",
        Revision = 1,
        EventId = "synthetic-moved",
        ChangedAtUtc = expense.ChangedAtUtc.AddMinutes(1)
    };
    var archived = moved with
    {
        Status = "archived",
        Revision = 2,
        EventId = "synthetic-archived",
        ChangedAtUtc = expense.ChangedAtUtc.AddMinutes(2)
    };
    var otherIncome = Projection(other, "income", "synthetic-other", 51.0042m) with { Currency = "EUR" };
    if (mode == "initial")
    {
        await Task.WhenAll(store.UpsertIfNewerAsync(income, default), second.UpsertIfNewerAsync(income, default));
        await store.UpsertIfNewerAsync(expense, default);
        var projector = new FinancialSummaryProjector(store);
        var totals = await projector.GetAsync(owner, "USD", income.Date, income.ChangedAtUtc.AddMinutes(1), TimeSpan.FromHours(1), default);
        Check(totals.Daily.Income == 100.1234m && totals.Daily.Expense == 40.0001m && totals.Weekly.BalanceDelta == 60.1233m &&
            totals.Monthly.BalanceDelta == 60.1233m && totals.CategoryBreakdown.Count == 2 && !totals.IsStale);
        await store.UpsertIfNewerAsync(moved, default);
        Check((await store.ListAsync(owner, "USD", default)).Single() == income);
        await store.UpsertIfNewerAsync(archived, default);
        await store.UpsertIfNewerAsync(otherIncome, default);
        var stale = expense with { Amount = 99m, EventId = "synthetic-stale" };
        await store.UpsertIfNewerAsync(stale, default);
        await Conflict(() => store.UpsertIfNewerAsync(stale with { Amount = 98m }, default));
        await Conflict(() => store.UpsertIfNewerAsync(archived with { Amount = 9m }, default));
        await Conflict(() => store.UpsertIfNewerAsync(archived with { EventId = "synthetic-conflicting-revision" }, default));
        await Conflict(() => store.UpsertIfNewerAsync(income with { UserIdHash = other }, default));
        await Conflict(() => store.UpsertIfNewerAsync(income with { RecordId = "synthetic-other-id" }, default));
        var race = Projection(new string('c', 64), "expense", "synthetic-race", 1m);
        var raceResults = await Task.WhenAll(TryWrite(() => store.UpsertIfNewerAsync(race, default)),
            TryWrite(() => second.UpsertIfNewerAsync(race with { Amount = 2m }, default)));
        Check(raceResults.Count(result => result) == 1);

        // Fail after the replay receipt and projection write, proving the owner transaction rolls both back.
        await Sql(admin, """
            CREATE FUNCTION fa_financial_summary.synthetic_abort_counts() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.bucket='projection_counts' THEN RAISE EXCEPTION 'synthetic rollback'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER synthetic_abort BEFORE INSERT OR UPDATE ON fa_financial_summary.records
              FOR EACH ROW EXECUTE FUNCTION fa_financial_summary.synthetic_abort_counts();
            """);
        var failed = Projection(owner, "expense", "synthetic-rollback", 3m);
        try
        {
            try { await store.UpsertIfNewerAsync(failed, default); throw new FixtureFailure(); }
            catch (StorageUnavailableException) { }
        }
        finally { await Sql(admin, "DROP TRIGGER synthetic_abort ON fa_financial_summary.records; DROP FUNCTION fa_financial_summary.synthetic_abort_counts()"); }
        Check(!(await store.ListAsync(owner, "USD", default)).Any(value => value.RecordId == failed.RecordId));
        await database.ExecuteAsync(new[] { "owner:" + owner }, async (records, _) =>
        {
            Check(await records.GetAsync("event_receipts", owner, failed.EventId) is null);
            Check(await records.GetAsync("record_owners", "summary", "expense:synthetic-rollback") is null);
            return true;
        });
        var pagedOwner = new string('d', 64);
        for (var i = 0; i < 101; i++)
        { await store.UpsertIfNewerAsync(Projection(pagedOwner, "expense", "synthetic-page-" + i, 0.0001m), default); }
    }
    await store.UpsertIfNewerAsync(income, default);
    await store.UpsertIfNewerAsync(expense, default);
    await store.UpsertIfNewerAsync(moved, default);
    Check((await store.ListAsync(owner, "USD", default)).Single() == income);
    Check((await store.ListAsync(owner, "EUR", default)).Single() == archived);
    Check((await store.ListAsync(other, "EUR", default)).Single() == otherIncome);
    Check((await store.ListAsync(new string('e', 64), "USD", default)).Count == 0);
    var summary = await new FinancialSummaryProjector(store).GetAsync(owner, "EUR", income.Date,
        archived.ChangedAtUtc.AddMinutes(1), TimeSpan.FromHours(1), default);
    Check(summary.Monthly.Expense == 0 && summary.LastEventAtUtc == archived.ChangedAtUtc && !summary.IsStale);
    Check((await store.ListAsync(new string('d', 64), "USD", default)).Count == 101);
    try { await store.ResetAsync(default); throw new FixtureFailure(); }
    catch (NotSupportedException) { }
}

static async Task VerifySummaryRebuild(NativePostgresSettings settings, NpgsqlConnection admin, string mode)
{
    using var protection = settings.CreateProtection();
    await using var database = new OwnedPostgresStore(settings.CreateOptions("financial-summary"), protection);
    var store = new PostgresFinancialSummaryReadModelStore(database);
    var owner = new string('f', 64);
    var other = new string('1', 64);
    var day = new DateOnly(2026, 10, 20);
    var first = Projection(owner, "expense", "synthetic-rebuild-000", 10m);
    var before = Projection(owner, "income", "synthetic-before-period", 7.011m) with { Date = day.AddDays(-1) };
    var after = Projection(owner, "expense", "synthetic-after-period", 8.011m) with { Date = day.AddDays(1), Currency = "EUR" };
    var live = Projection(owner, "expense", "synthetic-live-during-rebuild", 3.22m) with { Date = day.AddDays(2) };
    var obsolete = Projection(owner, "expense", "synthetic-obsolete", 10m) with { Revision = 2, EventId = "synthetic-obsolete-latest" };
    var sibling = Projection(other, "income", "synthetic-rebuild-sibling", 33m);
    var source = Enumerable.Range(0, 101).Select(i => Projection(owner, "expense", "synthetic-rebuild-" + i.ToString("D3"), 12.3401m)
        with
    { Currency = i % 2 == 0 ? "USD" : "EUR" }).ToArray();
    source[0] = first with { Revision = 1, Amount = 60.125m, ChangedAtUtc = first.ChangedAtUtc.AddMinutes(1), EventId = "synthetic-rebuild-new-first" };
    var request = new SummaryRebuildRequest(owner, day, day, "synthetic-source-v2", first.ChangedAtUtc, source.Length,
        PostgresFinancialSummaryReadModelStore.CalculateSourceDigest(source));
    if (mode == "initial")
    {
        foreach (var value in new[] { first, before, after, obsolete, sibling }) { await store.UpsertIfNewerAsync(value, default); }
        var firstRequest = request with { SourceSnapshotVersion = "synthetic-source-v1" };
        var firstJob = await store.BeginRebuildAsync(firstRequest);
        Check((await store.BeginRebuildAsync(firstRequest)).JobKey == firstJob.JobKey);
        await Conflict(() => store.BeginRebuildAsync(firstRequest with { ExpectedDigest = new string('A', 64) }));
        var firstPage = source.Take(100).ToArray();
        var outOfPeriod = firstPage.ToArray(); outOfPeriod[^1] = outOfPeriod[^1] with { Date = day.AddDays(1) };
        await Conflict(() => store.StageRebuildPageAsync(owner, firstJob.JobKey, 0, outOfPeriod));
        await Conflict(() => store.StageRebuildPageAsync(other, firstJob.JobKey, 0, firstPage));
        Check((await store.StageRebuildPageAsync(owner, firstJob.JobKey, 0, firstPage)).StagedRecords == 100);
        Check((await store.StageRebuildPageAsync(owner, firstJob.JobKey, 0, firstPage)).StagedRecords == 100);
        var changedPage = firstPage.ToArray(); changedPage[0] = changedPage[0] with { Amount = 1m };
        await Conflict(() => store.StageRebuildPageAsync(owner, firstJob.JobKey, 0, changedPage));
        await Conflict(() => store.CommitRebuildAsync(owner, firstJob.JobKey));
        Check((await store.ListAsync(owner, "USD", default)).Count == 3);
        await store.UpsertIfNewerAsync(live, default);
        await store.StageRebuildPageAsync(owner, firstJob.JobKey, 1, source.Skip(100).ToArray());
        await Conflict(() => store.CommitRebuildAsync(owner, firstJob.JobKey));
        Check((await store.ListAsync(owner, "USD", default)).Contains(live));
        await store.CancelRebuildAsync(owner, firstJob.JobKey);
        await Conflict(() => store.BeginRebuildAsync(request));
        await CleanupSummary(store, owner, firstJob.JobKey);
        var interrupted = await store.BeginRebuildAsync(request);
        await store.StageRebuildPageAsync(owner, interrupted.JobKey, 0, firstPage);
        Check((await store.GetRebuildAsync(owner)) is { Status: "running", StagedRecords: 100 });
        Check((await store.ListAsync(owner, "USD", default)).Contains(first));
        Check((await store.ListAsync(other, "USD", default)).Single() == sibling);
        return;
    }
    var recovered = await store.GetRebuildAsync(owner) ?? throw new FixtureFailure();
    Check(recovered.Status == "running" && recovered.StagedRecords == 100);
    Check((await store.ListAsync(owner, "USD", default)).Contains(first));
    var resumed = await store.BeginRebuildAsync(request);
    Check(resumed.JobKey == recovered.JobKey);
    await store.StageRebuildPageAsync(owner, resumed.JobKey, 0, source.Take(100).ToArray());
    await store.StageRebuildPageAsync(owner, resumed.JobKey, 1, source.Skip(100).ToArray());
    await Sql(admin, """
        CREATE FUNCTION fa_financial_summary.synthetic_abort_swap() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN IF NEW.bucket='projection_counts' THEN RAISE EXCEPTION 'synthetic swap rollback'; END IF; RETURN NEW; END $$;
        CREATE TRIGGER synthetic_abort_swap BEFORE UPDATE ON fa_financial_summary.records
          FOR EACH ROW EXECUTE FUNCTION fa_financial_summary.synthetic_abort_swap();
        """);
    try
    {
        try { await store.CommitRebuildAsync(owner, resumed.JobKey); throw new FixtureFailure(); }
        catch (StorageUnavailableException) { }
    }
    finally { await Sql(admin, "DROP TRIGGER synthetic_abort_swap ON fa_financial_summary.records; DROP FUNCTION fa_financial_summary.synthetic_abort_swap()"); }
    Check((await store.ListAsync(owner, "USD", default)).Contains(first));
    Check((await store.GetRebuildAsync(owner)) is { Status: "running", StagedRecords: 101 });
    await database.ExecuteAsync(new[] { "owner:" + owner }, async (records, _) =>
    {
        Check(await records.GetAsync("event_receipts", owner, source[0].EventId) is null);
        return true;
    });
    var concurrentCommit = store.CommitRebuildAsync(owner, resumed.JobKey);
    var concurrentRead = store.ListAsync(owner, "USD", default);
    await Task.WhenAll(concurrentCommit, concurrentRead);
    Check((await concurrentCommit).Status == "succeeded" && (await concurrentRead).Count is 4 or 53);
    Check((await store.CommitRebuildAsync(owner, resumed.JobKey)).Status == "succeeded");
    var usd = await store.ListAsync(owner, "USD", default);
    var eur = await store.ListAsync(owner, "EUR", default);
    Check(usd.Count == 53 && eur.Count == 51 && usd.Contains(before) && usd.Contains(live) && eur.Contains(after));
    Check((await store.ListAsync(other, "USD", default)).Single() == sibling);
    await store.UpsertIfNewerAsync(obsolete with { Revision = 1, EventId = "synthetic-stale-after-swap" }, default);
    await store.UpsertIfNewerAsync(obsolete, default);
    Check(!(await store.ListAsync(owner, "USD", default)).Any(value => value.RecordId == obsolete.RecordId));
    var summary = await new FinancialSummaryProjector(store).GetAsync(owner, "USD", day,
        first.ChangedAtUtc.AddMinutes(2), TimeSpan.FromHours(1), default);
    Check(summary.Daily.Expense == 677.13m && summary.Monthly.Expense == 680.35m && summary.Monthly.Income == 7.011m);
    await CleanupSummary(store, owner, resumed.JobKey);
    var empty = request with
    {
        SourceSnapshotVersion = "synthetic-empty-v3",
        ExpectedRecords = 0,
        ExpectedDigest = PostgresFinancialSummaryReadModelStore.CalculateSourceDigest(Array.Empty<FinancialRecordProjection>())
    };
    var emptyJob = await store.BeginRebuildAsync(empty);
    await store.CommitRebuildAsync(owner, emptyJob.JobKey);
    Check((await store.ListAsync(owner, "USD", default)).Count == 2 && (await store.ListAsync(owner, "EUR", default)).Single() == after);
    await store.UpsertIfNewerAsync(source[0], default);
    Check((await store.ListAsync(owner, "USD", default)).Count == 2);
    await CleanupSummary(store, owner, emptyJob.JobKey);
    await store.UpsertIfNewerAsync(source[0] with
    {
        Revision = 2,
        EventId = "synthetic-new-after-empty",
        ChangedAtUtc = source[0].ChangedAtUtc.AddMinutes(1)
    }, default);
    Check((await store.ListAsync(owner, "USD", default)).Count == 3);
    Check((await store.ListAsync(other, "USD", default)).Single() == sibling);
    var saved = await database.ExecuteAsync(new[] { "owner:" + owner }, async (records, _) =>
    {
        var active = await records.GetAsync("projection_counts", owner, "active") ?? throw new FixtureFailure();
        await records.DeleteAsync("projection_counts", owner, "active", active.Revision);
        return active;
    });
    try { await store.ListAsync(owner, "USD", default); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    await database.ExecuteAsync(new[] { "owner:" + owner }, async (records, _) =>
        await records.InsertAsync("projection_counts", owner, "active", saved.Payload));
    Check((await store.ListAsync(owner, "USD", default)).Count == 3);
}

static async Task CleanupSummary(PostgresFinancialSummaryReadModelStore store, string owner, string jobKey)
{
    for (var i = 0; i < 42; i++)
    { if (!(await store.CleanupRebuildAsync(owner, jobKey)).CleanupPending) { return; } }
    throw new FixtureFailure();
}

static FinancialRecordProjection Projection(string owner, string type, string id, decimal amount) => new(type, id, owner,
    amount, "USD", type + ".synthetic", new DateOnly(2026, 10, 20), "active", 0, "manual",
    new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero), id + "-event");
static async Task<bool> TryWrite(Func<Task> operation)
{ try { await operation(); return true; } catch (StorageConflictException) { return false; } }
static async Task Conflict(Func<Task> operation) { Check(!await TryWrite(operation)); }
static string Setting(string name) => Environment.GetEnvironmentVariable(name) ?? "";
static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
static async Task Sql(NpgsqlConnection connection, string sql)
{ await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
static NativePostgresSettings Settings(string database, int port, string password, byte index) => new()
{
    Database = database,
    Port = port,
    Password = password,
    IndexKey = Convert.ToBase64String(Enumerable.Repeat(index, 32).ToArray()),
    ActiveEncryptionKeyId = "v1",
    EncryptionKeys = new Dictionary<string, string>
    { ["v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)(index + 1), 32).ToArray()) }
};
sealed class FixtureFailure : Exception;
