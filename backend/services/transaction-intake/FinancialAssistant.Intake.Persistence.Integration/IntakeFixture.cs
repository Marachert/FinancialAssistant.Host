using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Infrastructure;
using FinancialAssistant.Expense.Infrastructure.Persistence;
using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Infrastructure;
using FinancialAssistant.Income.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Application;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;
using FinancialAssistant.TransactionIntake.Infrastructure;
using FinancialAssistant.TransactionIntake.Infrastructure.Events;
using FinancialAssistant.TransactionIntake.Infrastructure.Storage;
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
    const string databaseName = "fa_test_fin284";
    await using (var fresh = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin284') OR (datname='fa_test_fin284' AND $1='initial')", bootstrap))
    { fresh.Parameters.AddWithValue(mode); Check((long)(await fresh.ExecuteScalarAsync())! == 0); }
    var owners = new[] { new StorageIdentity("transaction-intake"), new StorageIdentity("income"), new StorageIdentity("expense") };
    if (mode == "initial")
    {
        stage = "isolated provisioning";
        await Sql(bootstrap, $"CREATE DATABASE {databaseName}");
        await Sql(bootstrap, $"REVOKE ALL ON DATABASE {databaseName} FROM PUBLIC");
        foreach (var owner in owners)
        { await Sql(bootstrap, $"CREATE ROLE {owner.MigrationRole} LOGIN PASSWORD '{password}'; CREATE ROLE {owner.RuntimeRole} LOGIN PASSWORD '{password}'; GRANT CONNECT ON DATABASE {databaseName} TO {owner.MigrationRole},{owner.RuntimeRole}"); }
    }
    connection.Database = databaseName;
    await using var admin = new NpgsqlConnection(connection.ConnectionString);
    await admin.OpenAsync();
    var intakeSettings = Settings(databaseName, port, password, 1);
    var incomeSettings = Settings(databaseName, port, password, 3);
    var expenseSettings = Settings(databaseName, port, password, 5);
    if (mode == "initial")
    {
        stage = "separate repeated migration";
        await Sql(admin, "REVOKE ALL ON SCHEMA public FROM PUBLIC");
        foreach (var owner in owners)
        {
            await Sql(admin, $"CREATE SCHEMA {owner.Schema} AUTHORIZATION {owner.MigrationRole}; REVOKE ALL ON SCHEMA {owner.Schema} FROM PUBLIC; GRANT USAGE ON SCHEMA {owner.Schema} TO {owner.RuntimeRole}");
            var settings = owner.ServiceId == "transaction-intake" ? intakeSettings : owner.ServiceId == "income" ? incomeSettings : expenseSettings;
            using var protection = settings.CreateProtection();
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(owner.ServiceId), protection);
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(owner.ServiceId), protection);
            await Sql(admin, $"GRANT SELECT,INSERT,UPDATE,DELETE ON {owner.Schema}.records TO {owner.RuntimeRole}; GRANT SELECT ON {owner.Schema}.schema_versions,{owner.Schema}.encryption_keys TO {owner.RuntimeRole}");
        }
    }
    var initial = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
    var clock = new MutableClock(mode == "restart" ? initial.AddHours(2) : initial);
    await using var provider = IntakeProvider(intakeSettings, clock);
    await using var incomeProvider = new ServiceCollection().AddLogging().AddIncomeApplication().AddIncomeInfrastructure(Configuration("Income", incomeSettings), true).BuildServiceProvider();
    await using var expenseProvider = new ServiceCollection().AddLogging().AddExpenseApplication().AddExpenseInfrastructure(Configuration("Expense", expenseSettings), true).BuildServiceProvider();
    var intake = provider.GetRequiredService<ITransactionIntakeService>();
    var review = provider.GetRequiredService<ITransactionDraftReviewService>();
    var confirmation = provider.GetRequiredService<ITransactionConfirmationService>();
    var drafts = provider.GetRequiredService<PostgresTransactionDraftStore>();
    var sources = provider.GetRequiredService<PostgresTransactionDraftCreationStore>();
    var confirmations = provider.GetRequiredService<PostgresTransactionConfirmationStore>();
    var outbox = provider.GetRequiredService<PostgresIntakeOutbox>();
    var database = provider.GetRequiredService<OwnedPostgresStore>();
    const string first = "synthetic-owner-a";
    const string second = "synthetic-owner-b";
    const string key = "synthetic-intake-key";
    const string input = "Received $2500 salary today";
    Check(!provider.GetServices<ITransactionConfirmedConsumer>().Any());
    Check(await database.IsReadyAsync());
    Check((await provider.GetRequiredService<OwnedStorageHealthCheck>().CheckHealthAsync(new HealthCheckContext())).Status == HealthStatus.Unhealthy);
    if (mode == "restart")
    {
        stage = "actual restart exact state and immutable replay";
        var proof = await database.ExecuteAsync(new[] { first }, async (records, _) =>
            OwnedRecordCodec.Decode<SnapshotProof>(1, await records.GetAsync("fixture_proof", first, "snapshot") ?? throw new FixtureFailure()));
        var draft = await drafts.GetByIdAsync(first, proof.DraftId, default) ?? throw new FixtureFailure();
        var stored = await confirmations.GetAsync(first, proof.DraftId, default) ?? throw new FixtureFailure();
        Check(proof.DraftHash == Hash(draft) && proof.ConfirmationHash == Hash(stored.IntegrationEvent));
        var count = (await outbox.ReadPendingAsync()).Count;
        var replay = await intake.CreateDraftAsync(first, key, new TransactionIntakeRequest(input), default);
        var replayConfirmation = await confirmation.ConfirmAsync(first, proof.DraftId, "restart-correlation", default) ?? throw new FixtureFailure();
        Check(replay.Replayed && replayConfirmation.Replayed && replayConfirmation.Transaction.TransactionId == proof.TransactionId);
        Check((await outbox.ReadPendingAsync()).Count == count && await sources.GetByReferenceAsync(first, proof.SourceReference, default) is null);
        var eventValue = stored.IntegrationEvent;
        await incomeProvider.GetRequiredService<ITransactionConfirmedConsumer>().ConsumeAsync(eventValue, default);
        Check((await incomeProvider.GetRequiredService<PostgresIncomeRecordStore>().ListAsync(first, eventValue.Date, eventValue.Date, true, default)).Count == 1);
        Console.WriteLine("PASS Intake PostgreSQL restart: exact draft/confirmation hashes, stable replay/outbox, no restored source text and no duplicate financial commit.");
        return 0;
    }

    stage = "concurrent draft creation and owner-key isolation";
    var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => intake.CreateDraftAsync(first, key, new TransactionIntakeRequest(input), default)));
    Check(attempts.Count(result => !result.Replayed) == 1 && attempts.Select(result => result.Draft.Id).Distinct().Count() == 1);
    var firstDraft = attempts[0].Draft;
    Check(firstDraft.Type == "income" && firstDraft.Amount == 2500m && firstDraft.Currency == "USD");
    try { await intake.CreateDraftAsync(first, key, new TransactionIntakeRequest("Received $999 salary today"), default); throw new FixtureFailure(); }
    catch (IdempotencyConflictException) { }
    var other = await intake.CreateDraftAsync(second, key, new TransactionIntakeRequest("Paid 25 EUR for taxi today"), default);
    Check(other.Draft.Id != firstDraft.Id && other.Draft.Currency == "EUR" && await drafts.GetByIdAsync(second, firstDraft.Id, default) is null);
    var sourceReference = "draft-payload-" + firstDraft.Id;
    Check((await sources.GetByReferenceAsync(first, sourceReference, default))?.NormalizedInput == input);
    Check(await sources.GetByReferenceAsync(second, sourceReference, default) is null);

    stage = "concurrent confirmation commits one intent without financial side effects";
    var confirmed = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => confirmation.ConfirmAsync(first, firstDraft.Id, "synthetic-correlation", default)));
    Check(confirmed.Count(result => result is not null && !result.Replayed) == 1);
    var result = confirmed[0] ?? throw new FixtureFailure();
    Check(confirmed.All(value => value?.Transaction.TransactionId == result.Transaction.TransactionId));
    var firstEvent = (await confirmations.GetAsync(first, firstDraft.Id, default))?.IntegrationEvent ?? throw new FixtureFailure();
    Check((await drafts.GetByIdAsync(first, firstDraft.Id, default))?.Status == TransactionDraftStatuses.Confirmed);
    Check(await incomeProvider.GetRequiredService<PostgresIncomeRecordStore>().GetAsync(first, result.Transaction.TransactionId, default) is null);
    Check(await confirmation.ConfirmAsync(second, firstDraft.Id, null, default) is null);
    for (var delivery = 0; delivery < 8; delivery++)
    { await incomeProvider.GetRequiredService<ITransactionConfirmedConsumer>().ConsumeAsync(firstEvent, default); }
    Check((await incomeProvider.GetRequiredService<PostgresIncomeRecordStore>().ListAsync(first, firstEvent.Date, firstEvent.Date, true, default)).Count == 1);
    await confirmation.ConfirmAsync(second, other.Draft.Id, "synthetic-other", default);
    var otherEvent = (await confirmations.GetAsync(second, other.Draft.Id, default))?.IntegrationEvent ?? throw new FixtureFailure();
    await expenseProvider.GetRequiredService<ITransactionConfirmedConsumer>().ConsumeAsync(otherEvent, default);
    var expense = await expenseProvider.GetRequiredService<PostgresExpenseRecordStore>().GetAsync(second, otherEvent.TransactionId, default);
    Check(expense?.Amount == 25m && expense.Currency == "EUR");

    stage = "review-confirm and reject-confirm races are serialized";
    var editable = await intake.CreateDraftAsync(first, "synthetic-edit-key", new TransactionIntakeRequest("Paid 25 USD for taxi today"), default);
    var edit = ExpectedRace(() => review.UpdateAsync(first, editable.Draft.Id,
        new TransactionDraftUpdateRequest(0, "expense", 26m, "USD", "expense.transport", null, new DateOnly(2026, 10, 12), "Synthetic note"), default));
    var confirmRace = ExpectedRace(() => confirmation.ConfirmAsync(first, editable.Draft.Id, "synthetic-race", default));
    await Task.WhenAll(edit, confirmRace);
    var edited = await drafts.GetByIdAsync(first, editable.Draft.Id, default) ?? throw new FixtureFailure();
    var editedEvent = (await confirmations.GetAsync(first, editable.Draft.Id, default))?.IntegrationEvent ?? throw new FixtureFailure();
    Check(edited.Status == TransactionDraftStatuses.Confirmed && edited.Amount == editedEvent.Amount && edited.Revision is 2 or 3);
    var rejected = await intake.CreateDraftAsync(first, "synthetic-reject-key", new TransactionIntakeRequest("Received $10 salary today"), default);
    await Task.WhenAll(ExpectedRace(() => review.RejectAsync(first, rejected.Draft.Id, default)),
        ExpectedRace(() => confirmation.ConfirmAsync(first, rejected.Draft.Id, "synthetic-reject-race", default)));
    var terminal = await drafts.GetByIdAsync(first, rejected.Draft.Id, default) ?? throw new FixtureFailure();
    Check(terminal.Status is TransactionDraftStatuses.Confirmed or TransactionDraftStatuses.Rejected);
    Check((await confirmations.GetAsync(first, rejected.Draft.Id, default) is not null) == (terminal.Status == TransactionDraftStatuses.Confirmed));
    var stale = await drafts.ReplaceAsync(first, editable.Draft.Id, 0, edited with { Revision = 1 }, default);
    Check(!stale.Replaced && stale.Draft?.Revision == edited.Revision);

    stage = "publisher failure rolls back draft/index/source/outbox and confirmation claims";
    var before = await RecordCount(admin, "fa_transaction_intake");
    await using (var fault = IntakeProvider(intakeSettings, clock, failCreation: true))
    {
        try
        {
            await fault.GetRequiredService<ITransactionIntakeService>().CreateDraftAsync(first, "synthetic-failed-key",
            new TransactionIntakeRequest("Received $30 salary today"), default); throw new FixtureFailure();
        }
        catch (StorageUnavailableException) { }
    }
    Check(await drafts.GetAsync(first, "synthetic-failed-key", default) is null && await RecordCount(admin, "fa_transaction_intake") == before);
    var retryDraft = await intake.CreateDraftAsync(first, "synthetic-confirm-fail", new TransactionIntakeRequest("Received $40 salary today"), default);
    before = await RecordCount(admin, "fa_transaction_intake");
    await using (var fault = IntakeProvider(intakeSettings, clock, failConfirmation: true))
    {
        try { await fault.GetRequiredService<ITransactionConfirmationService>().ConfirmAsync(first, retryDraft.Draft.Id, "synthetic-fault", default); throw new FixtureFailure(); }
        catch (StorageUnavailableException) { }
    }
    Check((await drafts.GetByIdAsync(first, retryDraft.Draft.Id, default))?.Status == TransactionDraftStatuses.Draft
        && await confirmations.GetAsync(first, retryDraft.Draft.Id, default) is null && await RecordCount(admin, "fa_transaction_intake") == before);
    await confirmation.ConfirmAsync(first, retryDraft.Draft.Id, "synthetic-retry", default);

    stage = "parser waits outside SQL transaction";
    var gate = new GateParser();
    await using (var parsing = IntakeProvider(intakeSettings, clock, parser: gate))
    {
        var pending = parsing.GetRequiredService<ITransactionIntakeService>().CreateDraftAsync(first, "synthetic-parser-key",
            new TransactionIntakeRequest("Synthetic parser input"), default);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try { Check(await drafts.GetByIdAsync(first, firstDraft.Id, default).WaitAsync(TimeSpan.FromSeconds(2)) is not null); }
        finally { gate.Release.TrySetResult(); }
        await pending;
    }

    stage = "bounded source expiry cannot be extended or repaired by replay";
    clock.Now = initial.AddHours(1);
    Check(await sources.GetByReferenceAsync(first, sourceReference, default) is null);
    await using (var extended = IntakeProvider(intakeSettings, clock, sourceHours: 24))
    { Check(await extended.GetRequiredService<PostgresTransactionDraftCreationStore>().GetByReferenceAsync(first, sourceReference, default) is null); }
    var deletedSources = 0;
    for (var pass = 0; pass < 8; pass++)
    {
        var removed = await sources.CleanupExpiredBatchAsync();
        Check(removed is >= 0 and <= 25);
        deletedSources += removed;
        if (removed == 0) { break; }
    }
    Check(deletedSources >= 5);
    before = await RecordCount(admin, "fa_transaction_intake", "source_payloads");
    await intake.CreateDraftAsync(first, key, new TransactionIntakeRequest(input), default);
    Check(await RecordCount(admin, "fa_transaction_intake", "source_payloads") == before
        && await sources.GetByReferenceAsync(first, sourceReference, default) is null);

    stage = "pending-safe bounded outbox retention";
    await using (var old = IntakeProvider(intakeSettings, new MutableClock(initial.AddDays(-40))))
    {
        for (var item = 0; item < 32; item++)
        {
            await old.GetRequiredService<ITransactionIntakeService>().CreateDraftAsync("synthetic-retention",
            "synthetic-retention-" + item, new TransactionIntakeRequest("Received $1 salary today"), default);
        }
    }
    var acknowledged = new HashSet<string>();
    for (var pass = 0; pass < 8 && acknowledged.Count < 32; pass++)
    {
        var batch = await outbox.ReadPendingAsync();
        Check(batch.Count <= 25);
        foreach (var message in batch.Where(value => value.OccurredAtUtc == initial.AddDays(-40)))
        { await outbox.MarkPublishedAsync(message, initial.AddDays(-31)); acknowledged.Add(message.EventId); }
    }
    Check(acknowledged.Count == 32);
    var removedOutbox = 0;
    for (var pass = 0; pass < 8 && removedOutbox < 32; pass++)
    {
        var count = await outbox.CleanupPublishedBatchAsync(initial);
        Check(count is >= 0 and <= 25);
        removedOutbox += count;
    }
    Check(removedOutbox == 32);
    var creationEvent = (await outbox.ReadPendingAsync()).Single(message => message.Creation?.DraftId == firstDraft.Id);
    var confirmationEvent = (await outbox.ReadPendingAsync()).Single(message => message.Confirmation?.DraftId == firstDraft.Id);
    await outbox.MarkPublishedAsync(creationEvent, initial.AddDays(1));
    await outbox.MarkPublishedAsync(confirmationEvent, initial.AddDays(1));
    var originalRemoved = 0;
    for (var pass = 0; pass < 8 && originalRemoved < 2; pass++)
    { originalRemoved += await outbox.CleanupPublishedBatchAsync(initial.AddDays(32)); }
    Check(originalRemoved == 2);
    var remaining = (await outbox.ReadPendingAsync()).Count;
    await intake.CreateDraftAsync(first, key, new TransactionIntakeRequest(input), default);
    await confirmation.ConfirmAsync(first, firstDraft.Id, "synthetic-after-retention", default);
    Check((await outbox.ReadPendingAsync()).Count == remaining);

    stage = "strict malformed-state failures and role isolation";
    for (var pass = 0; pass < 8; pass++)
    {
        var count = await sources.CleanupExpiredBatchAsync();
        Check(count is >= 0 and <= 25);
        if (count == 0) { break; }
    }
    Check(await RecordCount(admin, "fa_transaction_intake", "source_payloads") == 0);
    await database.ExecuteAsync(new[] { "synthetic-corrupt" }, async (records, _) =>
    {
        await records.InsertAsync("drafts", "synthetic-corrupt", "bad", Encoding.UTF8.GetBytes("{\"version\":1,\"value\":{}}"));
        return true;
    });
    try { await drafts.GetByIdAsync("synthetic-corrupt", "bad", default); throw new FixtureFailure(); }
    catch (StorageUnavailableException) { }
    var isolated = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Username = owners[0].RuntimeRole };
    await using (var runtime = new NpgsqlConnection(isolated.ConnectionString))
    {
        await runtime.OpenAsync();
        try { await Sql(runtime, "SELECT * FROM fa_income.records"); throw new FixtureFailure(); }
        catch (PostgresException exception) when (exception.SqlState == "42501") { }
    }
    foreach (var owner in owners)
    {
        await using var raw = new NpgsqlCommand($"SELECT payload FROM {owner.Schema}.records", admin);
        await using var reader = await raw.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { Check(!Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0)).Contains("synthetic", StringComparison.Ordinal)); }
    }
    var finalDraft = await drafts.GetByIdAsync(first, firstDraft.Id, default) ?? throw new FixtureFailure();
    await database.ExecuteAsync(new[] { first }, async (records, _) =>
    {
        await records.InsertAsync("fixture_proof", first, "snapshot", OwnedRecordCodec.Encode(1,
            new SnapshotProof(firstDraft.Id, result.Transaction.TransactionId, sourceReference, Hash(finalDraft), Hash(firstEvent))));
        return true;
    });
    Console.WriteLine("PASS Intake PostgreSQL: exact drafts/indexes/intent, owner and currency isolation, concurrent races, atomic rollback, parser outside SQL, explicit source TTL/no replay resurrection, bounded pending-safe retention and isolated financial consumer deduplication. Live broker remains unqualified.");
    return 0;
}
catch
{
    Console.Error.WriteLine("FAIL Intake PostgreSQL at " + stage + ". Sensitive details suppressed.");
    return 1;
}

