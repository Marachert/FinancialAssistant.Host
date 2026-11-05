using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialAssistant.Shared.Persistence;
using Npgsql;

var stage = "isolated fixture validation";
try
{
    var password = Setting("FA_PERSISTENCE_TEST_PASSWORD");
    var directory = Setting("FA_PERSISTENCE_TEST_DIRECTORY");
    var mode = Setting("FA_PERSISTENCE_TEST_MODE");
    Check(password.Length == 64 && password.All(char.IsAsciiHexDigit) && Path.IsPathFullyQualified(directory) &&
        Directory.Exists(directory) && mode is "initial" or "restart" or "restore");
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
    var recoveryMarker = Path.Combine(data.Parent!.FullName, "recovery.run");
    var recovery = File.Exists(recoveryMarker);
    Check(!recovery || new FileInfo(recoveryMarker).Length == 32 && (File.GetAttributes(recoveryMarker) & FileAttributes.ReparsePoint) == 0
        && File.ReadAllText(recoveryMarker) == data.Parent.Name && Setting("FA_RECOVERY_TEST_SCOPE") == "Audit");
    Check(mode != "restore" || recovery);
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
    var database = mode == "restore" ? "fa_test_fin209_restore" : "fa_test_fin289";
    var catalogQuery = recovery
        ? "SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin289','fa_test_fin209_restore') OR (datname IN('fa_test_fin289','fa_test_fin209_restore') AND $1='initial')"
        : "SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin289') OR (datname='fa_test_fin289' AND $1='initial')";
    await using (var fresh = new NpgsqlCommand(catalogQuery, bootstrap))
    { fresh.Parameters.AddWithValue(mode); Check((long)(await fresh.ExecuteScalarAsync())! == 0); }
    var identities = new[] { new StorageIdentity("audit"), new StorageIdentity("financial-score") };
    if (mode == "initial")
    {
        stage = "owned role provisioning";
        await Sql(bootstrap, $"CREATE DATABASE {database}");
        await Sql(bootstrap, $"REVOKE ALL ON DATABASE {database} FROM PUBLIC");
        foreach (var identity in identities)
        { await Sql(bootstrap, $"CREATE ROLE {identity.MigrationRole} LOGIN PASSWORD '{password}'; CREATE ROLE {identity.RuntimeRole} LOGIN PASSWORD '{password}'; GRANT CONNECT ON DATABASE {database} TO {identity.MigrationRole},{identity.RuntimeRole}"); }
    }
    connection.Database = database;
    await using var admin = new NpgsqlConnection(connection.ConnectionString);
    await admin.OpenAsync();
    stage = "Audit exact persistence";
    var recoveryRoot = mode == "restore" ? data.Parent.FullName : null;
    var auditSettings = Settings(port, database, password, 17, 19, recoveryRoot);
    var otherSettings = Settings(port, database, password, 23, 29, recoveryRoot);
    if (mode == "initial")
    {
        stage = "repeatable owned migration";
        await Sql(admin, "REVOKE ALL ON SCHEMA public FROM PUBLIC");
        foreach (var identity in identities)
        {
            var settings = identity.ServiceId == "audit" ? auditSettings : otherSettings;
            await Sql(admin, $"CREATE SCHEMA {identity.Schema} AUTHORIZATION {identity.MigrationRole}; REVOKE ALL ON SCHEMA {identity.Schema} FROM PUBLIC; GRANT USAGE ON SCHEMA {identity.Schema} TO {identity.RuntimeRole}");
            using var protection = settings.CreateProtection();
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(identity.ServiceId), protection);
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(identity.ServiceId), protection);
            await Sql(admin, $"GRANT SELECT,INSERT,UPDATE,DELETE ON {identity.Schema}.records TO {identity.RuntimeRole}; GRANT SELECT ON {identity.Schema}.schema_versions,{identity.Schema}.encryption_keys TO {identity.RuntimeRole}");
        }
    }
    if (recovery)
    {
        stage = "Audit exact persistence";
        using var otherProtection = otherSettings.CreateProtection();
        await using var otherDatabase = new OwnedPostgresStore(otherSettings.CreateOptions("financial-score"), otherProtection);
        Check(await otherDatabase.IsReadyAsync());
        await using var empty = new NpgsqlCommand("SELECT count(*) FROM fa_financial_score.records", admin);
        Check((long)(await empty.ExecuteScalarAsync())! == 0);
    }
    using var auditProtection = auditSettings.CreateProtection();
    await using var auditDatabase = new OwnedPostgresStore(auditSettings.CreateOptions("audit"), auditProtection);
    AuditRecoveryProof? proof = null;
    if (recovery && mode != "initial")
    {
        proof = await auditDatabase.ExecuteAsync([], async (records, _) =>
            OwnedRecordCodec.Decode<AuditRecoveryProof>(1, await records.GetAsync("fixture_recovery_proof", "audit", "snapshot") ?? throw new FixtureFailure()));
        Check(proof.Digest.Length == 64 && proof.Digest == await SnapshotHash(auditDatabase));
    }
    stage = mode == "restore" ? "Audit exact persistence" : "append-only Audit replay retention and crash recovery";
    await AuditFixture.VerifyAsync(auditSettings, admin, mode == "restore" ? "restart" : mode);
    if (recovery)
    {
        stage = "Audit recovery snapshot";
        if (mode == "initial")
        {
            var digest = await SnapshotHash(auditDatabase);
            await auditDatabase.ExecuteAsync([], async (records, _) =>
            {
                await records.InsertAsync("fixture_recovery_proof", "audit", "snapshot", OwnedRecordCodec.Encode(1, new AuditRecoveryProof(digest)));
                return true;
            });
        }
        else { Check(proof is not null && proof.Digest == await SnapshotHash(auditDatabase)); }
    }
    stage = "Audit runtime role isolation";
    foreach (var pair in new[] { (identities[0], identities[1]), (identities[1], identities[0]) })
    {
        var restricted = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Username = pair.Item1.RuntimeRole };
        await using var runtime = new NpgsqlConnection(restricted.ConnectionString);
        await runtime.OpenAsync();
        try { await Sql(runtime, $"SELECT * FROM {pair.Item2.Schema}.records"); throw new FixtureFailure(); }
        catch (PostgresException exception) when (exception.SqlState == "42501") { }
    }
    stage = "encrypted minimized Audit records";
    foreach (var identity in identities)
    {
        await using var raw = new NpgsqlCommand($"SELECT payload FROM {identity.Schema}.records", admin);
        await using var reader = await raw.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { Check(!Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0)).Contains("synthetic", StringComparison.Ordinal)); }
    }
    Console.WriteLine("PASS Audit PostgreSQL mode=" + mode + ": separate Audit role, append-only metadata, immutable replay, retention and recovery. Actual transport, provider delivery and installed acceptance remain unqualified.");
    return 0;
}
catch
{
    Console.Error.WriteLine("FAIL Audit PostgreSQL at " + stage + ". Sensitive details suppressed.");
    return 1;
}

