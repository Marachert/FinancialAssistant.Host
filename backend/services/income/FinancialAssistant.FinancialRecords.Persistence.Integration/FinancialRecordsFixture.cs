using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Contracts;
using FinancialAssistant.Expense.Domain;
using FinancialAssistant.Expense.Infrastructure;
using FinancialAssistant.Expense.Infrastructure.Persistence;
using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Contracts;
using FinancialAssistant.Income.Domain;
using FinancialAssistant.Income.Infrastructure;
using FinancialAssistant.Income.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Contracts;
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
    Check(password.Length == 64 && password.All(char.IsAsciiHexDigit) && Path.IsPathFullyQualified(directory)
        && Directory.Exists(directory) && mode is "initial" or "restart");
    Check(int.TryParse(Setting("FA_PERSISTENCE_TEST_PORT"), out var port) && port is >= 49152 and <= 65535);
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
    const string database = "fa_test_fin275";
    await using (var fresh = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin275') OR (datname='fa_test_fin275' AND $1='initial')", bootstrap))
    { fresh.Parameters.AddWithValue(mode); Check((long)(await fresh.ExecuteScalarAsync())! == 0); }
    var owners = new[] { new StorageIdentity("income"), new StorageIdentity("expense") };
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
    var incomeSettings = Settings(database, port, password, 1);
    var expenseSettings = Settings(database, port, password, 3);
    if (mode == "initial")
    {
        stage = "separate repeated migrations";
        await Sql(admin, "REVOKE ALL ON SCHEMA public FROM PUBLIC");
        foreach (var owner in owners)
        {
            await Sql(admin, $"CREATE SCHEMA {owner.Schema} AUTHORIZATION {owner.MigrationRole}; REVOKE ALL ON SCHEMA {owner.Schema} FROM PUBLIC; GRANT USAGE ON SCHEMA {owner.Schema} TO {owner.RuntimeRole}");
            var settings = owner.ServiceId == "income" ? incomeSettings : expenseSettings;
            using var protection = settings.CreateProtection();
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(owner.ServiceId), protection);
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(owner.ServiceId), protection);
            await Sql(admin, $"GRANT SELECT,INSERT,UPDATE,DELETE ON {owner.Schema}.records TO {owner.RuntimeRole}; GRANT SELECT ON {owner.Schema}.schema_versions,{owner.Schema}.encryption_keys TO {owner.RuntimeRole}");
        }
    }
    stage = "Income exact persistence, replay, currency, concurrency, rollback and retention";
    await VerifyIncome(incomeSettings, mode);
    stage = "Expense exact persistence, replay, currency, concurrency, rollback and retention";
    await VerifyExpense(expenseSettings, mode);
    stage = "cross-service role denial and encryption";
    foreach (var pair in new[] { ("income", "expense"), ("expense", "income") })
    {
        var isolated = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Username = new StorageIdentity(pair.Item1).RuntimeRole };
        await using var runtime = new NpgsqlConnection(isolated.ConnectionString);
        await runtime.OpenAsync();
        try { await Sql(runtime, $"SELECT * FROM fa_{pair.Item2}.records"); throw new FixtureFailure(); }
        catch (PostgresException exception) when (exception.SqlState == "42501") { }
    }
    foreach (var owner in owners)
    {
        await using var raw = new NpgsqlCommand($"SELECT payload FROM {owner.Schema}.records", admin);
        await using var reader = await raw.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { Check(!Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0)).Contains("synthetic", StringComparison.Ordinal)); }
    }
    Console.WriteLine("PASS Income/Expense PostgreSQL: separate owned roles/keys, exact snapshots, atomic record/outbox, immutable replay after corrections, owner/currency isolation, concurrency/CAS, rollback, pending-safe bounded retention and actual crash/restart.");
    return 0;
}
catch
{
    Console.Error.WriteLine("FAIL Income/Expense PostgreSQL at " + stage + ". Sensitive details suppressed.");
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

static ServiceProvider IncomeProvider(NativePostgresSettings settings, DateTimeOffset now, bool failOutbox = false)
{
    var services = new ServiceCollection().AddLogging().AddIncomeApplication().AddIncomeInfrastructure(Configuration("Income", settings), true);
    services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FixedClock(now)));
    if (failOutbox) { services.Replace(ServiceDescriptor.Singleton<IIncomeRecordEventPublisher>(provider => new FaultingIncomePublisher(provider.GetRequiredService<PostgresIncomeRecordEventPublisher>()))); }
    return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
}