static string Setting(string name) => Environment.GetEnvironmentVariable(name) ?? "";
static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
static async Task Sql(NpgsqlConnection connection, string sql)
{ await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
static async Task<long> RecordCount(NpgsqlConnection connection, string schema, string? bucket = null)
{
    await using var command = new NpgsqlCommand($"SELECT count(*) FROM {schema}.records" + (bucket is null ? "" : " WHERE bucket=$1"), connection);
    if (bucket is not null) { command.Parameters.AddWithValue(bucket); }
    return (long)(await command.ExecuteScalarAsync())!;
}
static NativePostgresSettings Settings(string database, int port, string password, byte index) => new()
{
    Database = database,
    Port = port,
    Password = password,
    IndexKey = Convert.ToBase64String(Enumerable.Repeat(index, 32).ToArray()),
    ActiveEncryptionKeyId = "v1",
    EncryptionKeys = new() { ["v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)(index + 1), 32).ToArray()) }
};
static IConfiguration Configuration(string prefix, NativePostgresSettings settings, int sourceHours = 1) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    [$"{prefix}:Storage:Postgres:Database"] = settings.Database,
    [$"{prefix}:Storage:Postgres:Port"] = settings.Port.ToString(),
    [$"{prefix}:Storage:Postgres:Password"] = settings.Password,
    [$"{prefix}:Storage:Postgres:IndexKey"] = settings.IndexKey,
    [$"{prefix}:Storage:Postgres:ActiveEncryptionKeyId"] = settings.ActiveEncryptionKeyId,
    [$"{prefix}:Storage:Postgres:EncryptionKeys:v1"] = settings.EncryptionKeys["v1"],
    [$"{prefix}:Storage:SourcePayloadRetentionHours"] = sourceHours.ToString(System.Globalization.CultureInfo.InvariantCulture)
}).Build();
static ServiceProvider IntakeProvider(NativePostgresSettings settings, MutableClock clock, bool failCreation = false,
    bool failConfirmation = false, ITransactionInputParser? parser = null, int sourceHours = 1)
{
    var services = new ServiceCollection().AddLogging().AddTransactionIntakeApplication()
        .AddTransactionIntakeInfrastructure(Configuration("TransactionIntake", settings, sourceHours), true);
    services.Replace(ServiceDescriptor.Singleton<ITransactionIntakeClock>(clock));
    if (parser is not null) { services.Replace(ServiceDescriptor.Singleton<ITransactionInputParser>(parser)); }
    if (failCreation) { services.Replace(ServiceDescriptor.Singleton<ITransactionDraftCreatedPublisher>(provider => new FaultingCreation(provider.GetRequiredService<PostgresIntakeOutbox>()))); }
    if (failConfirmation) { services.Replace(ServiceDescriptor.Singleton<ITransactionConfirmedPublisher>(provider => new FaultingConfirmation(provider.GetRequiredService<PostgresIntakeOutbox>()))); }
    return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
}
static async Task ExpectedRace<T>(Func<Task<T>> operation)
{
    try { await operation(); }
    catch (Exception exception) when (exception is DraftNotEditableException or DraftMutationConflictException or DraftNotConfirmableException) { }
}
sealed class FixtureFailure : Exception;
sealed record SnapshotProof(string DraftId, string TransactionId, string SourceReference, string DraftHash, string ConfirmationHash);
sealed class MutableClock(DateTimeOffset now) : ITransactionIntakeClock
{ public DateTimeOffset Now { get; set; } = now; public DateTimeOffset UtcNow => Now; }
sealed class FaultingCreation(PostgresIntakeOutbox inner) : ITransactionDraftCreatedPublisher
{ public async Task PublishAsync(TransactionDraftCreatedIntegrationEvent value, CancellationToken token) { await inner.PublishAsync(value, token); throw new StorageUnavailableException(); } }
sealed class FaultingConfirmation(PostgresIntakeOutbox inner) : ITransactionConfirmedPublisher
{ public async Task PublishAsync(TransactionConfirmedIntegrationEvent value, CancellationToken token) { await inner.PublishAsync(value, token); throw new StorageUnavailableException(); } }
sealed class GateParser : ITransactionInputParser
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<ParsedTransactionCandidate> ParseAsync(string input, DateOnly date, CancellationToken token)
    {
        Entered.TrySetResult();
        await Release.Task.WaitAsync(token);
        return new ParsedTransactionCandidate("income", 1m, "USD", "income.salary", null, date, 1m);
    }
}
