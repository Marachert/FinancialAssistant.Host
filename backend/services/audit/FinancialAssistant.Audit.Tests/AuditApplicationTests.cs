using System.Text.Json;
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
    public async Task MessageHandler_ConsumesCanonicalSafeEnvelope()
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
        var auditId = await handler.HandleAsync(
            JsonSerializer.SerializeToUtf8Bytes(
                envelope,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CancellationToken.None);
        Assert.StartsWith("audit_", auditId, StringComparison.Ordinal);
        Assert.Single(await store.FindByCorrelationAsync(
            envelope.CorrelationId,
            envelope.OccurredAtUtc,
            CancellationToken.None));
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
            new DateTimeOffset(2028, 8, 23, 12, 0, 0, TimeSpan.Zero));
}