static async Task VerifyIncome(NativePostgresSettings settings, string mode)
{
    var now = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
    var date = new DateOnly(2026, 10, 12);
    const string first = "synthetic-owner-a";
    const string second = "synthetic-owner-b";
    var confirmation = new TransactionConfirmedIntegrationEvent("synthetic-event", "synthetic-confirmed", first, "synthetic-draft",
        "income", 123.45m, "EUR", "income.salary", "Synthetic merchant", date, now, "synthetic-correlation");
    await using var provider = IncomeProvider(settings, now.AddMinutes(1));
    var store = provider.GetRequiredService<PostgresIncomeRecordStore>();
    var service = provider.GetRequiredService<IIncomeManagementService>();
    var outbox = provider.GetRequiredService<PostgresIncomeRecordEventPublisher>();
    var consumer = provider.GetRequiredService<ITransactionConfirmedConsumer>();
    var database = provider.GetRequiredService<OwnedPostgresStore>();
    Check(await database.IsReadyAsync());
    Check((await provider.GetRequiredService<OwnedStorageHealthCheck>().CheckHealthAsync(new HealthCheckContext())).Status == HealthStatus.Unhealthy);
    if (mode == "restart")
    {
        var saved = await store.GetAsync(first, confirmation.TransactionId, default) ?? throw new FixtureFailure();
        Check(saved.Amount == 130.25m && saved.Revision == 10 && saved.Status == IncomeRecordStatuses.Active
            && saved.Currency == "EUR" && saved.SourceDraftId == "synthetic-draft" && saved.ConfirmedAtUtc == now);
        await database.ExecuteAsync(new[] { first }, async (records, _) =>
        {
            var proof = OwnedRecordCodec.Decode<SnapshotProof>(1, await records.GetAsync("fixture_proof", first, "snapshot") ?? throw new FixtureFailure());
            Check(proof.Hash == Hash(saved));
            return true;
        });
        var count = (await outbox.ReadPendingAsync()).Count;
        await consumer.ConsumeAsync(confirmation, default);
        await consumer.ConsumeAsync(confirmation with { EventId = "synthetic-alternate-restart" }, default);
        Check(await store.GetAsync(first, confirmation.TransactionId, default) == saved && (await outbox.ReadPendingAsync()).Count == count);
        return;
    }
    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => consumer.ConsumeAsync(confirmation, default)));
    Check((await outbox.ReadPendingAsync()).Count == 1);
    Check((await store.GetAsync(first, confirmation.TransactionId, default))?.Amount == 123.45m);
    Check(await store.GetAsync(second, confirmation.TransactionId, default) is null);
    await RejectConfirmation(consumer, confirmation with { Amount = 999m });
    await RejectConfirmation(consumer, confirmation with { EventId = "other-event", UserId = second });
    await RejectConfirmation(consumer, confirmation with { EventId = "conflicting-original", Amount = 124m });
    Check(await store.GetAsync(second, confirmation.TransactionId, default) is null);
    var manualUsd = await service.CreateAsync(first, new CreateIncomeRequest(10.125m, "USD", "income.salary", null, date), default);
    var otherOwner = await service.CreateAsync(second, new CreateIncomeRequest(88m, "USD", "income.salary", null, date), default);
    var totals = await service.ListAsync(first, date, date, false, default);
    Check(totals.ActiveTotals.Count == 2 && totals.ActiveTotals.Single(value => value.Currency == "EUR").Amount == 123.45m
        && totals.ActiveTotals.Single(value => value.Currency == "USD").Amount == 10.12m);
    Check((await service.ListAsync(second, date, date, false, default)).ActiveTotals.Single().Amount == 88m);
    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.UpdateAsync(first, confirmation.TransactionId,
        new UpdateIncomeRequest(130.25m, "EUR", "income.salary", "Updated merchant", date), default)));
    var updated = await store.GetAsync(first, confirmation.TransactionId, default) ?? throw new FixtureFailure();
    Check(updated.Revision == 8 && updated.Amount == 130.25m);
    var stale = await store.ReplaceAsync(first, updated.TransactionId, 0, updated with { Revision = 1 }, default);
    Check(!stale.Replaced && stale.Record == updated);
    await service.ArchiveAsync(first, updated.TransactionId, default);
    Check((await service.ListAsync(first, date, date, false, default)).ActiveTotals.Single().Currency == "USD");
    await service.RestoreAsync(first, updated.TransactionId, default);
    var final = await store.GetAsync(first, updated.TransactionId, default) ?? throw new FixtureFailure();
    Check(final.Revision == 10 && final.Amount == 130.25m);
    var beforeReplay = (await outbox.ReadPendingAsync()).Count;
    await consumer.ConsumeAsync(confirmation, default);
    await consumer.ConsumeAsync(confirmation with { EventId = "synthetic-alternate" }, default);
    Check(await store.GetAsync(first, updated.TransactionId, default) == final && (await outbox.ReadPendingAsync()).Count == beforeReplay);
    await RejectConfirmation(consumer, confirmation with { EventId = "new-conflict", Amount = final.Amount });
    await using (var fault = IncomeProvider(settings, now.AddMinutes(2), failOutbox: true))
    {
        var before = (await store.ListAsync(first, date, date, true, default)).Count;
        try
        {
            await fault.GetRequiredService<IIncomeManagementService>().CreateAsync(first,
            new CreateIncomeRequest(7m, "USD", "income.salary", null, date), default); throw new FixtureFailure();
        }
        catch (StorageUnavailableException) { }
        Check((await store.ListAsync(first, date, date, true, default)).Count == before && (await outbox.ReadPendingAsync()).Count == beforeReplay);
        try
        {
            await fault.GetRequiredService<IIncomeManagementService>().UpdateAsync(first, final.TransactionId,
            new UpdateIncomeRequest(final.Amount + 1m, "EUR", "income.salary", null, date), default); throw new FixtureFailure();
        }
        catch (StorageUnavailableException) { }
        Check(await store.GetAsync(first, final.TransactionId, default) == final && (await outbox.ReadPendingAsync()).Count == beforeReplay);
        try
        {
            await fault.GetRequiredService<ITransactionConfirmedConsumer>().ConsumeAsync(confirmation with
            { EventId = "synthetic-failed-event", TransactionId = "synthetic-failed-record" }, default); throw new FixtureFailure();
        }
        catch (StorageUnavailableException) { }
        Check(await store.GetAsync(first, "synthetic-failed-record", default) is null);
    }
    const string corrupt = "synthetic-corrupt";
    await database.ExecuteAsync(new[] { corrupt }, async (records, _) =>
    {
        await records.InsertAsync("financial_records", corrupt, "bad", Encoding.UTF8.GetBytes("{\"version\":1,\"value\":{}}"));
        return true;
    });
    try { await store.GetAsync(corrupt, "bad", default); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    await using (var old = IncomeProvider(settings, now.AddDays(-40)))
    {
        for (var item = 0; item < 32; item++)
        { await old.GetRequiredService<IIncomeManagementService>().CreateAsync("synthetic-retention", new CreateIncomeRequest(1m, "USD", "income.salary", null, date), default); }
    }
    var acknowledged = new HashSet<string>();
    for (var pass = 0; pass < 8 && acknowledged.Count < 32; pass++)
    {
        var batch = await outbox.ReadPendingAsync();
        Check(batch.Count <= 25);
        foreach (var message in batch.Where(message => message.Event.OccurredAtUtc == now.AddDays(-40)))
        { await outbox.MarkPublishedAsync(message, now.AddDays(-31)); acknowledged.Add(message.Event.EventId); }
    }
    Check(acknowledged.Count == 32);
    var removed = 0;
    for (var pass = 0; pass < 8 && removed < 32; pass++)
    {
        var count = await outbox.CleanupPublishedBatchAsync(now);
        Check(count is >= 0 and <= 25);
        removed += count;
    }
    Check(removed == 32 && (await outbox.ReadPendingAsync()).Count == beforeReplay);
    Check((await store.ListAsync("synthetic-retention", date, date, true, default)).Count == 32);
    var creation = (await outbox.ReadPendingAsync()).Single(message => message.Event.Payload.RecordId == confirmation.TransactionId
        && message.Event.Payload.Revision == 0);
    await outbox.MarkPublishedAsync(creation, now.AddDays(1));
    var creationRemoved = 0;
    for (var pass = 0; pass < 8 && creationRemoved == 0; pass++)
    { creationRemoved += await outbox.CleanupPublishedBatchAsync(now.AddDays(32)); }
    Check(creationRemoved == 1);
    var remaining = (await outbox.ReadPendingAsync()).Count;
    await consumer.ConsumeAsync(confirmation with { EventId = "synthetic-replay-after-retention" }, default);
    Check((await outbox.ReadPendingAsync()).Count == remaining && remaining == beforeReplay - 1
        && await store.GetAsync(first, final.TransactionId, default) == final);
    await database.ExecuteAsync(new[] { first }, async (records, _) =>
    {
        await records.InsertAsync("fixture_proof", first, "snapshot", OwnedRecordCodec.Encode(1, new SnapshotProof(Hash(final))));
        return true;
    });
}

