using System.Text.Json.Nodes;
using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Audit.Infrastructure;
using FinancialAssistant.Audit.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FinancialAssistant.Audit.Tests;

public sealed class NativeAuditPersistenceTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static AuditPolicy Policy() => new(new[] { "expense-service" },
        new Dictionary<string, int> { ["standard"] = 365, ["regulatory"] = 2555 });

    [Fact]
    public void NativeSourcePreservesLegacyCompatibilityAndRejectsUnsafeFutureOrForeignFacts()
    {
        var source = Source();
        AuditPersistenceValidation.Source(source, Policy(), At);
        AuditPersistenceValidation.Source(Copy(source, source.Payload with { Action = "synthetic.legacy", ActorType = null, ActorIdHash = null }), Policy(), At);
        Assert.Throws<ArgumentException>(() => AuditPersistenceValidation.Source(Copy(source, source.Payload with { Action = "synthetic.legacy" }), Policy(), At));
        Assert.Throws<ArgumentException>(() => AuditPersistenceValidation.Source(Copy(source, source.Payload with { ActorIdHash = "private@example.invalid" }), Policy(), At));
        Assert.Throws<ArgumentException>(() => AuditPersistenceValidation.Source(source, Policy(), At.AddTicks(-1)));
        var offset = new IntegrationEventEnvelope<AuditEventV1>(source.EventId, source.OccurrenceId, source.EventType,
            At.ToOffset(TimeSpan.FromHours(1)), source.Producer, 1, source.CorrelationId, source.CausationId, source.UserIdHash, source.Payload);
        AuditPersistenceValidation.Source(offset, Policy(), At);
        Assert.Equal(TimeSpan.Zero, offset.OccurredAtUtc.Offset);
        Assert.Equal(AuditPersistenceValidation.Fingerprint(source), AuditPersistenceValidation.Fingerprint(offset));
        Assert.Throws<ArgumentException>(() => AuditPersistenceValidation.Utc(At.ToOffset(TimeSpan.FromHours(1))));
    }

    [Fact]
    public void FullSourceFingerprintIncludesOccurrenceCorrelationActorAndRetention()
    {
        var e = Source();
        var changed = new IntegrationEventEnvelope<AuditEventV1>(e.EventId, "synthetic-other-occurrence", e.EventType,
            e.OccurredAtUtc, e.Producer, 1, e.CorrelationId, e.CausationId, e.UserIdHash, e.Payload);
        Assert.NotEqual(AuditPersistenceValidation.Fingerprint(e), AuditPersistenceValidation.Fingerprint(changed));
        Assert.NotEqual(AuditPersistenceValidation.Fingerprint(e), AuditPersistenceValidation.Fingerprint(Copy(e, e.Payload with { Outcome = "failed" })));
        Assert.NotEqual(AuditPersistenceValidation.Fingerprint(e), AuditPersistenceValidation.Fingerprint(Copy(e, e.Payload with { ActorIdHash = new string('b', 64) })));
    }

    [Fact]
    public void OwnedEntryRequiresAllFieldsAndCannotBecomeAnUpdatedOrForeignRecord()
    {
        var e = Source(); var id = AuditPersistenceValidation.AuditId(e.EventId);
        var record = new AuditRecord(id, e.EventId, At, At, e.Producer, e.CorrelationId, e.CausationId, e.UserIdHash,
            "business", "expense.created", "succeeded", "expense", null, "regulatory", At.AddDays(2555), "user", new string('a', 64));
        var value = AuditEntryState.From(record, e.OccurrenceId, false);
        var bytes = OwnedRecordCodec.Encode(1, value);
        Assert.Equal(record, AuditPersistenceValidation.Decode(new DurableRecord(0, bytes)).ToRecord());
        Assert.Throws<StorageUnavailableException>(() => AuditPersistenceValidation.Decode(new DurableRecord(1, bytes)));
        var missing = JsonNode.Parse(bytes)!; Assert.True(missing["value"]!.AsObject().Remove("occurrenceId"));
        Assert.Throws<StorageUnavailableException>(() => AuditPersistenceValidation.Decode(new DurableRecord(0,
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(missing))));
        var extra = JsonNode.Parse(bytes)!; extra["value"]!["amount"] = 123;
        Assert.Throws<StorageUnavailableException>(() => AuditPersistenceValidation.Decode(new DurableRecord(0,
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(extra))));
        Assert.Throws<ArgumentException>(() => AuditPersistenceValidation.Entry(value with { Id = "synthetic-forged" }));
        Assert.Throws<ArgumentException>(() => AuditPersistenceValidation.Entry(value with { LegacyActor = true }));
        foreach (var actor in new string?[] { null, "USER", "unknown" })
        {
            var malformedActor = OwnedRecordCodec.Encode(1, value with { Actor = actor!, ActorHash = actor is null ? null : value.ActorHash });
            Assert.Throws<StorageUnavailableException>(() => AuditPersistenceValidation.Decode(new DurableRecord(0, malformedActor)));
        }
        Assert.Throws<ArgumentException>(() => AuditPersistenceValidation.Entry(value with { ExpiresAtUtc = At.AddDays(3651) }));
    }

    [Fact]
    public void HistoricalMetadataIsIndependentOfCurrentAdmissionButStillStrict()
    {
        var source = Source();
        var revoked = new AuditPolicy(new[] { "mcp-service" }, new Dictionary<string, int> { ["standard"] = 1 });
        Assert.Throws<ArgumentException>(() => AuditPersistenceValidation.Source(source, revoked, At));
        var retentionRevoked = new AuditPolicy(new[] { "expense-service" }, new Dictionary<string, int> { ["standard"] = 1 });
        Assert.Throws<ArgumentException>(() => AuditPersistenceValidation.Source(source, retentionRevoked, At));
        AuditPolicy.ValidateHistoricalMetadata(source.Payload, source.Producer);
        Assert.Throws<ArgumentException>(() => AuditPolicy.ValidateHistoricalMetadata(source.Payload, "mcp-service"));
        Assert.Throws<ArgumentException>(() => AuditPolicy.ValidateHistoricalMetadata(source.Payload with { ActorIdHash = "unsafe" }, source.Producer));
        Assert.Throws<ArgumentException>(() => AuditPolicy.ValidateHistoricalMetadata(source.Payload with { RetentionClass = "unsafe class" }, source.Producer));
    }

    [Fact]
    public void NativeConfigurationCannotFallBackToMemory()
    {
        var values = Configuration(); values.Remove("Audit:Storage:Postgres:Password");
        Assert.ThrowsAny<Exception>(() => new ServiceCollection().AddAuditInfrastructure(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(), true));
    }

    [Fact]
    public async Task NativeCompositionHasSeparateOwnerAndRetentionWorkerButNoLegacyConsumer()
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddAuditInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(Configuration()).Build(), true);
        await using var provider = services.BuildServiceProvider();
        Assert.IsType<PostgresAuditRecordStore>(provider.GetRequiredService<IAuditRecordStore>());
        Assert.Same(provider.GetRequiredService<IAuditRecordStore>(), provider.GetRequiredService<IAuditEventTransaction>());
        Assert.Equal("audit", provider.GetRequiredService<OwnedPostgresStore>().ServiceId);
        Assert.Null(provider.GetService<InMemoryAppendOnlyAuditRecordStore>());
        Assert.Contains(provider.GetServices<IHostedService>(), s => s is AuditRetentionWorker);
        Assert.DoesNotContain(provider.GetServices<IHostedService>(), s => s is RabbitMqAuditEventConsumer);
    }

    internal static Dictionary<string, string?> Configuration() => new()
    {
        ["Audit:AllowedProducers:0"] = "expense-service",
        ["Audit:RetentionDays:standard"] = "365",
        ["Audit:RetentionDays:regulatory"] = "2555",
        ["Audit:Storage:Postgres:Database"] = "synthetic_audit",
        ["Audit:Storage:Postgres:Port"] = "5432",
        ["Audit:Storage:Postgres:Password"] = new string('s', 48),
        ["Audit:Storage:Postgres:IndexKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)31, 32).ToArray()),
        ["Audit:Storage:Postgres:ActiveEncryptionKeyId"] = "v1",
        ["Audit:Storage:Postgres:EncryptionKeys:v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)37, 32).ToArray()),
        ["Audit:Events:Mode"] = "RabbitMq"
    };
    private static IntegrationEventEnvelope<AuditEventV1> Source() => new("synthetic-source", "synthetic-occurrence",
        AuditEventTypes.Recorded, At, "expense-service", 1, "synthetic-correlation", "synthetic-causation", new string('b', 64),
        new AuditEventV1("business", "expense.created", "succeeded", "expense", null, "regulatory", "user", new string('a', 64)));
    private static IntegrationEventEnvelope<AuditEventV1> Copy(IntegrationEventEnvelope<AuditEventV1> e, AuditEventV1 p) =>
        new(e.EventId, e.OccurrenceId, e.EventType, e.OccurredAtUtc, e.Producer, e.SchemaVersion, e.CorrelationId, e.CausationId, e.UserIdHash, p);
}
