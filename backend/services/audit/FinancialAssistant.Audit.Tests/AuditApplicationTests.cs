using System.Text.Json;
using System.Text.Json.Nodes;
using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Audit.Infrastructure;
using Xunit;

namespace FinancialAssistant.Audit.Tests;

public sealed class AuditApplicationTests
{
    [Fact]
    public async Task Append_IsIdempotentAndRejectsReplacement()
    {
        var store = new InMemoryAppendOnlyAuditRecordStore();
        var record = CreateRecord("event-one", "audit-one");
        Assert.Equal(AuditAppendResult.Appended, await store.AppendAsync(record, CancellationToken.None));
        Assert.Equal(AuditAppendResult.AlreadyPresent, await store.AppendAsync(record, CancellationToken.None));
        Assert.Equal(
            AuditAppendResult.AlreadyPresent,
            await store.AppendAsync(
                record with { RecordedAtUtc = record.RecordedAtUtc.AddSeconds(1) },
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AppendAsync(
            record with { AuditId = "audit-replacement" },
            CancellationToken.None));
    }

    [Fact]
    public async Task Query_ExcludesRecordsAfterPolicyRetentionExpires()
    {
        var store = new InMemoryAppendOnlyAuditRecordStore();
        var record = CreateRecord("event-expiring", "audit-expiring") with
        {
            ExpiresAtUtc = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero)
        };
        await store.AppendAsync(record, CancellationToken.None);
        Assert.Single(await store.FindByCorrelationAsync(
            record.CorrelationId,
            record.ExpiresAtUtc.AddTicks(-1),
            CancellationToken.None));
        Assert.Empty(await store.FindByCorrelationAsync(
            record.CorrelationId,
            record.ExpiresAtUtc,
            CancellationToken.None));
    }

    [Fact]
    public async Task MessageHandler_ConsumesLegacyEnvelopeWithoutActorFields()
    {
        var store = new InMemoryAppendOnlyAuditRecordStore();
        var service = new AuditEventService(
            store,
            new AuditPolicy(
                ["identity-service"],
                new Dictionary<string, int> { ["security"] = 730 }),
            TimeProvider.System);
        var handler = new AuditEventMessageHandler(service);
        var envelope = AuditEndpointTests.CreateEnvelope("event-message", "trace-message");
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var serialized = JsonNode.Parse(JsonSerializer.Serialize(envelope, serializerOptions));
        var payload = Assert.IsType<JsonObject>(serialized?["payload"]);
        Assert.True(payload.Remove("actorType"));
        Assert.True(payload.Remove("actorIdHash"));
        var auditId = await handler.HandleAsync(
            JsonSerializer.SerializeToUtf8Bytes(serialized, serializerOptions),
            CancellationToken.None);
        Assert.StartsWith("audit_", auditId, StringComparison.Ordinal);
        var record = Assert.Single(await store.FindByCorrelationAsync(
            envelope.CorrelationId,
            envelope.OccurredAtUtc,
            CancellationToken.None));
        Assert.Equal(AuditActorTypes.Service, record.ActorType);
        Assert.Null(record.ActorIdHash);
    }