static ServiceProvider ExpenseProvider(NativePostgresSettings settings, DateTimeOffset now, bool failOutbox = false)
{
    var services = new ServiceCollection().AddLogging().AddExpenseApplication().AddExpenseInfrastructure(Configuration("Expense", settings), true);
    services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FixedClock(now)));
    if (failOutbox) { services.Replace(ServiceDescriptor.Singleton<IExpenseRecordEventPublisher>(provider => new FaultingExpensePublisher(provider.GetRequiredService<PostgresExpenseRecordEventPublisher>()))); }
    return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
}

static async Task VerifyExpense(NativePostgresSettings settings, string mode)
{
    var now = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
    var date = new DateOnly(2026, 10, 12);
    const string first = "synthetic-owner-a";
    const string second = "synthetic-owner-b";
    var confirmation = new TransactionConfirmedIntegrationEvent("synthetic-event", "synthetic-confirmed", first, "synthetic-draft",
        "expense", 123.45m, "EUR", "expense.groceries", "Synthetic merchant", date, now, "synthetic-correlation");
    await using var provider = ExpenseProvider(settings, now.AddMinutes(1));
    var store = provider.GetRequiredService<PostgresExpenseRecordStore>();
    var service = provider.GetRequiredService<IExpenseManagementService>();
    var outbox = provider.GetRequiredService<PostgresExpenseRecordEventPublisher>();
    var consumer = provider.GetRequiredService<ITransactionConfirmedConsumer>();
    var database = provider.GetRequiredService<OwnedPostgresStore>();
    Check(await database.IsReadyAsync());
    Check((await provider.GetRequiredService<OwnedStorageHealthCheck>().CheckHealthAsync(new HealthCheckContext())).Status == HealthStatus.Unhealthy);
    if (mode == "restart")
    {
        var saved = await store.GetAsync(first, confirmation.TransactionId, default) ?? throw new FixtureFailure();
        Check(saved.Amount == 130.25m && saved.Revision == 10 && saved.Status == ExpenseRecordStatuses.Active
            && saved.Currency == "EUR" && saved.SourceDraftId == "synthetic-draft" && saved.ConfirmedAtUtc == now);
        await database.ExecuteAsync(new[] { first }, async (records, _) =>
        {
            var proof = OwnedRecordCodec.Decode<SnapshotProof>(1, await records.GetAsync("fixture_proof", first, "snapshot") ?? throw new FixtureFailure());
            Check(proof.Hash == Hash(saved));
            return true;
        });
        var count = (await outbox.ReadPendingAsync()).Count;
        await consumer.ConsumeAsync(confirmation, default);
        await consumer.ConsumeAsync(confirmation with { EventId = "synthetic-alternate-restart" }, default);
        Check(await store.GetAsync(first, confirmation.TransactionId, default) == saved && (await outbox.ReadPendingAsync()).Count == count);
        return;
    }
    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => consumer.ConsumeAsync(confirmation, default)));
    Check((await outbox.ReadPendingAsync()).Count == 1);
    Check((await store.GetAsync(first, confirmation.TransactionId, default))?.Amount == 123.45m);
    Check(await store.GetAsync(second, confirmation.TransactionId, default) is null);
    await RejectConfirmation(consumer, confirmation with { Amount = 999m });
    await RejectConfirmation(consumer, confirmation with { EventId = "other-event", UserId = second });
    await RejectConfirmation(consumer, confirmation with { EventId = "conflicting-original", Amount = 124m });
    Check(await store.GetAsync(second, confirmation.TransactionId, default) is null);
    var manualUsd = await service.CreateAsync(first, new CreateExpenseRequest(10.125m, "USD", "expense.groceries", null, date), default);
    var otherOwner = await service.CreateAsync(second, new CreateExpenseRequest(88m, "USD", "expense.groceries", null, date), default);
    var totals = await service.ListAsync(first, date, date, false, default);
    Check(totals.ActiveTotals.Count == 2 && totals.ActiveTotals.Single(value => value.Currency == "EUR").Amount == 123.45m
        && totals.ActiveTotals.Single(value => value.Currency == "USD").Amount == 10.12m);
    Check((await service.ListAsync(second, date, date, false, default)).ActiveTotals.Single().Amount == 88m);
    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.UpdateAsync(first, confirmation.TransactionId,
        new UpdateExpenseRequest(130.25m, "EUR", "expense.groceries", "Updated merchant", date), default)));
    var updated = await store.GetAsync(first, confirmation.TransactionId, default) ?? throw new FixtureFailure();
    Check(updated.Revision == 8 && updated.Amount == 130.25m);
    var stale = await store.ReplaceAsync(first, updated.TransactionId, 0, updated with { Revision = 1 }, default);
    Check(!stale.Replaced && stale.Record == updated);
    await service.ArchiveAsync(first, updated.TransactionId, default);
    Check((await service.ListAsync(first, date, date, false, default)).ActiveTotals.Single().Currency == "USD");
    await service.RestoreAsync(first, updated.TransactionId, default);
    var final = await store.GetAsync(first, updated.TransactionId, default) ?? throw new FixtureFailure();
    Check(final.Revision == 10 && final.Amount == 130.25m);
    var beforeReplay = (await outbox.ReadPendingAsync()).Count;
    await consumer.ConsumeAsync(confirmation, default);
    await consumer.ConsumeAsync(confirmation with { EventId = "synthetic-alternate" }, default);
    Check(await store.GetAsync(first, updated.TransactionId, default) == final && (await outbox.ReadPendingAsync()).Count == beforeReplay);
    await RejectConfirmation(consumer, confirmation with { EventId = "new-conflict", Amount = final.Amount });
    await using (var fault = ExpenseProvider(settings, now.AddMinutes(2), failOutbox: true))
    {
        var before = (await store.ListAsync(first, date, date, true, default)).Count;
        try
        {
            await fault.GetRequiredService<IExpenseManagementService>().CreateAsync(first,
            new CreateExpenseRequest(7m, "USD", "expense.groceries", null, date), default); throw new FixtureFailure();
        }
        catch (StorageUnavailableException) { }
        Check((await store.ListAsync(first, date, date, true, default)).Count == before && (await outbox.ReadPendingAsync()).Count == beforeReplay);
        try
        {
            await fault.GetRequiredService<IExpenseManagementService>().UpdateAsync(first, final.TransactionId,
            new UpdateExpenseRequest(final.Amount + 1m, "EUR", "expense.groceries", null, date), default); throw new FixtureFailure();
        }
        catch (StorageUnavailableException) { }
        Check(await store.GetAsync(first, final.TransactionId, default) == final && (await outbox.ReadPendingAsync()).Count == beforeReplay);
        try
        {
            await fault.GetRequiredService<ITransactionConfirmedConsumer>().ConsumeAsync(confirmation with
            { EventId = "synthetic-failed-event", TransactionId = "synthetic-failed-record" }, default); throw new FixtureFailure();
        }
        catch (StorageUnavailableException) { }
        Check(await store.GetAsync(first, "synthetic-failed-record", default) is null);
    }
    const string corrupt = "synthetic-corrupt";
    await database.ExecuteAsync(new[] { corrupt }, async (records, _) =>
    {
        await records.InsertAsync("financial_records", corrupt, "bad", Encoding.UTF8.GetBytes("{\"version\":1,\"value\":{}}"));
        return true;
    });
    try { await store.GetAsync(corrupt, "bad", default); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    await using (var old = ExpenseProvider(settings, now.AddDays(-40)))
    {
        for (var item = 0; item < 32; item++)
        { await old.GetRequiredService<IExpenseManagementService>().CreateAsync("synthetic-retention", new CreateExpenseRequest(1m, "USD", "expense.groceries", null, date), default); }
    }
    var acknowledged = new HashSet<string>();
    for (var pass = 0; pass < 8 && acknowledged.Count < 32; pass++)
    {
        var batch = await outbox.ReadPendingAsync();
        Check(batch.Count <= 25);
        foreach (var message in batch.Where(message => message.Event.OccurredAtUtc == now.AddDays(-40)))
        { await outbox.MarkPublishedAsync(message, now.AddDays(-31)); acknowledged.Add(message.Event.EventId); }
    }
    Check(acknowledged.Count == 32);
    var removed = 0;
    for (var pass = 0; pass < 8 && removed < 32; pass++)
    {
        var count = await outbox.CleanupPublishedBatchAsync(now);
        Check(count is >= 0 and <= 25);
        removed += count;
    }
    Check(removed == 32 && (await outbox.ReadPendingAsync()).Count == beforeReplay);
    Check((await store.ListAsync("synthetic-retention", date, date, true, default)).Count == 32);
    var creation = (await outbox.ReadPendingAsync()).Single(message => message.Event.Payload.RecordId == confirmation.TransactionId
        && message.Event.Payload.Revision == 0);
    await outbox.MarkPublishedAsync(creation, now.AddDays(1));
    var creationRemoved = 0;
    for (var pass = 0; pass < 8 && creationRemoved == 0; pass++)
    { creationRemoved += await outbox.CleanupPublishedBatchAsync(now.AddDays(32)); }
    Check(creationRemoved == 1);
    var remaining = (await outbox.ReadPendingAsync()).Count;
    await consumer.ConsumeAsync(confirmation with { EventId = "synthetic-replay-after-retention" }, default);
    Check((await outbox.ReadPendingAsync()).Count == remaining && remaining == beforeReplay - 1
        && await store.GetAsync(first, final.TransactionId, default) == final);
    await database.ExecuteAsync(new[] { first }, async (records, _) =>
    {
        await records.InsertAsync("fixture_proof", first, "snapshot", OwnedRecordCodec.Encode(1, new SnapshotProof(Hash(final))));
        return true;
    });
}

