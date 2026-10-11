using System.Text;
using System.Text.Json;
using FinancialAssistant.Identity.Application;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Messaging;
using FinancialAssistant.Identity.Application.Phone;
using FinancialAssistant.Identity.Application.Providers;
using FinancialAssistant.Identity.Application.Sessions;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;
using FinancialAssistant.Identity.Infrastructure;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Identity.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

// A console-only synthetic database fixture, not an API host.
var stage = "owned fixture validation";
try
{
    var password = Setting("FA_PERSISTENCE_TEST_PASSWORD");
    var directory = Environment.GetEnvironmentVariable("FA_PERSISTENCE_TEST_DIRECTORY") ?? "";
    var mode = Environment.GetEnvironmentVariable("FA_PERSISTENCE_TEST_MODE");
    if (password.Length != 64 || !password.All(char.IsAsciiHexDigit) || !Path.IsPathFullyQualified(directory)
        || !Directory.Exists(directory) || mode is not ("initial" or "restart")
        || !int.TryParse(Environment.GetEnvironmentVariable("FA_PERSISTENCE_TEST_PORT"), out var port) || port is < 49152 or > 65535)
    { throw new FixtureFailure(); }
    var repository = new DirectoryInfo(AppContext.BaseDirectory);
    while (!File.Exists(Path.Combine(repository.FullName, "FinancialAssistant.Backend.sln")))
    { repository = repository.Parent ?? throw new FixtureFailure(); }
    var data = new DirectoryInfo(Path.GetFullPath(directory));
    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
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
    const string database = "fa_test_fin282";
    await using (var fresh = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin282') OR (datname='fa_test_fin282' AND $1='initial')", bootstrap))
    { fresh.Parameters.AddWithValue(mode); Check((long)(await fresh.ExecuteScalarAsync())! == 0); }
    var storage = new StorageIdentity("identity");
    if (mode == "initial")
    {
        stage = "isolated provisioning and migration";
        await Sql(bootstrap, $"CREATE DATABASE {database}");
        await Sql(bootstrap, $"REVOKE ALL ON DATABASE {database} FROM PUBLIC; CREATE ROLE {storage.MigrationRole} LOGIN PASSWORD '{password}'; CREATE ROLE {storage.RuntimeRole} LOGIN PASSWORD '{password}'; GRANT CONNECT ON DATABASE {database} TO {storage.MigrationRole},{storage.RuntimeRole}");
    }
    connection.Database = database;
    await using var admin = new NpgsqlConnection(connection.ConnectionString);
    await admin.OpenAsync();
    var options = Options(database, port, password);
    options.Storage.Cleanup.RemovedCredentialRetentionDays = 1;
    options.Storage.Cleanup.RemovedProviderLinkRetentionDays = 1;
    options.Storage.Cleanup.DeletedAccountRetentionDays = 1;
    using var protection = NativeIdentityPersistence.CreateProtection(options.Storage.Postgres);
    if (mode == "initial")
    {
        await Sql(admin, $"REVOKE ALL ON SCHEMA public FROM PUBLIC; CREATE SCHEMA {storage.Schema} AUTHORIZATION {storage.MigrationRole}; REVOKE ALL ON SCHEMA {storage.Schema} FROM PUBLIC; GRANT USAGE ON SCHEMA {storage.Schema} TO {storage.RuntimeRole}");
        await OwnedPostgresStore.MigrateAsync(NativeIdentityPersistence.CreateOptions(options.Storage.Postgres), protection);
        await OwnedPostgresStore.MigrateAsync(NativeIdentityPersistence.CreateOptions(options.Storage.Postgres), protection);
        await Sql(admin, $"GRANT SELECT,INSERT,UPDATE,DELETE ON {storage.Schema}.records TO {storage.RuntimeRole}; GRANT SELECT ON {storage.Schema}.schema_versions,{storage.Schema}.encryption_keys TO {storage.RuntimeRole}");
    }
    await using var provider = Provider(options);
    var accounts = provider.GetRequiredService<IIdentityAccountStore>();
    var sessions = provider.GetRequiredService<IIdentitySessionStore>();
    var phone = provider.GetRequiredService<IPhoneVerificationChallengeStore>();
    var outbox = provider.GetRequiredService<IIdentityEventOutbox>();
    var context = provider.GetRequiredService<IdentityPersistenceContext>();
    var now = new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
    const string retainedId = "11111111111111111111111111111111";
    const string phoneId = "22222222222222222222222222222222";
    const string sessionId = "33333333333333333333333333333333";
    Check(await provider.GetRequiredService<OwnedPostgresStore>().IsReadyAsync());
    if (mode == "restart")
    {
        stage = "actual crash restart preserves identity state";
        var account = await accounts.FindAccountByIdAsync(retainedId);
        Check(account is not null && account.Id == retainedId && account.CreatedAtUtc == now
            && account.Status == IdentityAccountStatus.Disabled && account.Roles.SequenceEqual(new[] { "user", "auditor" }));
        Check(await accounts.FindAccountByIdAsync("44444444444444444444444444444444") is null);
        Check((await sessions.FindByIdAsync(sessionId))?.Status == IdentitySessionStatus.Revoked);
        Check((await phone.FindAsync(phoneId))?.Status == PhoneVerificationChallengeStatus.Locked);
        Check((await accounts.FindCredentialByLookupHashAsync(Hash('a')))?.SecretHash == "synthetic_hash");
        Check((await outbox.ReadPendingAsync(100, now.AddYears(1))).Count > 0);
        Console.WriteLine("PASS identity actual crash restart: accounts/roles/credential/session family/challenge/outbox survive; rolled-back state absent.");
        return 0;
    }

    stage = "account rehydration and unique concurrent registration";
    var retained = IdentityAccount.Rehydrate(retainedId, IdentityAccountStatus.Disabled, ["user", "auditor"], now);
    Check(await accounts.TryCreateAsync(retained, Credential(retained, Hash('a'), now)));
    var competingAccounts = Enumerable.Range(0, 4).Select(_ => IdentityAccount.Create(now)).ToArray();
    var results = await Task.WhenAll(competingAccounts.Select(account => accounts.TryCreateAsync(account, Credential(account, Hash('b'), now))));
    Check(results.Count(result => result) == 1);
    for (var i = 0; i < results.Length; i++)
    { Check((await accounts.FindAccountByIdAsync(competingAccounts[i].Id) is not null) == results[i]); }
    var active = competingAccounts[Array.IndexOf(results, true)];

    stage = "atomic registration session and outbox through native composition";
    using (var scope = provider.CreateScope())
    {
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var result = await auth.RegisterAsync(new RegisterAccountRequest("native-fixture@example.invalid", "Synthetic-Only-Password-123!", new IdentityClientContext("synthetic", "web", "0.0-test")), null, "synthetic-correlation");
        Check(result.IsSuccess && result.Value is not null);
        Check(await accounts.FindAccountByIdAsync(result.Value!.User.UserId) is not null);
        Check(await sessions.FindByIdAsync(result.Value.User.SessionId) is not null);
        var events = await outbox.ReadPendingAsync(100, DateTimeOffset.UtcNow.AddDays(1));
        Check(events.Any(message => message.Envelope.EventType == "user.registered.v1"));
    }
    stage = "atomic rollback includes account credential and outbox";
    var rollback = IdentityAccount.Rehydrate("44444444444444444444444444444444", IdentityAccountStatus.Active, ["user"], now);
    try
    {
        await context.RunAsync<bool>(async token =>
        {
            Check(await accounts.TryCreateAsync(rollback, Credential(rollback, Hash('c'), now), token));
            await outbox.EnqueueAsync(Envelope("55555555-5555-5555-5555-555555555555", now), token);
            throw new FixtureFailure();
        });
        throw new InvalidOperationException();
    }
    catch (FixtureFailure) { }
    Check(await accounts.FindAccountByIdAsync(rollback.Id) is null);
    Check(await accounts.FindCredentialByLookupHashAsync(Hash('c')) is null);
    Check(!(await outbox.ReadPendingAsync(100, DateTimeOffset.UtcNow.AddYears(1))).Any(message => message.Envelope.EventId == "55555555-5555-5555-5555-555555555555"));

    stage = "real service outbox failure rolls back account credential and initial session";
    await using (var faulty = Provider(options, failOutbox: true))
    using (var scope = faulty.CreateScope())
    {
        var result = await scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>().RegisterAsync(
            new RegisterAccountRequest("rollback-fixture@example.invalid", "Synthetic-Only-Password-123!", new IdentityClientContext("synthetic", "web", "0.0-test")), null, "outbox-failure-fixture");
        Check(!result.IsSuccess && result.Failure?.Kind == IdentityFailureKind.ServiceUnavailable);
        var lookup = faulty.GetRequiredService<IEmailLookupHasher>().Hash("rollback-fixture@example.invalid");
        Check(await accounts.FindCredentialByLookupHashAsync(lookup) is null);
        Check(!(await outbox.ReadPendingAsync(100, DateTimeOffset.UtcNow.AddYears(1))).Any(message => message.Envelope.CorrelationId == "outbox-failure-fixture"));
    }

    stage = "unique federated link and preserved authentication timestamps";
    var federated = Enumerable.Range(0, 4).Select(_ => IdentityAccount.Create(now)).ToArray();
    var links = await Task.WhenAll(federated.Select(account => provider.GetRequiredService<IIdentityFederatedAccountStore>()
        .TryCreateAccountWithProviderLinkAsync(account, new IdentityProviderLinkRecord(Guid.NewGuid().ToString("N"), account.Id, "google", Hash('d'), null, now, now))));
    Check(links.Count(result => result) == 1);
    var federatedStore = provider.GetRequiredService<IIdentityFederatedAccountStore>();
    await federatedStore.TouchProviderLinkAsync("google", Hash('d'), now.AddMinutes(1));
    Check((await federatedStore.FindProviderLinkAsync("google", Hash('d')))?.LastAuthenticatedAtUtc == now.AddMinutes(1));

    stage = "concurrent rotation reuse revokes family and denies foreign owner";
    var initial = Session(sessionId, active.Id, Hash('e'), Hash('f'), now);
    Check(await sessions.TryCreateAsync(initial));
    var replacements = Enumerable.Range(0, 2).Select(_ => initial with { Id = Guid.NewGuid().ToString("N"), RefreshTokenHash = Hash('1') }).ToArray();
    var rotations = await Task.WhenAll(replacements.Select(replacement => sessions.RotateAsync(sessionId, Hash('f'), replacement, now.AddMinutes(1))));
    Check(rotations.Count(result => result == SessionRotationStoreResult.Success) == 1 && rotations.Count(result => result == SessionRotationStoreResult.ReuseDetected) == 1);
    Check((await sessions.FindByIdAsync(sessionId))?.Status == IdentitySessionStatus.Revoked);
    Check(await sessions.RevokeAsync(sessionId, retainedId, Hash('f'), now.AddMinutes(2)) == SessionRevocationStoreResult.InvalidSecret);
    Check(!await sessions.TryCreateAsync(initial with { Id = Guid.NewGuid().ToString("N"), AccountId = retainedId }));

    stage = "durable phone cooldown rate attempt lock and replay";
    var policy = new PhoneVerificationPolicy(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(30), TimeSpan.FromHours(1), 2, 2, 3, 6);
    var challenge = new PhoneVerificationChallengeRecord(phoneId, Hash('2'), Hash('3'), "sign_in", null, now, now.AddMinutes(10), now.AddSeconds(30), 0, PhoneVerificationChallengeStatus.PendingDispatch);
    Check((await phone.TryReserveAsync(challenge, policy)).Reserved);
    Check(!(await phone.TryReserveAsync(challenge with { Id = Guid.NewGuid().ToString("N") }, policy)).Reserved);
    Check(await phone.TryActivateAsync(phoneId, "synthetic-provider-ref", now));
    Check(!(await phone.TryReserveAsync(challenge with { Id = Guid.NewGuid().ToString("N"), CreatedAtUtc = now.AddSeconds(1) }, policy)).Reserved);
    Check(!(await phone.RecordRejectedAttemptAsync(phoneId, 2)).Locked);
    Check((await phone.RecordRejectedAttemptAsync(phoneId, 2)).Locked);
    Check(!await phone.TryCompleteAsync(phoneId, now.AddMinutes(1)));
    await phone.CancelAsync(phoneId);
    Check((await phone.FindAsync(phoneId))?.Status == PhoneVerificationChallengeStatus.Locked);

    stage = "bounded account deletion and replay-safe retention";
    var maintenance = provider.GetRequiredService<IdentityPersistenceMaintenance>();
    var deleted = IdentityAccount.Create(now);
    Check(await accounts.TryCreateAsync(deleted, Credential(deleted, Hash('5'), now)));
    var deletedSession = Session(Guid.NewGuid().ToString("N"), deleted.Id, Hash('0'), Hash('a'), now);
    Check(await sessions.TryCreateAsync(deletedSession));
    Check(!await maintenance.DeleteAccountBatchAsync(deleted.Id, now));
    Check((await accounts.FindAccountByIdAsync(deleted.Id))?.CanAuthenticate == false);
    Check(await accounts.FindCredentialByLookupHashAsync(Hash('5')) is not null);
    for (var batch = 0; batch < 20 && !await maintenance.DeleteAccountBatchAsync(deleted.Id, now.AddDays(2)); batch++) { }
    Check((await accounts.FindAccountByIdAsync(deleted.Id))?.Status == IdentityAccountStatus.Deleted);
    Check(await accounts.FindCredentialByLookupHashAsync(Hash('5')) is null);
    Check(await sessions.FindByIdAsync(deletedSession.Id) is null);
    var oldSession = Session(Guid.NewGuid().ToString("N"), active.Id, Hash('6'), Hash('7'), now.AddDays(-100));
    Check(await sessions.TryCreateAsync(oldSession));
    var oldPhone = challenge with { Id = Guid.NewGuid().ToString("N"), PhoneSubjectHash = Hash('8'), ClientInstanceHash = Hash('9'), CreatedAtUtc = now.AddDays(-100), ExpiresAtUtc = now.AddDays(-100).AddMinutes(10), ResendAvailableAtUtc = now.AddDays(-100).AddSeconds(30) };
    Check((await phone.TryReserveAsync(oldPhone, policy)).Reserved);
    await outbox.EnqueueAsync(Envelope("66666666-6666-6666-6666-666666666666", now.AddDays(-100)));
    await outbox.MarkPublishedAsync("66666666-6666-6666-6666-666666666666", now.AddDays(-100));
    await outbox.EnqueueAsync(Envelope("77777777-7777-7777-7777-777777777777", now.AddDays(-100)));
    for (var batch = 0; batch < 14; batch++) { await maintenance.CleanupBatchAsync(now.AddDays(4)); }
    Check(await sessions.FindByIdAsync(oldSession.Id) is null);
    Check((await sessions.FindByIdAsync(sessionId))?.Status == IdentitySessionStatus.Revoked);
    Check(await phone.FindAsync(oldPhone.Id) is null);
    Check((await phone.FindAsync(phoneId))?.Status == PhoneVerificationChallengeStatus.Locked);
    Check(await accounts.FindAccountByIdAsync(deleted.Id) is null);
    Check((await outbox.ReadPendingAsync(100, now.AddYears(1))).Any(message => message.Envelope.EventId == "77777777-7777-7777-7777-777777777777"));

    stage = "owned keyset pages and encrypted-at-rest content";
    await context.WithRecordsAsync(async (records, _) =>
    {
        Check((await records.ReadPageAsync("accounts", "foreign_owner")).Count == 0);
        var first = await records.ReadPageAsync("accounts", "identity", pageSize: 1);
        Check(first.Count == 1);
        var next = await records.ReadPageAsync("accounts", "identity", first[0].Cursor, 1);
        Check(next.Count == 1 && first[0].Cursor != next[0].Cursor);
        return true;
    });
    stage = "keyset deletion cannot authorize a foreign owner";
    var cursor = await context.WithRecordsAsync(async (records, _) => (await records.ReadPageAsync("accounts", "identity", pageSize: 1))[0]);
    try
    {
        await context.WithRecordsAsync((records, _) => records.DeletePageItemAsync("accounts", "foreign_owner", cursor.Cursor, cursor.Revision));
        throw new FixtureFailure();
    }
    catch (StorageConflictException) { }
    Check(await accounts.FindAccountByIdAsync(retainedId) is not null);
    stage = "unknown DTO version fails closed without exposing contents";
    const string invalidId = "88888888888888888888888888888888";
    await context.WithRecordsAsync((records, _) => records.InsertAsync("accounts", "identity", invalidId,
        Encoding.UTF8.GetBytes("{\"version\":99,\"value\":{\"id\":\"synthetic_invalid\"}}")));
    try { await accounts.FindAccountByIdAsync(invalidId); throw new FixtureFailure(); }
    catch (StorageUnavailableException exception) { Check(exception.InnerException is null && !exception.Message.Contains("synthetic_invalid", StringComparison.Ordinal)); }
    await context.WithRecordsAsync(async (records, _) =>
    {
        var invalid = await records.GetAsync("accounts", "identity", invalidId) ?? throw new FixtureFailure();
        return await records.DeleteAsync("accounts", "identity", invalidId, invalid.Revision);
    });
    await using (var raw = new NpgsqlCommand("SELECT payload FROM fa_identity.records", admin))
    await using (var reader = await raw.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            var bytes = Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0));
            Check(!bytes.Contains("synthetic", StringComparison.Ordinal) && !bytes.Contains("example.invalid", StringComparison.Ordinal));
        }
    }
    Console.WriteLine("PASS identity PostgreSQL: native composition, registration/federation uniqueness, service outbox failure rollback, rotation/reuse/owner isolation, phone cooldown/attempt replay, bounded retention/deletion, owner-scoped keyset pages and encrypted storage.");
    return 0;
}
catch
{
    Console.Error.WriteLine("FAIL identity PostgreSQL at " + stage + ". Credential and payload details suppressed.");
    return 1;
}

