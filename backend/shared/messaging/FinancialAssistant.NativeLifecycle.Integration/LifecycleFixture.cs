using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Messaging;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Contracts;
using Npgsql;

return await LifecycleFixture.MainAsync(args);

internal static partial class LifecycleFixture
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    internal static readonly string[] Roles = ["profile", "transaction-intake", "income", "expense", "analytics", "financial-score", "recommendations-notifications", "audit"];
    internal const string Database = "fa_test_fin276";
    internal const string User = "synthetic-native-owner";
    internal static string Stage = "arguments";

    internal static async Task<int> MainAsync(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "prepare")
            {
                Check(Environment.GetEnvironmentVariable("FA_NATIVE_BROKER_SYNTHETIC_ONLY") == "1");
                var root = Root(args[1]); Check(int.TryParse(args[2], out var port) && port is >= 49152 and <= 65535);
                Prepare(root, port); return 0;
            }
            Check(args.Length == 0);
            var fixture = Root(Setting("FA_NATIVE_BROKER_FIXTURE_ROOT"));
            var mode = Setting("FA_PERSISTENCE_TEST_MODE"); Check(mode is "initial" or "restart");
            var credentials = JsonSerializer.Deserialize<Credentials>(File.ReadAllBytes(Path.Combine(fixture, "credentials.json")), Json) ?? throw new FixtureFailure();
            Check(credentials.RunId == Path.GetFileName(fixture) && credentials.SyntheticOnly && credentials.Port is >= 49152 and <= 65535 &&
                credentials.Vhost == "fa-synthetic-" + credentials.RunId && Roles.All(role => credentials.Stores.ContainsKey(role)));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(110));
            Stage = "owned PostgreSQL provisioning";
            await ProvisionAsync(credentials, mode, deadline.Token);
            Stage = "automatic native financial delivery";
            await RunAsync(fixture, credentials, mode, deadline.Token);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL native lifecycle gate=" + Stage + "; type=" + exception.GetType().Name + ". Sensitive details suppressed.");
            return 1;
        }
    }

    private static void Prepare(string root, int port)
    {
        var financial = new[]
        {
            NativeBinding.Event(FinancialRecordEventTypes.IncomeCreated, "income"), NativeBinding.Event(FinancialRecordEventTypes.IncomeUpdated, "income"),
            NativeBinding.Event(FinancialRecordEventTypes.IncomeArchived, "income"), NativeBinding.Event(FinancialRecordEventTypes.IncomeRestored, "income"),
            NativeBinding.Event(FinancialRecordEventTypes.ExpenseCreated, "expense"), NativeBinding.Event(FinancialRecordEventTypes.ExpenseUpdated, "expense"),
            NativeBinding.Event(FinancialRecordEventTypes.ExpenseArchived, "expense"), NativeBinding.Event(FinancialRecordEventTypes.ExpenseRestored, "expense")
        };
        var subscriptions = new[]
        {
            new NativeSubscription("income", "confirmed", [NativeBinding.Command("income", "transaction-confirmed", TransactionConfirmedIntegrationEvent.Name, "transaction-intake")]),
            new NativeSubscription("expense", "confirmed", [NativeBinding.Command("expense", "transaction-confirmed", TransactionConfirmedIntegrationEvent.Name, "transaction-intake")]),
            new NativeSubscription("analytics", "financial", financial), new NativeSubscription("financial-score", "financial", financial),
            new NativeSubscription("recommendations-notifications", "insights", [NativeBinding.Event(AnalyticsEventTypes.AnalyticsUpdated, "analytics"), NativeBinding.Event(FinancialScoreEventTypes.ScoreCalculated, "financial-score")]),
            new NativeSubscription("recommendations-notifications", "prepared", [NativeBinding.Command("recommendations-notifications", "recommendation-generated", RecommendationEventTypes.RecommendationGenerated, "recommendations-notifications")]),
            new NativeSubscription("audit", "events", [NativeBinding.Event(AuditEventTypes.Recorded, "income", "expense")])
        };
        var vhost = "fa-synthetic-" + Path.GetFileName(root);
        var topology = new NativeTopology(vhost, subscriptions, subscriptions.SelectMany(s => s.Bindings).ToArray());
        var passwords = topology.BrokerUsers.ToDictionary(user => user, _ => Secret(), StringComparer.Ordinal);
        var credentials = new Credentials(Path.GetFileName(root), true, port, vhost, passwords,
            Roles.ToDictionary(role => role, _ => new StoreKeys(Secret(), Secret(), Key(), Key()), StringComparer.Ordinal),
            new[] { "analytics", "financial-score", "recommendations-notifications" }.ToDictionary(role => role, _ => Secret(), StringComparer.Ordinal), Secret());
        WriteNew(root, "credentials.json", credentials);
        File.WriteAllBytes(Path.Combine(root, "definitions.json"), topology.CreateDefinitions(passwords));
    }

    private static async Task ProvisionAsync(Credentials credentials, string mode, CancellationToken token)
    {
        var password = Setting("FA_PERSISTENCE_TEST_PASSWORD");
        Check(password.Length == 64 && password.All(char.IsAsciiHexDigit));
        Check(int.TryParse(Setting("FA_PERSISTENCE_TEST_PORT"), out var port) && port is >= 49152 and <= 65535);
        var directory = Path.GetFullPath(Setting("FA_PERSISTENCE_TEST_DIRECTORY"));
        var repo = Path.GetFullPath(Setting("FA_NATIVE_LIFECYCLE_REPOSITORY"));
        Check(File.Exists(Path.Combine(repo, "FinancialAssistant.Backend.sln")) && Directory.Exists(Path.Combine(repo, ".git")));
        var data = new DirectoryInfo(directory);
        Check(data.Name == "data" && data.Parent is not null && Guid.TryParseExact(data.Parent.Name, "N", out _) &&
            string.Equals(data.Parent.Parent?.FullName, Path.Combine(repo, ".codex-runtime", "persistence-test"), StringComparison.OrdinalIgnoreCase));
        for (var ancestor = data; ancestor is not null && ancestor.FullName.Length >= repo.Length; ancestor = ancestor.Parent)
        { Check((ancestor.Attributes & FileAttributes.ReparsePoint) == 0); }
        var builder = new NpgsqlConnectionStringBuilder
        { Host = "127.0.0.1", Port = port, Database = "postgres", Username = "fa_test_admin", Password = password, Timeout = 3, CommandTimeout = 5, IncludeErrorDetail = false, LogParameters = false };
        await using var bootstrap = new NpgsqlConnection(builder.ConnectionString); await bootstrap.OpenAsync(token);
        await using (var location = new NpgsqlCommand("SHOW data_directory", bootstrap))
        { Check(string.Equals(Path.GetFullPath((string)(await location.ExecuteScalarAsync(token))!), directory, StringComparison.OrdinalIgnoreCase)); }
        await using (var fresh = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin276') OR (datname='fa_test_fin276' AND $1='initial')", bootstrap))
        { fresh.Parameters.AddWithValue(mode); Check((long)(await fresh.ExecuteScalarAsync(token))! == 0); }
        if (mode != "initial") { return; }
        await SqlAsync(bootstrap, "CREATE DATABASE " + Database, token);
        await SqlAsync(bootstrap, "REVOKE ALL ON DATABASE " + Database + " FROM PUBLIC", token);
        foreach (var role in Roles)
        {
            var identity = new StorageIdentity(role); var keys = credentials.Stores[role];
            Check(keys.RuntimePassword.Length == 64 && keys.RuntimePassword.All(char.IsAsciiHexDigit) &&
                keys.MigrationPassword.Length == 64 && keys.MigrationPassword.All(char.IsAsciiHexDigit) && keys.RuntimePassword != keys.MigrationPassword);
            await SqlAsync(bootstrap, $"CREATE ROLE {identity.MigrationRole} LOGIN PASSWORD '{keys.MigrationPassword}'; CREATE ROLE {identity.RuntimeRole} LOGIN PASSWORD '{keys.RuntimePassword}'; GRANT CONNECT ON DATABASE {Database} TO {identity.MigrationRole},{identity.RuntimeRole}", token);
        }
        builder.Database = Database;
        await using var admin = new NpgsqlConnection(builder.ConnectionString); await admin.OpenAsync(token);
        await SqlAsync(admin, "REVOKE ALL ON SCHEMA public FROM PUBLIC", token);
        foreach (var role in Roles)
        {
            var identity = new StorageIdentity(role);
            await SqlAsync(admin, $"CREATE SCHEMA {identity.Schema} AUTHORIZATION {identity.MigrationRole}; REVOKE ALL ON SCHEMA {identity.Schema} FROM PUBLIC; GRANT USAGE ON SCHEMA {identity.Schema} TO {identity.RuntimeRole}", token);
            var settings = Settings(credentials, role, port);
            using var protection = settings.CreateProtection();
            await OwnedPostgresStore.MigrateAsync(new OwnedPostgresOptions(identity, Database, port, credentials.Stores[role].MigrationPassword), protection, token);
            await SqlAsync(admin, $"GRANT SELECT,INSERT,UPDATE,DELETE ON {identity.Schema}.records TO {identity.RuntimeRole}; GRANT SELECT ON {identity.Schema}.schema_versions,{identity.Schema}.encryption_keys TO {identity.RuntimeRole}", token);
        }
    }

    internal static NativePostgresSettings Settings(Credentials credentials, string role, int port) => new()
    {
        Database = Database,
        Port = port,
        Password = credentials.Stores[role].RuntimePassword,
        IndexKey = credentials.Stores[role].IndexKey,
        ActiveEncryptionKeyId = "v1",
        EncryptionKeys = new() { ["v1"] = credentials.Stores[role].EncryptionKey }
    };
    internal static string Connection(Credentials credentials, string role) => "amqp://fa-" + role + ":" +
        Uri.EscapeDataString(credentials.BrokerPasswords["fa-" + role]) + "@127.0.0.1:" + credentials.Port + "/" + credentials.Vhost;
    private static string Root(string path)
    {
        var root = new DirectoryInfo(Path.GetFullPath(path));
        Check(root.Exists && Guid.TryParseExact(root.Name, "N", out _) && root.Parent?.Name == "broker-proof" && root.Parent.Parent?.Name == ".codex-runtime");
        for (var ancestor = root; ancestor is not null; ancestor = ancestor.Parent) { Check((ancestor.Attributes & FileAttributes.ReparsePoint) == 0); }
        return root.FullName;
    }
    internal static void WriteNew<T>(string root, string name, T value)
    { using var file = new FileStream(Path.Combine(root, name), FileMode.CreateNew, FileAccess.Write, FileShare.None); JsonSerializer.Serialize(file, value, Json); }
    internal static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
    internal static string Setting(string name) => Environment.GetEnvironmentVariable(name) ?? throw new FixtureFailure();
    private static string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Key() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static async Task SqlAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(token); }
    internal sealed record StoreKeys(string RuntimePassword, string MigrationPassword, string IndexKey, string EncryptionKey);
    internal sealed record Credentials(string RunId, bool SyntheticOnly, int Port, string Vhost, Dictionary<string, string> BrokerPasswords,
        Dictionary<string, StoreKeys> Stores, Dictionary<string, string> ProfileReaders, string GatewaySecret);
    private sealed class FixtureFailure : Exception;
}