static async Task RejectConfirmation(ITransactionConfirmedConsumer consumer, TransactionConfirmedIntegrationEvent value)
{
    try { await consumer.ConsumeAsync(value, default); throw new FixtureFailure(); }
    catch (FinancialAssistant.Income.Infrastructure.Persistence.FinancialConfirmationConflictException) { }
    catch (FinancialAssistant.Expense.Infrastructure.Persistence.FinancialConfirmationConflictException) { }
}
sealed class FixtureFailure : Exception;
sealed record SnapshotProof(string Hash);
sealed class FixedClock(DateTimeOffset now) : TimeProvider
{ public override DateTimeOffset GetUtcNow() => now; }

sealed class FaultingIncomePublisher(IIncomeRecordEventPublisher inner) : IIncomeRecordEventPublisher
{
    public async Task PublishAsync(string eventType, IncomeRecord record, string correlationId, string causationId, CancellationToken token)
    { await inner.PublishAsync(eventType, record, correlationId, causationId, token); throw new StorageUnavailableException(); }
}

sealed class FaultingExpensePublisher(IExpenseRecordEventPublisher inner) : IExpenseRecordEventPublisher
{
    public async Task PublishAsync(string eventType, ExpenseRecord record, string correlationId, string causationId, CancellationToken token)
    { await inner.PublishAsync(eventType, record, correlationId, causationId, token); throw new StorageUnavailableException(); }
}