static IdentityServiceOptions Options(string database, int port, string password) => new()
{
    Storage = new()
    {
        Provider = "PostgreSQL",
        Postgres = new()
        {
            Database = database,
            Port = port,
            Password = password,
            IndexKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
            ActiveEncryptionKeyId = "v1",
            EncryptionKeys = new() { ["v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray()) }
        }
    },
    Authentication = new() { RuntimeAdapter = "NativePostgres", LookupHmacKey = new string('l', 64), RefreshTokenHashKey = new string('r', 64), AccessTokenSigningKey = new string('s', 64) },
    Providers = new() { IdentifierHmacKey = new string('i', 64) },
    Events = new() { Mode = "DurableOutboxPendingTransport", UserIdHmacKey = new string('e', 64) }
};
static ServiceProvider Provider(IdentityServiceOptions options, bool failOutbox = false)
{
    using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new { Identity = options }));
    var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
    var services = new ServiceCollection();
    services.AddLogging(); services.AddRouting(); services.AddIdentityApplication(); services.AddIdentityInfrastructure(configuration, true);
    if (failOutbox) { services.Replace(ServiceDescriptor.Singleton<IIdentityEventOutbox>(provider => new FaultingOutbox(new PostgresIdentityEventOutbox(provider.GetRequiredService<IdentityPersistenceContext>())))); }
    return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
static string Hash(char character) => new(character, 64);
static string Setting(string name) => Environment.GetEnvironmentVariable(name) ?? "";
static EmailCredentialRecord Credential(IdentityAccount account, string lookup, DateTimeOffset now) =>
    new(Guid.NewGuid().ToString("N"), account.Id, lookup, "synthetic_hash", "synthetic", "synthetic", now);
static IdentitySessionRecord Session(string id, string account, string family, string secret, DateTimeOffset now) =>
    new(id, account, family, secret, Hash('4'), IdentitySessionStatus.Active, "email_password", now, now.AddMinutes(15), now.AddDays(30));
static IdentityIntegrationEventEnvelope Envelope(string id, DateTimeOffset now) =>
    new(id, "user.registered.v1", now, null, "financial-assistant-identity-service", 1, "synthetic", "synthetic", null, new Dictionary<string, string>());
static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
static async Task Sql(NpgsqlConnection connection, string sql)
{ await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
sealed class FixtureFailure : Exception;
sealed class FaultingOutbox(IIdentityEventOutbox inner) : IIdentityEventOutbox
{
    public async Task EnqueueAsync(IdentityIntegrationEventEnvelope envelope, CancellationToken token = default)
    { await inner.EnqueueAsync(envelope, token); throw new StorageUnavailableException(); }
    public Task<IReadOnlyList<IdentityOutboxMessage>> ReadPendingAsync(int count, DateTimeOffset now, CancellationToken token = default) => inner.ReadPendingAsync(count, now, token);
    public Task MarkPublishedAsync(string id, DateTimeOffset now, CancellationToken token = default) => inner.MarkPublishedAsync(id, now, token);
    public Task MarkFailedAsync(string id, string code, DateTimeOffset next, CancellationToken token = default) => inner.MarkFailedAsync(id, code, next, token);
}
