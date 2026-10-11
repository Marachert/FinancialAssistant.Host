using System.Text;
using FinancialAssistant.Shared.Persistence;
using Npgsql;

var stage = "fixture validation";
try
{
    var password = Setting("FA_PERSISTENCE_TEST_PASSWORD");
    var directory = Environment.GetEnvironmentVariable("FA_PERSISTENCE_TEST_DIRECTORY") ?? "";
    var mode = Environment.GetEnvironmentVariable("FA_PERSISTENCE_TEST_MODE") ?? "initial";
    if (password.Length != 64 || !password.All(char.IsAsciiHexDigit) || !Path.IsPathFullyQualified(directory) ||
        !Directory.Exists(directory) || !int.TryParse(Environment.GetEnvironmentVariable("FA_PERSISTENCE_TEST_PORT"), out var port) ||
        port is < 49152 or > 65535 || mode is not ("initial" or "restart"))
    {
        throw new InvalidOperationException();
    }
    var repository = new DirectoryInfo(AppContext.BaseDirectory);
    while (!File.Exists(Path.Combine(repository.FullName, "FinancialAssistant.Backend.sln")))
    {
        repository = repository.Parent ?? throw new InvalidOperationException();
    }
    var fixtureRoot = Path.GetFullPath(Path.Combine(repository.FullName, ".codex-runtime", "persistence-test"));
    var fixtureData = new DirectoryInfo(Path.GetFullPath(directory));
    if (fixtureData.Name != "data" || fixtureData.Parent is null ||
        !Guid.TryParseExact(fixtureData.Parent.Name, "N", out _) ||
        !string.Equals(fixtureData.Parent.Parent?.FullName, fixtureRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
    {
        throw new InvalidOperationException();
    }
    for (DirectoryInfo? ancestor = fixtureData; ancestor is not null && ancestor.FullName.Length >= repository.FullName.Length; ancestor = ancestor.Parent)
    {
        if ((ancestor.Attributes & FileAttributes.ReparsePoint) != 0) { throw new InvalidOperationException(); }
    }

    var builder = new NpgsqlConnectionStringBuilder
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
    await using var bootstrap = new NpgsqlConnection(builder.ConnectionString);
    await bootstrap.OpenAsync();
    await using (var location = new NpgsqlCommand("SHOW data_directory", bootstrap))
    {
        var actual = (string)(await location.ExecuteScalarAsync())!;
        if (!string.Equals(Path.GetFullPath(actual).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException();
        }
    }
    await using (var fresh = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin274') OR (datname='fa_test_fin274' AND $1='initial')", bootstrap))
    {
        fresh.Parameters.AddWithValue(mode);
        if ((long)(await fresh.ExecuteScalarAsync())! != 0) { throw new InvalidOperationException(); }
    }

    const string database = "fa_test_fin274";
    var identity = new StorageIdentity("identity");
    var profile = new StorageIdentity("profile");
    if (mode == "initial")
    {
        stage = "isolated provisioning";
        await Sql(bootstrap, $"CREATE DATABASE {database}");
        await Sql(bootstrap, $"REVOKE ALL ON DATABASE {database} FROM PUBLIC");
        foreach (var owner in new[] { identity, profile })
        {
            await Sql(bootstrap, $"CREATE ROLE {owner.MigrationRole} LOGIN PASSWORD '{password}'; CREATE ROLE {owner.RuntimeRole} LOGIN PASSWORD '{password}'; GRANT CONNECT ON DATABASE {database} TO {owner.MigrationRole},{owner.RuntimeRole};");
        }
    }
    builder.Database = database;
    await using var admin = new NpgsqlConnection(builder.ConnectionString);
    await admin.OpenAsync();
    var identityOptions = new OwnedPostgresOptions(identity, database, port, password);
    var profileOptions = new OwnedPostgresOptions(profile, database, port, password);
    // Stable synthetic keys deliberately survive the fixture's database restart.
    using var protection = new RecordProtection(Enumerable.Repeat((byte)1, 32).ToArray(), "v1",
        new Dictionary<string, byte[]> { ["v1"] = Enumerable.Repeat((byte)2, 32).ToArray() });
    if (mode == "initial")
    {
        await Sql(admin, "REVOKE ALL ON SCHEMA public FROM PUBLIC");
        foreach (var owner in new[] { identity, profile })
        {
            await Sql(admin, $"CREATE SCHEMA {owner.Schema} AUTHORIZATION {owner.MigrationRole}; REVOKE ALL ON SCHEMA {owner.Schema} FROM PUBLIC; GRANT USAGE ON SCHEMA {owner.Schema} TO {owner.RuntimeRole};");
        }
        stage = "fresh and repeated migration";
        await OwnedPostgresStore.MigrateAsync(identityOptions, protection);
        await OwnedPostgresStore.MigrateAsync(identityOptions, protection);
        await Task.WhenAll(OwnedPostgresStore.MigrateAsync(profileOptions, protection), OwnedPostgresStore.MigrateAsync(profileOptions, protection));
        foreach (var owner in new[] { identity, profile })
        {
            await Sql(admin, $"GRANT SELECT,INSERT,UPDATE,DELETE ON {owner.Schema}.records TO {owner.RuntimeRole}; GRANT SELECT ON {owner.Schema}.schema_versions,{owner.Schema}.encryption_keys TO {owner.RuntimeRole};");
        }
    }

    await using var store = new OwnedPostgresStore(identityOptions, protection);
    await using var otherStore = new OwnedPostgresStore(profileOptions, protection);
    stage = "readiness";
    Check(await store.IsReadyAsync() && await otherStore.IsReadyAsync());
    if (mode == "restart")
    {
        stage = "actual database restart persistence";
        var persisted = await store.ExecuteAsync([], async (tx, _) => await tx.GetAsync("account", "synthetic_owner", "restart"));
        Check(persisted is { Revision: 0 } && Encoding.UTF8.GetString(persisted.Payload) == "retained synthetic state");
        var rolledBack = await store.ExecuteAsync([], async (tx, _) => await tx.GetAsync("account", "synthetic_owner", "rollback"));
        Check(rolledBack is null);
        Console.WriteLine("PASS actual database restart: encrypted state retained; uncommitted state absent.");
        return 0;
    }

    stage = "insert replay and encrypted storage";
    var value = Encoding.UTF8.GetBytes("retained synthetic state");
    Check(await store.ExecuteAsync(["account:owner"], async (tx, _) => await tx.InsertAsync("account", "synthetic_owner", "restart", value)));
    Check(!await TryWrite(() => store.ExecuteAsync(["account:owner"], async (tx, _) => await tx.InsertAsync("account", "synthetic_owner", "restart", value))));
    var wrongOwner = await store.ExecuteAsync([], async (tx, _) => await tx.GetAsync("account", "different_owner", "restart"));
    Check(wrongOwner is null);
    var wrongService = await otherStore.ExecuteAsync([], async (tx, _) => await tx.GetAsync("account", "synthetic_owner", "restart"));
    Check(wrongService is null);
    await using (var encrypted = new NpgsqlCommand($"SELECT payload,owner_hash,record_hash FROM {identity.Schema}.records LIMIT 1", admin))
    await using (var reader = await encrypted.ExecuteReaderAsync())
    {
        Check(await reader.ReadAsync());
        Check(!Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0)).Contains("synthetic"));
        Check(reader.GetFieldValue<byte[]>(1).Length == 32 && reader.GetFieldValue<byte[]>(2).Length == 32);
    }

    stage = "atomic domain and outbox rollback";
    try
    {
        await store.ExecuteAsync<bool>(["rollback"], async (tx, _) =>
        {
            Check(await tx.InsertAsync("account", "synthetic_owner", "rollback", [1]));
            Check(await tx.InsertAsync("outbox", "synthetic_owner", "rollback", [2]));
            throw new FixtureFailure();
        });
        throw new InvalidOperationException();
    }
    catch (FixtureFailure) { }
    Check(await store.ExecuteAsync([], async (tx, _) => await tx.GetAsync("account", "synthetic_owner", "rollback")) is null);
    Check(await store.ExecuteAsync([], async (tx, _) => await tx.GetAsync("outbox", "synthetic_owner", "rollback")) is null);
    Check(await store.ExecuteAsync(["commit"], async (tx, _) =>
        await tx.InsertAsync("account", "synthetic_owner", "committed", [1]) &&
        await tx.InsertAsync("outbox", "synthetic_owner", "committed", [2])));
    Check(await store.ExecuteAsync([], async (tx, _) => await tx.GetAsync("outbox", "synthetic_owner", "committed")) is not null);
    stage = "write conflict cannot commit a successful prefix";
    try
    {
        await store.ExecuteAsync(["conflict"], async (tx, _) =>
        {
            Check(await tx.InsertAsync("account", "synthetic_owner", "conflict_prefix", [1]));
            await tx.InsertAsync("outbox", "synthetic_owner", "committed", [9]);
            return true;
        });
        throw new FixtureFailure();
    }
    catch (StorageConflictException) { }
    Check(await store.ExecuteAsync([], async (tx, _) => await tx.GetAsync("account", "synthetic_owner", "conflict_prefix")) is null);

    stage = "revision race and sorted multi-lock serialization";
    Check(await store.ExecuteAsync([], async (tx, _) => await tx.InsertAsync("draft", "synthetic_owner", "race", [0])));
    var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => TryWrite(() => store.ExecuteAsync(["z", "a"],
        async (tx, _) => await tx.ReplaceAsync("draft", "synthetic_owner", "race", 0, [1])))));
    Check(results.Count(result => result) == 1);
    var updated = await store.ExecuteAsync([], async (tx, _) => await tx.GetAsync("draft", "synthetic_owner", "race"));
    Check(updated is { Revision: 1 } && updated.Payload.SequenceEqual(new byte[] { 1 }));
    Check(!await TryWrite(() => store.ExecuteAsync([], async (tx, _) => await tx.DeleteAsync("draft", "synthetic_owner", "race", 0))));
    Check(await store.ExecuteAsync([], async (tx, _) => await tx.DeleteAsync("draft", "synthetic_owner", "race", 1)));

    stage = "one transaction rejects concurrent commands";
    Check(await store.ExecuteAsync([], async (tx, _) => await tx.InsertAsync("account", "synthetic_owner", "busy", [7])));
    await using (var blocker = await admin.BeginTransactionAsync())
    {
        await Sql(admin, $"SELECT * FROM {identity.Schema}.records FOR UPDATE");
        Check(await store.ExecuteAsync([], async (tx, _) =>
        {
            var pending = tx.ReplaceAsync("account", "synthetic_owner", "busy", 0, [8]);
            try { await tx.GetAsync("account", "synthetic_owner", "busy"); throw new FixtureFailure(); }
            catch (InvalidOperationException) { }
            finally { await blocker.RollbackAsync(); }
            return await pending;
        }));
    }

    stage = "caller cancellation rollback";
    using (var cancellation = new CancellationTokenSource())
    {
        try
        {
            await store.ExecuteAsync(["cancel"], async (tx, _) =>
            {
                Check(await tx.InsertAsync("account", "synthetic_owner", "cancelled", [1]));
                cancellation.Cancel();
                return true;
            }, cancellation.Token);
            throw new InvalidOperationException();
        }
        catch (OperationCanceledException) { }
        Check(await store.ExecuteAsync([], async (tx, _) => await tx.GetAsync("account", "synthetic_owner", "cancelled")) is null);
    }
    stage = "expired transaction rejects later use";
    OwnedRecordTransaction? captured = null;
    await store.ExecuteAsync([], (tx, _) => { captured = tx; return Task.FromResult(true); });
    try { await captured!.GetAsync("account", "synthetic_owner", "restart"); throw new FixtureFailure(); }
    catch (InvalidOperationException) { }

    stage = "database role actually denies foreign SQL";
    var runtimeBuilder = new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Username = identity.RuntimeRole };
    await using (var runtime = new NpgsqlConnection(runtimeBuilder.ConnectionString))
    {
        await runtime.OpenAsync();
        try { await Sql(runtime, $"SELECT * FROM {profile.Schema}.records"); throw new FixtureFailure(); }
        catch (PostgresException exception) when (exception.SqlState == "42501") { }
    }
    stage = "misconfigured role and grants fail closed";
    await Sql(admin, $"GRANT USAGE ON SCHEMA {profile.Schema} TO {identity.RuntimeRole}");
    Check(!await store.IsReadyAsync());
    await Sql(admin, $"REVOKE USAGE ON SCHEMA {profile.Schema} FROM {identity.RuntimeRole}");
    await Sql(admin, $"GRANT TRUNCATE ON {identity.Schema}.records TO {identity.RuntimeRole}");
    Check(!await store.IsReadyAsync());
    await Sql(admin, $"REVOKE TRUNCATE ON {identity.Schema}.records FROM {identity.RuntimeRole}");
    await Sql(admin, $"GRANT {identity.MigrationRole} TO {identity.RuntimeRole}");
    Check(!await store.IsReadyAsync());
    await Sql(admin, $"REVOKE {identity.MigrationRole} FROM {identity.RuntimeRole}");
    await Sql(admin, $"ALTER ROLE {identity.RuntimeRole} SUPERUSER");
    Check(!await store.IsReadyAsync());
    await Sql(admin, $"ALTER ROLE {identity.RuntimeRole} NOSUPERUSER");
    Check(await store.IsReadyAsync());

    stage = "foreign/PUBLIC/schema/column grants and grant options fail closed";
    var category = new StorageIdentity("category");
    var categoryOptions = new OwnedPostgresOptions(category, database, port, password);
    await Sql(admin, $"CREATE ROLE {category.MigrationRole} LOGIN PASSWORD '{password}'; CREATE ROLE {category.RuntimeRole} LOGIN PASSWORD '{password}'; GRANT CONNECT ON DATABASE {database} TO {category.MigrationRole},{category.RuntimeRole}; CREATE SCHEMA {category.Schema} AUTHORIZATION {category.MigrationRole}; REVOKE ALL ON SCHEMA {category.Schema} FROM PUBLIC; GRANT USAGE ON SCHEMA {category.Schema} TO {category.RuntimeRole};");
    await Sql(admin, $"ALTER ROLE {category.RuntimeRole} RENAME TO fa_test_missing_category");
    try { await OwnedPostgresStore.MigrateAsync(categoryOptions, protection); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    await Sql(admin, $"ALTER ROLE fa_test_missing_category RENAME TO {category.RuntimeRole}");
    await Sql(admin, $"ALTER DEFAULT PRIVILEGES FOR ROLE {category.MigrationRole} IN SCHEMA {category.Schema} GRANT SELECT ON TABLES TO PUBLIC");
    try { await OwnedPostgresStore.MigrateAsync(categoryOptions, protection); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    await using (var absent = new NpgsqlCommand("SELECT to_regclass($1)::text", admin))
    {
        absent.Parameters.AddWithValue(category.Schema + ".records");
        Check(await absent.ExecuteScalarAsync() is not string);
    }
    await Sql(admin, $"ALTER DEFAULT PRIVILEGES FOR ROLE {category.MigrationRole} IN SCHEMA {category.Schema} REVOKE SELECT ON TABLES FROM PUBLIC");
    await OwnedPostgresStore.MigrateAsync(categoryOptions, protection);
    foreach (var grant in new[]
    {
        ($"GRANT SELECT ON {identity.Schema}.records TO PUBLIC", $"REVOKE SELECT ON {identity.Schema}.records FROM PUBLIC"),
        ($"GRANT UPDATE ON {identity.Schema}.records TO {profile.RuntimeRole}", $"REVOKE UPDATE ON {identity.Schema}.records FROM {profile.RuntimeRole}"),
        ($"GRANT SELECT(payload) ON {identity.Schema}.records TO {profile.RuntimeRole}", $"REVOKE SELECT(payload) ON {identity.Schema}.records FROM {profile.RuntimeRole}"),
        ($"GRANT USAGE ON SCHEMA {identity.Schema} TO {profile.RuntimeRole}", $"REVOKE USAGE ON SCHEMA {identity.Schema} FROM {profile.RuntimeRole}"),
        ($"GRANT SELECT ON {identity.Schema}.records TO {identity.RuntimeRole} WITH GRANT OPTION", $"REVOKE GRANT OPTION FOR SELECT ON {identity.Schema}.records FROM {identity.RuntimeRole}"),
        ($"GRANT SELECT ON {identity.Schema}.schema_versions TO PUBLIC", $"REVOKE SELECT ON {identity.Schema}.schema_versions FROM PUBLIC"),
        ($"GRANT SELECT ON {identity.Schema}.encryption_keys TO PUBLIC", $"REVOKE SELECT ON {identity.Schema}.encryption_keys FROM PUBLIC")
    })
    {
        await Sql(admin, grant.Item1);
        Check(!await store.IsReadyAsync());
        await Sql(admin, grant.Item2);
        Check(await store.IsReadyAsync());
    }
    stage = "missing or weakened CHECK constraints fail closed";
    var checks = new List<(string Table, string Name, string Definition)>();
    await using (var constraints = new NpgsqlCommand($"SELECT c.relname,k.conname,pg_get_constraintdef(k.oid,false) FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname=$1 AND k.contype='c' ORDER BY c.relname,k.conname", admin))
    {
        constraints.Parameters.AddWithValue(identity.Schema);
        await using var reader = await constraints.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { checks.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2))); }
    }
    Check(checks.Count == 7);
    foreach (var constraint in checks)
    {
        Check(constraint.Table is "records" or "schema_versions" or "encryption_keys" && constraint.Name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'));
        await Sql(admin, $"ALTER TABLE {identity.Schema}.{constraint.Table} DROP CONSTRAINT {constraint.Name}");
        Check(!await store.IsReadyAsync());
        await Sql(admin, $"ALTER TABLE {identity.Schema}.{constraint.Table} ADD CONSTRAINT {constraint.Name} {constraint.Definition}");
        Check(await store.IsReadyAsync());
    }
    await Sql(admin, $"ALTER TABLE {identity.Schema}.records DROP CONSTRAINT records_revision_check");
    Check(!await store.IsReadyAsync());
    await Sql(admin, $"ALTER TABLE {identity.Schema}.records ADD CONSTRAINT records_revision_check CHECK(revision>=-1)");
    Check(!await store.IsReadyAsync());
    try { await OwnedPostgresStore.MigrateAsync(identityOptions, protection); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    await Sql(admin, $"ALTER TABLE {identity.Schema}.records DROP CONSTRAINT records_revision_check; ALTER TABLE {identity.Schema}.records ADD CONSTRAINT records_revision_check CHECK(revision>=0)");
    Check(await store.IsReadyAsync());
    await Sql(admin, $"ALTER TABLE {identity.Schema}.encryption_keys DROP CONSTRAINT encryption_keys_marker_check; ALTER TABLE {identity.Schema}.encryption_keys ADD CONSTRAINT encryption_keys_marker_check CHECK(octet_length(marker)=32) NOT VALID");
    Check(!await store.IsReadyAsync());
    await Sql(admin, $"ALTER TABLE {identity.Schema}.encryption_keys VALIDATE CONSTRAINT encryption_keys_marker_check");
    Check(await store.IsReadyAsync());

    stage = "schema drift fails readiness";
    await Sql(admin, $"ALTER TABLE {identity.Schema}.records RENAME COLUMN payload TO wrong_payload");
    Check(!await store.IsReadyAsync());
    await Sql(admin, $"ALTER TABLE {identity.Schema}.records RENAME COLUMN wrong_payload TO payload");
    Check(await store.IsReadyAsync());

    stage = "bounded lock contention";
    var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var holder = store.ExecuteAsync(["held"], async (_, token) => { held.SetResult(); await release.Task.WaitAsync(token); return true; });
    await held.Task;
    try
    {
        try { await store.ExecuteAsync(["held"], (_, _) => Task.FromResult(true)); throw new FixtureFailure(); }
        catch (StorageUnavailableException exception) { Check(exception.InnerException is null); }
    }
    finally { release.TrySetResult(); await holder; }

    stage = "wrong schema version and checksum fail closed";
    string originalChecksum;
    await using (var original = new NpgsqlCommand($"SELECT checksum FROM {identity.Schema}.schema_versions", admin))
    {
        originalChecksum = (string)(await original.ExecuteScalarAsync())!;
    }
    await Sql(admin, $"UPDATE {identity.Schema}.schema_versions SET version=2");
    Check(!await store.IsReadyAsync());
    await Sql(admin, $"UPDATE {identity.Schema}.schema_versions SET version=1");
    await Sql(admin, $"UPDATE {identity.Schema}.schema_versions SET checksum='wrong' RETURNING checksum");
    Check(!await store.IsReadyAsync());
    try { await OwnedPostgresStore.MigrateAsync(identityOptions, protection); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    // Restore only this fixture's saved checksum for the separate restart check.
    await using (var restore = new NpgsqlCommand($"UPDATE {identity.Schema}.schema_versions SET checksum=$1", admin))
    {
        restore.Parameters.AddWithValue(originalChecksum);
        await restore.ExecuteNonQueryAsync();
    }
    Check(await store.IsReadyAsync());
    stage = "wrong index or encryption keys fail closed";
    using (var wrongIndex = new RecordProtection(Enumerable.Repeat((byte)3, 32).ToArray(), "v1",
        new Dictionary<string, byte[]> { ["v1"] = Enumerable.Repeat((byte)2, 32).ToArray() }))
    await using (var wrongStore = new OwnedPostgresStore(identityOptions, wrongIndex))
    {
        Check(!await wrongStore.IsReadyAsync());
        try { await OwnedPostgresStore.MigrateAsync(identityOptions, wrongIndex); throw new FixtureFailure(); }
        catch (StorageUnavailableException) { }
    }
    using (var wrongEncryption = new RecordProtection(Enumerable.Repeat((byte)1, 32).ToArray(), "v1",
        new Dictionary<string, byte[]> { ["v1"] = Enumerable.Repeat((byte)3, 32).ToArray() }))
    await using (var wrongStore = new OwnedPostgresStore(identityOptions, wrongEncryption))
    {
        Check(!await wrongStore.IsReadyAsync());
    }
    stage = "explicit key rotation retains old ciphertext";
    using (var rotated = new RecordProtection(Enumerable.Repeat((byte)1, 32).ToArray(), "v2",
        new Dictionary<string, byte[]> { ["v1"] = Enumerable.Repeat((byte)2, 32).ToArray(), ["v2"] = Enumerable.Repeat((byte)3, 32).ToArray() }))
    await using (var rotatedStore = new OwnedPostgresStore(identityOptions, rotated))
    {
        Check(!await rotatedStore.IsReadyAsync());
        await OwnedPostgresStore.MigrateAsync(identityOptions, rotated);
        Check(await rotatedStore.IsReadyAsync());
        Check(!await store.IsReadyAsync());
        Check(await rotatedStore.ExecuteAsync([], async (tx, _) => await tx.GetAsync("account", "synthetic_owner", "restart")) is not null);
        Check(await rotatedStore.ExecuteAsync([], async (tx, _) => await tx.InsertAsync("account", "synthetic_owner", "rotated", [3])));
        Check(await rotatedStore.ExecuteAsync([], async (tx, _) => await tx.DeleteAsync("account", "synthetic_owner", "rotated", 0)));
    }
    // Only the synthetic test removes the unused v2 marker; production key removal needs its own migration.
    await Sql(admin, $"DELETE FROM {identity.Schema}.encryption_keys WHERE key_id='v2'");
    Check(await store.IsReadyAsync());
    Console.WriteLine("PASS initial PostgreSQL: migration, complete ACL/constraint validation, role isolation, encryption/key manifest/rotation, replay, atomic commit/rollback, revision race, deletion, cancellation, schema drift, lock timeout and negative readiness.");
    return 0;
}
catch
{
    Console.Error.WriteLine("FAIL PostgreSQL integration at " + stage + ". Details suppressed to protect credentials and payloads.");
    return 1;
}

static void Check(bool condition) { if (!condition) { throw new FixtureFailure(); } }
static string Setting(string name) => Environment.GetEnvironmentVariable(name) ?? "";
static async Task<bool> TryWrite(Func<Task<bool>> operation)
{
    try { return await operation(); }
    catch (StorageConflictException) { return false; }
}
static async Task Sql(NpgsqlConnection connection, string sql)
{
    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}
sealed class FixtureFailure : Exception;