    [Fact]
    public async Task Consumer_RejectsEmailLikeCorrelationIdentifier()
    {
        var service = new AuditEventService(
            new InMemoryAppendOnlyAuditRecordStore(),
            new AuditPolicy(
                ["identity-service"],
                new Dictionary<string, int> { ["security"] = 730 }),
            TimeProvider.System);
        var envelope = AuditEndpointTests.CreateEnvelope("event-unsafe", "person@example.com");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConsumeAsync(envelope, CancellationToken.None));
    }

    [Fact]
    public void Catalog_CoversEveryRequiredSensitiveOperationFamily()
    {
        var actions = AuditEventCatalog.Definitions
            .Select(definition => definition.Action)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(AuditActions.ProfileUpdated, actions);
        Assert.Contains(AuditActions.ProfilePreferencesUpdated, actions);
        Assert.Contains(AuditActions.ProfileConsentUpdated, actions);
        Assert.Contains(AuditActions.IncomeCreated, actions);
        Assert.Contains(AuditActions.IncomeUpdated, actions);
        Assert.Contains(AuditActions.IncomeArchived, actions);
        Assert.Contains(AuditActions.IncomeRestored, actions);
        Assert.Contains(AuditActions.ExpenseCreated, actions);
        Assert.Contains(AuditActions.ExpenseUpdated, actions);
        Assert.Contains(AuditActions.ExpenseArchived, actions);
        Assert.Contains(AuditActions.ExpenseRestored, actions);
        Assert.Contains(AuditActions.DraftConfirmed, actions);
        Assert.Contains(AuditActions.AuthenticationSucceeded, actions);
        Assert.Contains(AuditActions.AuthenticationFailed, actions);
        Assert.Contains(AuditActions.SessionCreated, actions);
        Assert.Contains(AuditActions.SessionRefreshed, actions);
        Assert.Contains(AuditActions.SessionRevoked, actions);
        Assert.Contains(AuditActions.AdminAuditViewed, actions);
        Assert.Contains(AuditActions.AdminMonitoringViewed, actions);
        Assert.Contains(AuditActions.AdminActionExecuted, actions);
        Assert.All(AuditEventCatalog.Definitions, definition =>
        {
            Assert.NotEmpty(definition.Producers);
            Assert.NotEmpty(definition.ActorTypes);
        });
    }

    [Fact]
    public void Policy_RejectsUncataloguedAndMismatchedSensitiveEvents()
    {
        var policy = CreatePolicy();
        var valid = new AuditEventV1(
            AuditDomains.Business,
            AuditActions.IncomeCreated,
            AuditOutcomes.Succeeded,
            AuditResourceTypes.Income,
            null,
            AuditRetentionClasses.Regulatory,
            AuditActorTypes.User,
            new string('c', 64));

        policy.Validate(valid, "income-service");
        Assert.Throws<ArgumentException>(() => policy.Validate(
            valid with { Action = "income.exported" },
            "income-service"));
        Assert.Throws<ArgumentException>(() => policy.Validate(
            valid with { RetentionClass = AuditRetentionClasses.Standard },
            "income-service"));
        Assert.Throws<ArgumentException>(() => policy.Validate(valid, "expense-service"));
    }

    [Fact]
    public void Policy_RequiresPseudonymousHashForHumanActors()
    {
        var policy = CreatePolicy();
        var payload = new AuditEventV1(
            AuditDomains.Admin,
            AuditActions.AdminAuditViewed,
            AuditOutcomes.Succeeded,
            AuditResourceTypes.AuditTrail,
            null,
            AuditRetentionClasses.Security,
            AuditActorTypes.Admin);

        Assert.Throws<ArgumentException>(() => policy.Validate(payload, "audit-service"));
        policy.Validate(payload with { ActorIdHash = new string('d', 64) }, "audit-service");
        Assert.Throws<ArgumentException>(() => policy.Validate(
            payload with { ActorIdHash = "administrator@example.com" },
            "audit-service"));
    }

    [Fact]
    public void Policy_PreservesLegacyMcpServiceEvents()
    {
        var policy = CreatePolicy();
        policy.Validate(
            new AuditEventV1(
                AuditDomains.Mcp,
                "tool.system_health",
                AuditOutcomes.Succeeded,
                AuditResourceTypes.McpTool,
                null,
                AuditRetentionClasses.Standard),
            "mcp-service");
    }

    private static AuditPolicy CreatePolicy() =>
        new(
            [
                "identity-service",
                "profile-service",
                "transaction-intake-service",
                "income-service",
                "expense-service",
                "monitoring-service",
                "audit-service",
                "mcp-service"
            ],
            new Dictionary<string, int>
            {
                [AuditRetentionClasses.Standard] = 365,
                [AuditRetentionClasses.Security] = 730,
                [AuditRetentionClasses.Regulatory] = 2555
            });

    private static AuditRecord CreateRecord(string sourceEventId, string auditId) =>
        new(
            auditId,
            sourceEventId,
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 24, 12, 0, 1, TimeSpan.Zero),
            "identity-service",
            "trace-safe",
            "cause-safe",
            new string('b', 64),
            "security",
            "session.revoked",
            "succeeded",
            "session",
            null,
            "security",
            new DateTimeOffset(2028, 8, 23, 12, 0, 0, TimeSpan.Zero),
            AuditActorTypes.Service,
            null);
}
