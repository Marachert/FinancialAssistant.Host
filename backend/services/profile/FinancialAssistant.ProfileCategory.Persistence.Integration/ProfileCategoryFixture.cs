using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialAssistant.Category.Application;
using FinancialAssistant.Category.Application.Abstractions;
using FinancialAssistant.Category.Application.Categories;
using FinancialAssistant.Category.Contracts;
using FinancialAssistant.Category.Domain.Categories;
using FinancialAssistant.Category.Infrastructure;
using FinancialAssistant.Category.Infrastructure.Events;
using FinancialAssistant.Category.Infrastructure.Storage;
using FinancialAssistant.Profile.Application;
using FinancialAssistant.Profile.Application.Abstractions;
using FinancialAssistant.Profile.Domain.Profiles;
using FinancialAssistant.Profile.Infrastructure;
using FinancialAssistant.Profile.Infrastructure.Storage;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

var stage = "owned fixture validation";
try
{
    var password = Setting("FA_PERSISTENCE_TEST_PASSWORD");
    var directory = Setting("FA_PERSISTENCE_TEST_DIRECTORY");
    var mode = Setting("FA_PERSISTENCE_TEST_MODE");
    if (password.Length != 64 || !password.All(char.IsAsciiHexDigit) || !Path.IsPathFullyQualified(directory)
        || !Directory.Exists(directory) || mode is not ("initial" or "restart")
        || !int.TryParse(Setting("FA_PERSISTENCE_TEST_PORT"), out var port) || port is < 49152 or > 65535)
    { throw new FixtureFailure(); }
    var repository = new DirectoryInfo(AppContext.BaseDirectory);
    while (!File.Exists(Path.Combine(repository.FullName, "FinancialAssistant.Backend.sln")))
    { repository = repository.Parent ?? throw new FixtureFailure(); }
    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    var data = new DirectoryInfo(Path.GetFullPath(directory));
    Check(data.Name == "data" && data.Parent is not null && Guid.TryParseExact(data.Parent.Name, "N", out _)
        && string.Equals(data.Parent.Parent?.FullName, Path.Combine(repository.FullName, ".codex-runtime", "persistence-test"), comparison));
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
    const string database = "fa_test_fin283";
    await using (var fresh = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin283') OR (datname='fa_test_fin283' AND $1='initial')", bootstrap))
    { fresh.Parameters.AddWithValue(mode); Check((long)(await fresh.ExecuteScalarAsync())! == 0); }
    var owners = new[] { new StorageIdentity("profile"), new StorageIdentity("category") };
    if (mode == "initial")
    {
        stage = "isolated role provisioning";
        await Sql(bootstrap, $"CREATE DATABASE {database}");
        await Sql(bootstrap, $"REVOKE ALL ON DATABASE {database} FROM PUBLIC");
        foreach (var owner in owners)
        { await Sql(bootstrap, $"CREATE ROLE {owner.MigrationRole} LOGIN PASSWORD '{password}'; CREATE ROLE {owner.RuntimeRole} LOGIN PASSWORD '{password}'; GRANT CONNECT ON DATABASE {database} TO {owner.MigrationRole},{owner.RuntimeRole}"); }
    }
    connection.Database = database;
    await using var admin = new NpgsqlConnection(connection.ConnectionString);
    await admin.OpenAsync();
    var profileSettings = Settings(database, port, password, 1);
    var categorySettings = Settings(database, port, password, 3);
    if (mode == "initial")
    {
        stage = "separate fresh and repeat migrations";
        await Sql(admin, "REVOKE ALL ON SCHEMA public FROM PUBLIC");
        foreach (var owner in owners)
        {
            await Sql(admin, $"CREATE SCHEMA {owner.Schema} AUTHORIZATION {owner.MigrationRole}; REVOKE ALL ON SCHEMA {owner.Schema} FROM PUBLIC; GRANT USAGE ON SCHEMA {owner.Schema} TO {owner.RuntimeRole}");
            var settings = owner.ServiceId == "profile" ? profileSettings : categorySettings;
            using var protection = settings.CreateProtection();
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(owner.ServiceId), protection);
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(owner.ServiceId), protection);
            await Sql(admin, $"GRANT SELECT,INSERT,UPDATE,DELETE ON {owner.Schema}.records TO {owner.RuntimeRole}; GRANT SELECT ON {owner.Schema}.schema_versions,{owner.Schema}.encryption_keys TO {owner.RuntimeRole}");
        }
    }
    await using var profileProvider = ProfileProvider(profileSettings);
    await using var categoryProvider = CategoryProvider(categorySettings);
    var profiles = profileProvider.GetRequiredService<PostgresProfileStore>();
    var categories = categoryProvider.GetRequiredService<PostgresCategoryStore>();
    var categoryService = categoryProvider.GetRequiredService<ICategoryService>();
    var outbox = categoryProvider.GetRequiredService<PostgresCategoryEventPublisher>();
    var categoryContext = categoryProvider.GetRequiredService<CategoryPersistenceContext>();
    const string first = "synthetic-owner-a";
    const string second = "synthetic-owner-b";
    var now = new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
    Check(await profileProvider.GetRequiredService<OwnedPostgresStore>().IsReadyAsync());
    Check(await categoryProvider.GetRequiredService<OwnedPostgresStore>().IsReadyAsync());
    Check((await profileProvider.GetRequiredService<OwnedStorageHealthCheck>().CheckHealthAsync(new HealthCheckContext())).Status == HealthStatus.Unhealthy);
    Check((await categoryProvider.GetRequiredService<OwnedStorageHealthCheck>().CheckHealthAsync(new HealthCheckContext())).Status == HealthStatus.Unhealthy);
    if (mode == "restart")
    {
        stage = "actual crash restart preserves exact profile and category state";
        var profile = await profiles.GetAsync(first, default) ?? throw new FixtureFailure();
        Check(profile.Preferences.MonthlyBudgetAmount == 12345678901234567890.12345678m && profile.Preferences.PrivacyMode == "strict"
            && profile.CreatedAtUtc == now && profile.UpdatedAtUtc == now.AddMinutes(2));
        var catalog = await categories.GetAsync(first, default) ?? throw new FixtureFailure();
        await profileProvider.GetRequiredService<OwnedPostgresStore>().ExecuteAsync(new[] { first }, async (records, _) =>
        {
            var proof = OwnedRecordCodec.Decode<SnapshotProof>(1, await records.GetAsync("fixture_assertions", first, "restart") ?? throw new FixtureFailure());
            Check(proof.ProfileHash == Hash(profile) && proof.CategoryHash == Hash(catalog));
            return true;
        });
        Check(catalog.Find("expense.food") is { Version: 2 } food && food.Aliases.SequenceEqual(new[] { "market", "shop" }));
        Check(await profiles.GetAsync(second, default) is null && await categories.GetAsync(second, default) is null);
        Check((await outbox.ReadPendingAsync()).Count == 1);
        Console.WriteLine("PASS Profile/Category actual crash restart: exact decimal/preferences/taxonomy/aliases/version/time/outbox and deletion markers retained.");
        return 0;
    }

    stage = "atomic concurrent bootstrap preserves two isolated users";
    await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => profiles.CreateDefaultIfMissingAsync(UserProfile.CreateDefault(first, now), default)));
    await profiles.CreateDefaultIfMissingAsync(UserProfile.CreateDefault(second, now), default);
    await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => categories.CreateDefaultIfMissingAsync(UserCategoryCatalog.CreateDefault(first, now), default)));
    await categories.CreateDefaultIfMissingAsync(UserCategoryCatalog.CreateDefault(second, now), default);
    stage = "concurrent profile updates have no lost decimal increments";
    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => profiles.UpdateAsync(first,
        profile => profile.UpdatePreferences(Budget(profile.Preferences, profile.Preferences.MonthlyBudgetAmount + 0.01m), now.AddMinutes(1)), default)));
    Check((await profiles.GetAsync(first, default))?.Preferences.MonthlyBudgetAmount == 0.08m);
    Check((await profiles.GetAsync(second, default))?.Preferences.MonthlyBudgetAmount == 0m);
    await profiles.UpdateAsync(first, profile => profile.UpdatePreferences(UserProfilePreferences.Create("en-US", "UTC", "USD", "strict", true,
        "sunday", 12345678901234567890.12345678m, true, true, true, true), now.AddMinutes(2)), default);
    await profiles.CreateDefaultIfMissingAsync(UserProfile.CreateDefault(first, now.AddDays(1)), default);
    Check((await profiles.GetAsync(first, default))?.Preferences.MonthlyBudgetAmount == 12345678901234567890.12345678m);
    try { await profiles.UpdateAsync(first, _ => UserProfile.CreateDefault(second, now), default); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }

    stage = "alias concurrent no-op version and event idempotency";
    await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => categoryService.ReplaceAliasesAsync(first, "expense.food",
        new UpdateCategoryAliasesRequest(new[] { " SHOP ", "Market", "shop" }, "synthetic-alias"), default)));
    var changed = (await categories.GetAsync(first, default))?.Find("expense.food") ?? throw new FixtureFailure();
    Check(changed.Version == 2 && changed.Aliases.SequenceEqual(new[] { "market", "shop" }));
    Check((await categories.GetAsync(second, default))?.Find("expense.food")?.Version == 1);
    await categories.CreateDefaultIfMissingAsync(UserCategoryCatalog.CreateDefault(first, now.AddDays(1)), default);
    Check((await categories.GetAsync(first, default))?.Find("expense.food")?.Version == 2);
    var pending = await outbox.ReadPendingAsync();
    Check(pending.Count == 1 && pending[0].Event.Version == 2 && !pending[0].Event.ToString().Contains("market", StringComparison.Ordinal));
    await outbox.PublishAsync(pending[0].Event, default);
    try { await outbox.PublishAsync(pending[0].Event with { CorrelationId = "synthetic-conflict" }, default); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    try { await outbox.MarkPublishedAsync(pending[0] with { EventId = Guid.NewGuid().ToString("D") }, now); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }

    stage = "real service outbox failure rolls back alias state and event";
    await using (var faulty = CategoryProvider(categorySettings, failOutbox: true))
    {
        try { await faulty.GetRequiredService<ICategoryService>().ReplaceAliasesAsync(first, "expense.food", new UpdateCategoryAliasesRequest(new[] { "failed" }, "synthetic-failure"), default); throw new FixtureFailure(); }
        catch (StorageUnavailableException) { }
    }
    Check((await categories.GetAsync(first, default))?.Find("expense.food")?.Version == 2);
    Check((await outbox.ReadPendingAsync()).Count == 1);

    stage = "invalid owned versions and missing DTO fields fail closed";
    var corruptIndex = 0;
    foreach (var invalid in new[] { "{\"version\":2,\"value\":{}}", "{\"version\":1,\"value\":{}}" })
    {
        var corruptOwner = "synthetic-corrupt-" + corruptIndex++;
        await profileProvider.GetRequiredService<OwnedPostgresStore>().ExecuteAsync(new[] { corruptOwner }, async (records, _) =>
        { await records.InsertAsync("profiles", corruptOwner, "profile", Encoding.UTF8.GetBytes(invalid)); return true; });
        try { await profiles.GetAsync(corruptOwner, default); throw new FixtureFailure(); }
        catch (StorageUnavailableException exception) { Check(exception.InnerException is null); }
        await profiles.DeleteAsync(corruptOwner, now);
        await categoryProvider.GetRequiredService<OwnedPostgresStore>().ExecuteAsync(new[] { corruptOwner }, async (records, _) =>
        { await records.InsertAsync("catalogs", corruptOwner, "catalog", Encoding.UTF8.GetBytes(invalid)); return true; });
        try { await categories.GetAsync(corruptOwner, default); throw new FixtureFailure(); }
        catch (StorageUnavailableException exception) { Check(exception.InnerException is null); }
        await categories.DeleteAsync(corruptOwner, now);
    }

    stage = "capture all exact persisted domain fields before abrupt restart";
    var finalProfile = await profiles.GetAsync(first, default) ?? throw new FixtureFailure();
    var finalCatalog = await categories.GetAsync(first, default) ?? throw new FixtureFailure();
    await profileProvider.GetRequiredService<OwnedPostgresStore>().ExecuteAsync(new[] { first }, async (records, _) =>
    { await records.InsertAsync("fixture_assertions", first, "restart", OwnedRecordCodec.Encode(1, new SnapshotProof(Hash(finalProfile), Hash(finalCatalog)))); return true; });

    stage = "owner context and actual cross-role SQL denial";
    try { await categoryContext.RunAsync(first, token => categories.GetAsync(second, token)); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    try { new PostgresProfileStore(categoryProvider.GetRequiredService<OwnedPostgresStore>()); throw new FixtureFailure(); }
    catch (ArgumentException) { }
    foreach (var pair in new[] { ("fa_profile", "fa_category"), ("fa_category", "fa_profile") })
    {
        var role = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Username = pair.Item1 };
        await using var restricted = new NpgsqlConnection(role.ConnectionString);
        await restricted.OpenAsync();
        try { await Sql(restricted, $"SELECT * FROM {pair.Item2}.records"); throw new FixtureFailure(); }
        catch (PostgresException exception) when (exception.SqlState == "42501") { }
    }

    stage = "erasure markers prevent stale bootstrap resurrection";
    Check(await profiles.DeleteAsync(second, now.AddMinutes(3)));
    Check(await categories.DeleteAsync(second, now.AddMinutes(3)));
    Check(!await profiles.DeleteAsync(second, now.AddMinutes(4)) && !await categories.DeleteAsync(second, now.AddMinutes(4)));
    try { await profiles.CreateDefaultIfMissingAsync(UserProfile.CreateDefault(second, now), default); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    try { await categories.CreateDefaultIfMissingAsync(UserCategoryCatalog.CreateDefault(second, now), default); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    try { await outbox.PublishAsync(new CategoryUpdatedIntegrationEvent(second, "expense.food", 2, "aliases_replaced", now, "synthetic-erased"), default); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }

    stage = "pending intents survive retention and published cleanup is bounded";
    await outbox.CleanupPublishedBatchAsync(now.AddYears(1));
    Check((await outbox.ReadPendingAsync()).Count == 1);
    await outbox.PublishAsync(new CategoryUpdatedIntegrationEvent(first, "expense.food", 99, "aliases_replaced", now.AddDays(-32), "synthetic-published"), default);
    var published = (await outbox.ReadPendingAsync()).Single(message => message.Event.Version == 99);
    await outbox.MarkPublishedAsync(published, now.AddDays(-31));
    Check(await outbox.CleanupPublishedBatchAsync(now) == 1);
    Check((await outbox.ReadPendingAsync()).Count == 1);

    stage = "multi-page published retention never exceeds its deletion bound";
    for (var version = 100; version < 132; version++)
    {
        await outbox.PublishAsync(new CategoryUpdatedIntegrationEvent(first, "expense.food", version, "aliases_replaced", now.AddDays(-32), "synthetic-retention"), default);
    }
    var acknowledged = new HashSet<string>(StringComparer.Ordinal);
    for (var pass = 0; pass < 5 && acknowledged.Count < 32; pass++)
    {
        var batch = await outbox.ReadPendingAsync();
        Check(batch.Count <= 25 && batch.Select(message => message.EventId).Distinct().Count() == batch.Count);
        foreach (var message in batch.Where(message => message.Event.Version >= 100))
        { await outbox.MarkPublishedAsync(message, now.AddDays(-31)); acknowledged.Add(message.EventId); }
    }
    Check(acknowledged.Count == 32);
    var removed = 0;
    for (var pass = 0; pass < 4 && removed < 32; pass++)
    {
        var count = await outbox.CleanupPublishedBatchAsync(now);
        Check(count is >= 0 and <= 25);
        removed += count;
    }
    Check(removed == 32 && (await outbox.ReadPendingAsync()).Count == 1);

    stage = "encrypted storage does not expose owned values";
    foreach (var owner in owners)
    {
        await using var raw = new NpgsqlCommand($"SELECT payload FROM {owner.Schema}.records", admin);
        await using var reader = await raw.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { Check(!Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0)).Contains("synthetic", StringComparison.Ordinal)); }
    }
    Console.WriteLine("PASS Profile/Category PostgreSQL: separate roles/keys, exact rehydration, concurrent bootstrap/updates, owner isolation, alias no-op/event replay, atomic outbox failure rollback, cross-role denial, erasure/replay markers and pending-safe bounded retention.");
    return 0;
}
catch
{
    Console.Error.WriteLine("FAIL Profile/Category PostgreSQL at " + stage + ". Sensitive details suppressed.");
    return 1;
}

