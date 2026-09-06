using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.Audit.Application;

public sealed class AuditEventService(
    IAuditRecordStore store,
    AuditPolicy policy,
    TimeProvider timeProvider) : IAuditEventConsumer
{
    public async Task<string> ConsumeAsync(
        IntegrationEventEnvelope<AuditEventV1> integrationEvent,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                integrationEvent.EventType,
                AuditEventTypes.Recorded,
                StringComparison.Ordinal)
            || integrationEvent.SchemaVersion != AuditEventTypes.SchemaVersion)
        {
            throw new ArgumentException("Audit event type or schema version is unsupported.");
        }

        AuditPolicy.EnsureSafeEnvelopeIdentifier(
            integrationEvent.EventId,
            nameof(integrationEvent.EventId));
        AuditPolicy.EnsureSafeEnvelopeIdentifier(
            integrationEvent.OccurrenceId,
            nameof(integrationEvent.OccurrenceId));
        AuditPolicy.EnsureSafeEnvelopeIdentifier(
            integrationEvent.CorrelationId,
            nameof(integrationEvent.CorrelationId));
        AuditPolicy.EnsureSafeEnvelopeIdentifier(
            integrationEvent.CausationId,
            nameof(integrationEvent.CausationId));
        AuditPolicy.EnsureSubjectHash(integrationEvent.UserIdHash);
        policy.Validate(integrationEvent.Payload, integrationEvent.Producer);
        var payload = integrationEvent.Payload;
        var auditId = CreateAuditId(integrationEvent.EventId);
        var record = new AuditRecord(
            auditId,
            integrationEvent.EventId,
            integrationEvent.OccurredAtUtc,
            timeProvider.GetUtcNow(),
            integrationEvent.Producer.Trim().ToLowerInvariant(),
            integrationEvent.CorrelationId,
            integrationEvent.CausationId,
            integrationEvent.UserIdHash,
            payload.Domain.Trim().ToLowerInvariant(),
            payload.Action.Trim().ToLowerInvariant(),
            payload.Outcome.Trim().ToLowerInvariant(),
            payload.ResourceType.Trim().ToLowerInvariant(),
            payload.FailureCategory?.Trim().ToLowerInvariant(),
            payload.RetentionClass.Trim().ToLowerInvariant(),
            policy.ExpiresAt(integrationEvent.OccurredAtUtc, payload.RetentionClass),
            AuditPolicy.NormalizeActorType(payload.ActorType),
            payload.ActorIdHash);
        await store.AppendAsync(record, cancellationToken);
        return auditId;
    }

    public Task<IReadOnlyList<AuditRecord>> FindByCorrelationAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        AuditPolicy.EnsureSafeEnvelopeIdentifier(correlationId, nameof(correlationId));
        return store.FindByCorrelationAsync(
            correlationId,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private static string CreateAuditId(string sourceEventId) =>
        $"audit_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceEventId))).ToLowerInvariant()}";
}
