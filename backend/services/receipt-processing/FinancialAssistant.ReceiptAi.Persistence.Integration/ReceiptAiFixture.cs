using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Contracts;
using FinancialAssistant.AiOrchestration.Domain;
using FinancialAssistant.AiOrchestration.Infrastructure.Prompts;
using FinancialAssistant.AiOrchestration.Infrastructure.Providers;
using FinancialAssistant.AiOrchestration.Infrastructure.Routing;
using FinancialAssistant.AiOrchestration.Infrastructure.Storage;
using FinancialAssistant.AiOrchestration.Infrastructure.Usage;
using FinancialAssistant.AiOrchestration.Infrastructure.Validation;
using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Usage;
using FinancialAssistant.Shared.Persistence;
using Npgsql;

var stage = "isolated fixture validation";
try
{
    if (!OperatingSystem.IsWindows()) { throw new FixtureFailure(); }
    var password = Setting("FA_PERSISTENCE_TEST_PASSWORD");
    var directory = Setting("FA_PERSISTENCE_TEST_DIRECTORY");
    var mode = Setting("FA_PERSISTENCE_TEST_MODE");
    Check(password.Length == 64 && password.All(char.IsAsciiHexDigit) && Path.IsPathFullyQualified(directory)
        && Directory.Exists(directory) && mode is "initial" or "restart");
    Check(int.TryParse(Setting("FA_PERSISTENCE_TEST_PORT"), out var port) && port is >= 49152 and <= 65535);
    var repository = new DirectoryInfo(AppContext.BaseDirectory);
    while (!File.Exists(Path.Combine(repository.FullName, "FinancialAssistant.Backend.sln")))
    { repository = repository.Parent ?? throw new FixtureFailure(); }
    var data = new DirectoryInfo(Path.GetFullPath(directory));
    Check(data.Name == "data" && data.Parent is not null && Guid.TryParseExact(data.Parent.Name, "N", out _)
        && string.Equals(data.Parent.Parent?.FullName, Path.Combine(repository.FullName, ".codex-runtime", "persistence-test"), StringComparison.OrdinalIgnoreCase));
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
    { Check(string.Equals(Path.GetFullPath((string)(await location.ExecuteScalarAsync())!), data.FullName, StringComparison.OrdinalIgnoreCase)); }
    const string databaseName = "fa_test_fin285";
    await using (var fresh = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname NOT IN('postgres','template0','template1','fa_test_fin285') OR (datname='fa_test_fin285' AND $1='initial')", bootstrap))
    { fresh.Parameters.AddWithValue(mode); Check((long)(await fresh.ExecuteScalarAsync())! == 0); }
    var owners = new[] { new StorageIdentity("receipt-processing"), new StorageIdentity("ai-orchestration") };
    if (mode == "initial")
    {
        stage = "isolated service roles";
        await Sql(bootstrap, $"CREATE DATABASE {databaseName}");
        await Sql(bootstrap, $"REVOKE ALL ON DATABASE {databaseName} FROM PUBLIC");
        foreach (var owner in owners)
        { await Sql(bootstrap, $"CREATE ROLE {owner.MigrationRole} LOGIN PASSWORD '{password}'; CREATE ROLE {owner.RuntimeRole} LOGIN PASSWORD '{password}'; GRANT CONNECT ON DATABASE {databaseName} TO {owner.MigrationRole},{owner.RuntimeRole}"); }
    }
    connection.Database = databaseName;
    await using var admin = new NpgsqlConnection(connection.ConnectionString);
    await admin.OpenAsync();
    var receiptSettings = Settings(databaseName, port, password, 1);
    var aiSettings = Settings(databaseName, port, password, 3);
    if (mode == "initial")
    {
        stage = "repeatable service-owned migrations";
        await Sql(admin, "REVOKE ALL ON SCHEMA public FROM PUBLIC");
        foreach (var owner in owners)
        {
            await Sql(admin, $"CREATE SCHEMA {owner.Schema} AUTHORIZATION {owner.MigrationRole}; REVOKE ALL ON SCHEMA {owner.Schema} FROM PUBLIC; GRANT USAGE ON SCHEMA {owner.Schema} TO {owner.RuntimeRole}");
            var settings = owner.ServiceId == "receipt-processing" ? receiptSettings : aiSettings;
            using var keys = settings.CreateProtection();
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(owner.ServiceId), keys);
            await OwnedPostgresStore.MigrateAsync(settings.CreateOptions(owner.ServiceId), keys);
            await Sql(admin, $"GRANT SELECT,INSERT,UPDATE,DELETE ON {owner.Schema}.records TO {owner.RuntimeRole}; GRANT SELECT ON {owner.Schema}.schema_versions,{owner.Schema}.encryption_keys TO {owner.RuntimeRole}");
        }
    }
    using var receiptKeys = receiptSettings.CreateProtection(); using var aiKeys = aiSettings.CreateProtection();
    await using var receiptDb = new OwnedPostgresStore(receiptSettings.CreateOptions("receipt-processing"), receiptKeys);
    await using var aiDb = new OwnedPostgresStore(aiSettings.CreateOptions("ai-orchestration"), aiKeys);
    Check(await receiptDb.IsReadyAsync() && await aiDb.IsReadyAsync());
    var filesPath = Path.Combine(data.Parent!.FullName, "receipt-files");
    using var identity = WindowsIdentity.GetCurrent();
    if (mode == "initial")
    {
        Check(!Directory.Exists(filesPath));
        var security = new DirectorySecurity(); security.SetOwner(identity.User!); security.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { identity.User!.Value, "S-1-5-18", "S-1-5-32-544" }.Distinct())
        {
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid), FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        }
        new DirectoryInfo(filesPath).Create(security);
    }
    var now = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
    var clock = new Clock(now);
    using var objects = new NativeReceiptObjectStore(new NativeReceiptObjectOptions
    {
        RootPath = filesPath,
        ServiceSid = identity.User!.Value,
        RetentionHours = 1,
        IndexKey = receiptSettings.IndexKey,
        ActiveEncryptionKeyId = "v1",
        EncryptionKeys = receiptSettings.EncryptionKeys
    }, clock);
    var context = new ReceiptPersistenceContext(receiptDb);
    var metadata = new PostgresReceiptMetadataStore(context, objects);
    var results = new PostgresOcrProcessingStore(context);
    var outbox = new PostgresReceiptOutbox(context);
    var ids = new Ids();
    var service = new TransactionalReceiptService(context, new ReceiptProcessingService(objects, metadata, results, outbox, clock, ids));
    var aiMetadata = new PostgresAiCallMetadataStore(aiDb);
    var aiUsage = new PostgresAiUsageLimiter(aiDb);
    var journal = new PostgresAiExecutionJournal(aiDb);
    const string ownerId = "synthetic_owner";
    var image = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    var request = new AiCapabilityRequest("transaction.parse", "transaction.parse", "synthetic transient input", 1, ownerId, "request_synthetic");
    var pipelineRequest = request with
    {
        CapabilityName = "fixture.parse",
        PromptName = "fixture.parse",
        UsageSubjectId = "synthetic_ai_pipeline_owner",
        RequestId = "request_pipeline"
    };
    var syntheticAi = new SyntheticAi();
    var aiCore = new AiOrchestrationService(new StaticModelRouter(new[] { new AiModelRoute("fixture.parse", "synthetic", "model") }),
        new InMemoryPromptRegistry(new[] { new PromptDefinition("fixture.parse", 1, "Synthetic fixture instruction",
            """{"type":"object","required":["kind"],"additionalProperties":false,"properties":{"kind":{"type":"string"}}}""") }),
        new RegisteredLlmProviderResolver(new[] { syntheticAi }), new JsonSchemaStructuredOutputValidator(), aiMetadata, clock,
        new CallIds(), new AiUsageCostControlPolicy(10, 10000, 1m, true), aiUsage);
    var aiPipeline = new DurableAiOrchestrationService(journal, aiCore);
    if (mode == "restart")
    {
        stage = "actual database and object restart";
        var replay = await Upload(service, ownerId, "synthetic-key", image);
        Check(replay.Replayed && replay.Receipt.ReceiptId == "receipt_fixture_1");
        var result = await results.GetAsync(ownerId, replay.Receipt.ReceiptId, default);
        Check(result?.IntegrationEvent?.Amount == 1234567890.123456789m && result.Metadata.OcrCompletedPublished);
        Check((await results.GetAsync(ownerId, "receipt_zero", default))?.IntegrationEvent?.Amount == 0m);
        Check((await outbox.ReadPendingAsync()).Count == 0);
        Check(await aiMetadata.GetAsync(ownerId, "call_synthetic") is { Status: AiCallStatus.Succeeded });
        Check(await aiMetadata.GetAsync("other_owner", "call_synthetic") is null);
        Check(await aiUsage.TryAcquireAsync(ownerId, "synthetic", new DateOnly(2026, 10, 19), 1, "usage_first", default));
        Check(!await aiUsage.TryAcquireAsync(ownerId, "synthetic", new DateOnly(2026, 10, 19), 1, "usage_after_restart", default));
        await Recorded(() => journal.ReserveAsync(request, default), "completed");
        await Recorded(() => journal.ReserveAsync(request with { RequestId = "request_crash" }, default), "reserved");
        Check(await aiMetadata.GetAsync(pipelineRequest.UsageSubjectId, "call_execution_1") is { Status: AiCallStatus.Succeeded });
        await Recorded(() => aiPipeline.ExecuteAsync(pipelineRequest, default), "completed");
        await Recorded(() => aiPipeline.ExecuteAsync(pipelineRequest with { RequestId = "request_failed" }, default), "failed");
        Check(syntheticAi.Calls == 0);
        Check(await metadata.GetByIdempotencyKeyAsync(ownerId, "rollback-key", default) is null);
        Console.WriteLine("PASS actual PostgreSQL/process restart: exact Receipt/OCR/index/outbox state, encrypted object identity, committed AI quotas and no uncertain provider replay.");
        return 0;
    }

    stage = "atomic upload and concurrent independent scope replay";
    var first = await Upload(service, ownerId, "synthetic-key", image);
    Check(!first.Replayed && first.Receipt.ReceiptId == "receipt_fixture_1");
    var secondContext = new ReceiptPersistenceContext(receiptDb);
    var second = new TransactionalReceiptService(secondContext, new ReceiptProcessingService(objects,
        new PostgresReceiptMetadataStore(secondContext, objects), new PostgresOcrProcessingStore(secondContext),
        new PostgresReceiptOutbox(secondContext), clock, new Ids()));
    var replays = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Upload(i % 2 == 0 ? service : second, ownerId, "synthetic-key", image)));
    Check(replays.All(r => r.Replayed && r.Receipt.ReceiptId == first.Receipt.ReceiptId));
    var changed = image.Concat(new byte[] { 1 }).ToArray();
    await Fails<ReceiptIdempotencyConflictException>(() => Upload(service, ownerId, "synthetic-key", changed));
    Check(await metadata.GetAsync("other_owner", first.Receipt.ReceiptId, default) is null);
    Check((await outbox.ReadPendingAsync()).Count == 1);
    var file = await metadata.GetAsync(ownerId, first.Receipt.ReceiptId, default) ?? throw new FixtureFailure();
    Check(!file.ReceiptUploadedPublished);
    await Fails<StorageUnavailableException>(() => metadata.MarkReceiptUploadedPublishedAsync(ownerId, file.ReceiptId, file.ReceiptUploadedEventId, default));

    stage = "rollback after encrypted object and outbox writes";
    await Fails<FixtureFailure>(() => context.RunAsync(ownerId, async token =>
    { await Upload(service, ownerId, "rollback-key", image); return await Task.FromException<bool>(new FixtureFailure()); }));
    Check(await metadata.GetByIdempotencyKeyAsync(ownerId, "rollback-key", default) is null);
    Check((await outbox.ReadPendingAsync()).Count == 1);
    await objects.DeleteAsync("receipt_fixture_2");

    stage = "atomic deterministic candidate and publication replay";
    var ocrEvent = new OcrCompletedIntegrationEvent(OcrCompletedIntegrationEvent.Name, "event_completed", file.ReceiptId, ownerId,
        "expense", 1234567890.123456789m, "USD", "expense.food", "Synthetic Store", new DateOnly(2026, 10, 19), 0.75m, new[] { "low_confidence" }, now);
    var audit = new OcrProcessingAuditMetadata(file.ReceiptUploadedEventId, "synthetic", "model", 1, 0.75m, null,
        file.ReceiptUploadedEventId, new OcrProviderUsageMetadata(image.Length, 0, "2026-10"));
    var ocr = new ReceiptOcrMetadata(file.ReceiptId, ownerId, ReceiptProcessingStatuses.OcrCompleted, 0.75m,
        ocrEvent.Ambiguities, audit, now, false);
    await results.StoreIfMissingAsync(ocr, ocrEvent, default);
    var unchanged = await new PostgresOcrProcessingStore(secondContext).StoreIfMissingAsync(ocr, ocrEvent with { Amount = 1m }, default);
    Check(unchanged.IntegrationEvent?.Amount == ocrEvent.Amount);
    var pending = await outbox.ReadPendingAsync(); Check(pending.Count == 2);
    await outbox.PublishAsync(ocrEvent, default); Check((await outbox.ReadPendingAsync()).Count == 2);
    foreach (var intent in pending)
    {
        await outbox.MarkPublishedAsync(intent, now);
        if (intent.Uploaded is not null) { await metadata.MarkReceiptUploadedPublishedAsync(ownerId, file.ReceiptId, intent.Uploaded.EventId, default); }
        else { await results.MarkPublishedAsync(ownerId, file.ReceiptId, intent.Completed!.EventId, default); }
    }
    Check((await outbox.ReadPendingAsync()).Count == 0);

    stage = "zero-valued candidate persists without financial confirmation";
    await objects.StoreAsync("receipt_zero", image, default);
    var zeroFile = file with { ReceiptId = "receipt_zero", ReceiptUploadedEventId = "event_zero_uploaded", ReceiptUploadedPublished = false };
    Check(await metadata.AddAsync("zero-total-key", zeroFile, default));
    var zeroEvent = ocrEvent with { ReceiptId = zeroFile.ReceiptId, EventId = "event_zero_completed", Amount = 0m };
    var zeroMetadata = ocr with
    {
        ReceiptId = zeroFile.ReceiptId,
        Audit = audit with
        { RequestId = zeroFile.ReceiptUploadedEventId, TraceId = zeroFile.ReceiptUploadedEventId }
    };
    await results.StoreIfMissingAsync(zeroMetadata, zeroEvent, default);
    Check((await results.GetAsync(ownerId, zeroFile.ReceiptId, default))?.IntegrationEvent?.Amount == 0m);
    var zeroPending = await outbox.ReadPendingAsync(); Check(zeroPending.Count == 2);
    foreach (var intent in zeroPending)
    {
        await outbox.MarkPublishedAsync(intent, now);
        if (intent.Uploaded is not null) { await metadata.MarkReceiptUploadedPublishedAsync(ownerId, intent.ReceiptId, intent.Uploaded.EventId, default); }
        else { await results.MarkPublishedAsync(ownerId, intent.ReceiptId, intent.Completed!.EventId, default); }
    }
    Check((await outbox.ReadPendingAsync()).Count == 0);

    stage = "durable quota concurrency and immutable AI metadata";
    Check(await aiUsage.TryAcquireAsync(ownerId, "synthetic", new DateOnly(2026, 10, 19), 1, "usage_first", default));
    var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => new PostgresAiUsageLimiter(aiDb).TryAcquireAsync(ownerId, "synthetic",
        new DateOnly(2026, 10, 19), 1, "usage_attempt_" + i, default)));
    Check(attempts.All(value => !value));
    Check(await aiUsage.TryAcquireAsync(ownerId, "synthetic", new DateOnly(2026, 10, 19), 1, "usage_first", default));
    Check(await aiUsage.TryAcquireAsync("other_owner", "synthetic", new DateOnly(2026, 10, 19), 1, "usage_first", default));
    var ocrUsage = new PostgresOcrUsageLimiter(context);
    Check(await ocrUsage.TryAcquireAsync(ownerId, "synthetic", new DateOnly(2026, 10, 19), 1, "ocr_usage", default));
    Check(!await ocrUsage.TryAcquireAsync(ownerId, "synthetic", new DateOnly(2026, 10, 19), 1, "ocr_overflow", default));
    var call = new AiCallMetadata("call_synthetic", "transaction.parse", "transaction.parse", 1, "synthetic", "model", AiCallStatus.Succeeded,
        new AiTokenUsage(10, 2), null, null, now, now, new AiProviderUsageMetadata(100, 1, "2026-10"));
    await aiMetadata.AddAsync(ownerId, call, default); await aiMetadata.AddAsync(ownerId, call, default);
    Check(await aiMetadata.GetAsync("other_owner", call.CallId) is null);
    await Fails<StorageConflictException>(() => aiMetadata.AddAsync(ownerId, call with { TokenUsage = new AiTokenUsage(999, 2) }, default));
    await journal.ReserveAsync(request, default); await journal.CompleteAsync(ownerId, request.RequestId, call.CallId);
    await Recorded(() => journal.ReserveAsync(request, default), "completed");
    await Fails<StorageConflictException>(() => journal.ReserveAsync(request with { Input = "different synthetic input" }, default));
    await journal.ReserveAsync(request with { RequestId = "request_crash" }, default);

    stage = "complete synthetic AI execution and retry guard";
    var competing = new DurableAiOrchestrationService(new PostgresAiExecutionJournal(aiDb), aiCore);
    var executions = await Task.WhenAll(Attempt(aiPipeline, pipelineRequest), Attempt(competing, pipelineRequest));
    Check(executions.Count(value => value) == 1 && syntheticAi.Calls == 1);
    var executed = await aiMetadata.GetAsync(pipelineRequest.UsageSubjectId, "call_execution_1");
    Check(executed is { Status: AiCallStatus.Succeeded, TokenUsage.InputTokens: 10, TokenUsage.OutputTokens: 2 });
    Check(await aiMetadata.GetAsync("other_owner", "call_execution_1") is null);
    await Recorded(() => aiPipeline.ExecuteAsync(pipelineRequest, default), "completed");
    Check(syntheticAi.Calls == 1);
    syntheticAi.FailNext = true;
    await Fails<LlmProviderException>(() => aiPipeline.ExecuteAsync(pipelineRequest with { RequestId = "request_failed" }, default));
    await Recorded(() => aiPipeline.ExecuteAsync(pipelineRequest with { RequestId = "request_failed" }, default), "failed");
    Check(syntheticAi.Calls == 2);
    var otherRequest = pipelineRequest with { UsageSubjectId = "other_owner", RequestId = "request_foreign_call" };
    await journal.ReserveAsync(otherRequest, default);
    await Fails<StorageUnavailableException>(() => journal.CompleteAsync(otherRequest.UsageSubjectId, otherRequest.RequestId, "call_execution_1"));

    stage = "missing object rejection and service-role isolation";
    var missing = file with { ReceiptId = "receipt_missing", ReceiptUploadedEventId = "event_missing" };
    await Fails<StorageUnavailableException>(() => metadata.AddAsync("missing-key", missing, default));
    Check(await metadata.GetAsync(ownerId, missing.ReceiptId, default) is null);
    var isolated = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Username = owners[0].RuntimeRole };
    await using (var runtime = new NpgsqlConnection(isolated.ConnectionString))
    {
        await runtime.OpenAsync();
        try { await Sql(runtime, "SELECT * FROM fa_ai_orchestration.records"); throw new FixtureFailure(); }
        catch (PostgresException exception) when (exception.SqlState == "42501") { }
    }
    foreach (var owner in owners)
    {
        await using var raw = new NpgsqlCommand($"SELECT payload FROM {owner.Schema}.records", admin);
        await using var reader = await raw.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { Check(!Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0)).Contains("synthetic", StringComparison.Ordinal)); }
    }
    Console.WriteLine("PASS Receipt/AI PostgreSQL: atomic upload and candidate intents, owner/role isolation, exact decimals, independent-scope concurrency, rollback, no false delivery acknowledgement, encrypted metadata and durable conservative quota/request replay. No live provider or broker used.");
    return 0;
}
catch
{
    Console.Error.WriteLine("FAIL Receipt/AI PostgreSQL at " + stage + ". Sensitive details suppressed.");
    return 1;
}

