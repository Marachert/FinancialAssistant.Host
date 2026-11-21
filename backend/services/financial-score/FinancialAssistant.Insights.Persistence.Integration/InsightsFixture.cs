using System.Security.Cryptography;
using System.Text;
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
    var marker = Path.Combine(data.Parent!.FullName, "recovery.run");
    var recovery = File.Exists(marker);
    Check(!recovery || new FileInfo(marker).Length == 32 && (File.GetAttributes(marker) & FileAttributes.ReparsePoint) == 0
        && File.ReadAllText(marker) == data.Parent.Name && Setting("FA_RECOVERY_TEST_SCOPE") == "Insights");
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
    var database = mode == "restore" ? "fa_test_fin209_restore" : "fa_test_fin288";
    var catalog = recovery
        ? "SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin288','fa_test_fin209_restore') OR (datname IN('fa_test_fin288','fa_test_fin209_restore') AND $1='initial')"
        : "SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin288') OR (datname='fa_test_fin288' AND $1='initial')";
    await using (var fresh = new NpgsqlCommand(catalog, bootstrap))
    { fresh.Parameters.AddWithValue(mode); Check((long)(await fresh.ExecuteScalarAsync())! == 0); }
    var identities = new[] { new StorageIdentity("financial-score"), new StorageIdentity("recommendations-notifications") };
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
    stage = "Insight exact persistence";
    var restoredRoot = mode == "restore" ? data.Parent.FullName : null;
    var scoreSettings = Settings(port, database, password, 17, 19, "financial-score", restoredRoot);
    var insightSettings = Settings(port, database, password, 23, 29, "recommendations-notifications", restoredRoot);
    if (mode == "initial")
    {
        stage = "repeatable owned migration";
        await Sql(admin, "REVOKE ALL ON SCHEMA public FROM PUBLIC");
        foreach (var identity in identities)
        {
            var settings = identity.ServiceId == "financial-score" ? scoreSettings : insightSettings;
            await Sql(admin, $"CREATE SCHEMA {identity.Schema} AUTHORIZATION {identity.MigrationRole}; REVOKE ALL ON SCHEMA {identity.Schema} FROM PUBLIC; GRANT USAGE ON SCHEMA {identity.Schema} TO {identity.RuntimeRole}");
            using var protection = settings.CreateProtection();
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(identity.ServiceId), protection);
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(identity.ServiceId), protection);
            await Sql(admin, $"GRANT SELECT,INSERT,UPDATE,DELETE ON {identity.Schema}.records TO {identity.RuntimeRole}; GRANT SELECT ON {identity.Schema}.schema_versions,{identity.Schema}.encryption_keys TO {identity.RuntimeRole}");
        }
    }
    if (recovery && mode != "initial")
    {
        stage = "Insight exact persistence";
        await InsightRecoveryFixture.VerifyAsync(scoreSettings, insightSettings, admin, async () =>
        {
            InsightRecoveryFixture.Stage = "score-replay";
            await ScoreFixture.VerifyAsync(scoreSettings, admin, "restore");
            InsightRecoveryFixture.Stage = "recommendation-replay";
            await RecommendationFixture.VerifyAsync(insightSettings, admin, "restore");
            InsightRecoveryFixture.Stage = "ordered-replay";
            await NativeInsightOrderFixture.VerifyAsync(insightSettings, "restore");
            InsightRecoveryFixture.Stage = "pending-work";
            await NativeInsightOutboxFixture.VerifyAsync(insightSettings, "restore");
        });
    }
    else
    {
        stage = "atomic Score state replay and recovery";
        await ScoreFixture.VerifyAsync(scoreSettings, admin, mode);
        stage = "atomic Recommendation Notification preference and publication recovery";
        await RecommendationFixture.VerifyAsync(insightSettings, admin, mode);
        stage = "native causal insight and trigger admission";
        await NativeInsightOrderFixture.VerifyAsync(insightSettings, mode);
        stage = "native bounded recommendation outbox discovery";
        await NativeInsightOutboxFixture.VerifyAsync(insightSettings, mode);
        if (recovery)
        {
            stage = "Insight recovery snapshot";
            await InsightRecoveryFixture.CaptureAsync(scoreSettings, insightSettings, admin);
        }
    }
    stage = "separate Score and Recommendation runtime role isolation";
    foreach (var pair in new[] { (identities[0], identities[1]), (identities[1], identities[0]) })
    {
        var restricted = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Username = pair.Item1.RuntimeRole };
        await using var runtime = new NpgsqlConnection(restricted.ConnectionString);
        await runtime.OpenAsync();
        try { await Sql(runtime, $"SELECT * FROM {pair.Item2.Schema}.records"); throw new FixtureFailure(); }
        catch (PostgresException exception) when (exception.SqlState == "42501") { }
    }
    stage = "encrypted minimized owned insight records";
    foreach (var identity in identities)
    {
        await using var raw = new NpgsqlCommand($"SELECT payload FROM {identity.Schema}.records", admin);
        await using var reader = await raw.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { Check(!Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0)).Contains("synthetic", StringComparison.Ordinal)); }
    }
    Console.WriteLine("PASS Insights PostgreSQL mode=" + mode + ": separate Score and Recommendation/Notification roles, atomic owned state/settings/preferences/outbox and recovery. Actual transport, provider delivery and installed acceptance remain unqualified.");
    return 0;
}
catch (Exception exception)
{
    if (stage == "native causal insight and trigger admission")
    { Console.Error.WriteLine("Native insight fixture gate=" + NativeInsightOrderFixture.Gate + "; type=" + exception.GetType().Name); }
    Console.Error.WriteLine("FAIL Insights PostgreSQL at " + stage + ". Sensitive details suppressed.");
    if (stage == "Insight exact persistence")
    { Console.Error.WriteLine("Insight recovery fixture phase: " + InsightRecoveryFixture.Stage); }
    return 1;
}

static string Setting(string name) => Environment.GetEnvironmentVariable(name) ?? throw new FixtureFailure();
static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
static async Task Sql(NpgsqlConnection connection, string sql)
{ await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
static NativePostgresSettings Settings(int port, string database, string password, byte index, byte encryption, string owner, string? restoredRoot)
{
    var keys = new byte[64];
    try
    {
        if (restoredRoot is not null)
        {
            var path = Path.Combine(restoredRoot, "recovered-" + owner + ".keys");
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
internal sealed class FixtureFailure : Exception;