static string Setting(string name) => Environment.GetEnvironmentVariable(name) ?? "";
static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
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
    EncryptionKeys = new() { ["v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)(index + 1), 32).ToArray()) }
};
static IConfiguration Configuration(string prefix, NativePostgresSettings settings) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    [$"{prefix}:Storage:Postgres:Database"] = settings.Database,
    [$"{prefix}:Storage:Postgres:Port"] = settings.Port.ToString(),
    [$"{prefix}:Storage:Postgres:Password"] = settings.Password,
    [$"{prefix}:Storage:Postgres:IndexKey"] = settings.IndexKey,
    [$"{prefix}:Storage:Postgres:ActiveEncryptionKeyId"] = settings.ActiveEncryptionKeyId,
    [$"{prefix}:Storage:Postgres:EncryptionKeys:v1"] = settings.EncryptionKeys["v1"]
}).Build();
static ServiceProvider ProfileProvider(NativePostgresSettings settings)
{
    var services = new ServiceCollection(); services.AddLogging(); services.AddProfileApplication();
    services.AddProfileInfrastructure(Configuration("Profile", settings), true);
    return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
}
static ServiceProvider CategoryProvider(NativePostgresSettings settings, bool failOutbox = false)
{
    var services = new ServiceCollection(); services.AddLogging(); services.AddCategoryApplication();
    services.AddCategoryInfrastructure(Configuration("Category", settings), true);
    if (failOutbox) { services.Replace(ServiceDescriptor.Singleton<ICategoryEventPublisher>(provider => new FaultingPublisher(provider.GetRequiredService<PostgresCategoryEventPublisher>()))); }
    return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
}
static UserProfilePreferences Budget(UserProfilePreferences p, decimal amount) => UserProfilePreferences.Create(p.Locale, p.TimeZone, p.CurrencyCode,
    p.PrivacyMode, p.AiPersonalizationEnabled, p.FirstDayOfWeek, amount, p.BudgetNotificationsEnabled, p.WeeklySummaryNotificationsEnabled,
    p.ProfileOnboardingCompleted, p.PreferencesOnboardingCompleted);
sealed class FixtureFailure : Exception;
sealed record SnapshotProof(string ProfileHash, string CategoryHash);
sealed class FaultingPublisher(ICategoryEventPublisher inner) : ICategoryEventPublisher
{
    public async Task PublishAsync(CategoryUpdatedIntegrationEvent value, CancellationToken token)
    { await inner.PublishAsync(value, token); throw new StorageUnavailableException(); }
}