static string Setting(string name) => Environment.GetEnvironmentVariable(name) ?? throw new FixtureFailure();
static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
static async Task Sql(NpgsqlConnection connection, string sql)
{ await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
static async Task<string> SnapshotHash(OwnedPostgresStore database) => await database.ExecuteAsync([], async (records, _) =>
{
    var snapshot = new List<AuditSnapshotItem>();
    foreach (var bucket in new[] { "audit_entries", "audit_receipts" })
    {
        string? after = null; var complete = false;
        for (var page = 0; page < 100; page++)
        {
            var items = await records.ReadPageAsync(bucket, "audit", after);
            if (items.Count == 0) { complete = true; break; }
            foreach (var item in items)
            { snapshot.Add(new AuditSnapshotItem(bucket, item.Cursor, item.Revision, Convert.ToHexString(SHA256.HashData(item.Payload)))); }
            Check(snapshot.Count <= 10000); after = items[^1].Cursor;
        }
        Check(complete);
    }
    Check(snapshot.Count > 1000);
    var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
    try { return Convert.ToHexString(SHA256.HashData(bytes)); }
    finally { CryptographicOperations.ZeroMemory(bytes); }
});
static NativePostgresSettings Settings(int port, string database, string password, byte index, byte encryption, string? recoveryRoot = null)
{
    var keys = new byte[64];
    try
    {
        if (recoveryRoot is not null)
        {
            var path = Path.Combine(recoveryRoot, "recovered-" + (index == 17 ? "audit" : "financial-score") + ".keys");
            Check(new FileInfo(path).Length == 64 && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0);
            keys = File.ReadAllBytes(path);
        }
        else { keys.AsSpan(0, 32).Fill(index); keys.AsSpan(32, 32).Fill(encryption); }
        return new NativePostgresSettings
        {
            Port = port,
            Database = database,
            Password = password,
            ActiveEncryptionKeyId = "v1",
            IndexKey = Convert.ToBase64String(keys.AsSpan(0, 32)),
            EncryptionKeys = new Dictionary<string, string> { ["v1"] = Convert.ToBase64String(keys.AsSpan(32, 32)) }
        };
    }
    finally { CryptographicOperations.ZeroMemory(keys); }
}
sealed record AuditRecoveryProof(string Digest);
sealed record AuditSnapshotItem(string Bucket, string Cursor, long Revision, string Digest);
internal sealed class FixtureFailure : Exception;