static string Setting(string name) => Environment.GetEnvironmentVariable(name) ?? "";
static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
static async Task Sql(NpgsqlConnection connection, string sql)
{ await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
static async Task Fails<T>(Func<Task> operation) where T : Exception
{ try { await operation(); } catch (T) { return; } throw new FixtureFailure(); }
static async Task Recorded(Func<Task> operation, string state)
{ try { await operation(); } catch (AiRequestAlreadyRecordedException exception) { Check(exception.State == state); return; } throw new FixtureFailure(); }
static async Task<bool> Attempt(DurableAiOrchestrationService service, AiCapabilityRequest request)
{
    try { var result = await service.ExecuteAsync(request, default); Check(result.Review.RequiresReview); return true; }
    catch (AiRequestAlreadyRecordedException exception) { Check(exception.State is "reserved" or "completed"); return false; }
}
static Task<ReceiptUploadResult> Upload(IReceiptProcessingService service, string owner, string key, byte[] bytes) =>
    service.UploadAsync(owner, key, "image/png", new MemoryStream(bytes), default);
static NativePostgresSettings Settings(string database, int port, string password, byte index) => new()
{
    Database = database,
    Port = port,
    Password = password,
    IndexKey = Convert.ToBase64String(Enumerable.Repeat(index, 32).ToArray()),
    ActiveEncryptionKeyId = "v1",
    EncryptionKeys = new() { ["v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)(index + 1), 32).ToArray()) }
};
sealed class FixtureFailure : Exception;
sealed class Clock(DateTimeOffset now) : TimeProvider, FinancialAssistant.ReceiptProcessing.Application.Abstractions.IReceiptProcessingClock, IAiOrchestrationClock
{ public override DateTimeOffset GetUtcNow() => now; public DateTimeOffset UtcNow => now; }
sealed class Ids : FinancialAssistant.ReceiptProcessing.Application.Abstractions.IReceiptProcessingIdGenerator
{
    private int receipt;
    private int eventId;
    public string CreateReceiptId() => "receipt_fixture_" + Interlocked.Increment(ref receipt);
    public string CreateEventId() => "event_fixture_" + Interlocked.Increment(ref eventId);
}
sealed class CallIds : IAiCallIdGenerator
{
    private int next;
    public string CreateCallId() => "call_execution_" + Interlocked.Increment(ref next);
}
sealed class SyntheticAi : ILlmProvider
{
    private int calls;
    public string Name => "synthetic";
    public int Calls => calls;
    public bool FailNext { get; set; }
    public Task<LlmProviderResponse> CompleteAsync(LlmProviderRequest request, CancellationToken token)
    {
        Interlocked.Increment(ref calls);
        if (FailNext) { FailNext = false; throw new LlmProviderException(Name, "provider_failure", false); }
        return Task.FromResult(new LlmProviderResponse("""{"kind":"expense"}""", 10, 2));
    }
}
