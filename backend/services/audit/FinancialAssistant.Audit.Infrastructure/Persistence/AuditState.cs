using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Audit.Infrastructure.Persistence;

internal sealed record AuditEntryState([property: JsonRequired] string Id,
    [property: JsonRequired] string SourceId, [property: JsonRequired] string OccurrenceId,
    [property: JsonRequired] DateTimeOffset OccurredAtUtc, [property: JsonRequired] DateTimeOffset RecordedAtUtc,
    [property: JsonRequired] string Producer, [property: JsonRequired] string Correlation,
    [property: JsonRequired] string Causation, [property: JsonRequired] string? Subject,
    [property: JsonRequired] string Domain, [property: JsonRequired] string Action,
    [property: JsonRequired] string Outcome, [property: JsonRequired] string Resource,
    [property: JsonRequired] string? Failure, [property: JsonRequired] string Retention,
    [property: JsonRequired] DateTimeOffset ExpiresAtUtc, [property: JsonRequired] string Actor,
    [property: JsonRequired] string? ActorHash, [property: JsonRequired] bool LegacyActor)
{
    public AuditRecord ToRecord() => new(Id, SourceId, OccurredAtUtc, RecordedAtUtc, Producer, Correlation,
        Causation, Subject, Domain, Action, Outcome, Resource, Failure, Retention, ExpiresAtUtc, Actor, ActorHash);

    public static AuditEntryState From(AuditRecord r, string occurrence, bool legacy) => new(r.AuditId,
        r.SourceEventId, occurrence, r.OccurredAtUtc, r.RecordedAtUtc, r.Producer, r.CorrelationId,
        r.CausationId, r.SubjectIdHash, r.Domain, r.Action, r.Outcome, r.ResourceType,
        r.FailureCategory, r.RetentionClass, r.ExpiresAtUtc, r.ActorType, r.ActorIdHash, legacy);
}
internal sealed record AuditReceiptState([property: JsonRequired] string Fingerprint, [property: JsonRequired] string AuditId);
internal sealed record AuditIndexState([property: JsonRequired] string AuditId);
internal sealed record AuditLifecycleState([property: JsonRequired] Dictionary<string, int> MinimumDays,
    [property: JsonRequired] string? CleanupCursor);

public static class AuditPersistenceValidation
{
    internal static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(OwnedRecordCodec.Encode(1, value)));
    internal static string AuditId(string source) => "audit_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    internal static void Utc(DateTimeOffset value)
    { if (value == default || value.Offset != TimeSpan.Zero) { throw new ArgumentException("Audit requires an explicit UTC time."); } }

    public static void Source(IntegrationEventEnvelope<AuditEventV1> source, AuditPolicy policy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(source.Payload);
        if (source.EventType != AuditEventTypes.Recorded || source.SchemaVersion != AuditEventTypes.SchemaVersion)
        { throw new ArgumentException("Unsupported Audit event."); }
        foreach (var id in new[] { source.EventId, source.OccurrenceId, source.CorrelationId, source.CausationId })
        { AuditPolicy.EnsureSafeEnvelopeIdentifier(id, nameof(source)); }
        Utc(source.OccurredAtUtc); Utc(now);
        if (source.OccurredAtUtc > now) { throw new ArgumentException("Audit occurrence cannot be in the future."); }
        AuditPolicy.EnsureSubjectHash(source.UserIdHash); policy.Validate(source.Payload, source.Producer);
        _ = policy.ExpiresAt(source.OccurredAtUtc, source.Payload.RetentionClass);
    }

    internal static void Entry(AuditEntryState value)
    {
        Utc(value.OccurredAtUtc); Utc(value.RecordedAtUtc); Utc(value.ExpiresAtUtc);
        if (value.Id != AuditId(value.SourceId) || value.RecordedAtUtc < value.OccurredAtUtc ||
            value.ExpiresAtUtc <= value.OccurredAtUtc || value.ExpiresAtUtc > value.OccurredAtUtc.AddDays(3650) ||
            value.Actor is not (AuditActorTypes.Anonymous or AuditActorTypes.User or AuditActorTypes.Admin or AuditActorTypes.Service or AuditActorTypes.System))
        { throw new ArgumentException("Invalid immutable Audit entry."); }
        foreach (var id in new[] { value.SourceId, value.OccurrenceId, value.Correlation, value.Causation })
        { AuditPolicy.EnsureSafeEnvelopeIdentifier(id, nameof(value)); }
        AuditPolicy.EnsureSubjectHash(value.Subject);
        AuditPolicy.EnsureSafeIdentifier(value.Producer, nameof(value));
        if (value.LegacyActor && (value.Actor != AuditActorTypes.Service || value.ActorHash is not null))
        { throw new ArgumentException("Invalid legacy Audit actor."); }
        AuditPolicy.ValidateHistoricalMetadata(new AuditEventV1(value.Domain, value.Action, value.Outcome, value.Resource,
            value.Failure, value.Retention, value.LegacyActor ? null : value.Actor, value.ActorHash), value.Producer);
        if (new[] { value.Domain, value.Action, value.Outcome, value.Resource, value.Retention, value.Producer }
            .Any(s => s != s.Trim().ToLowerInvariant()) || value.Failure is { } failure && failure != failure.Trim().ToLowerInvariant())
        { throw new ArgumentException("Audit metadata must be canonical."); }
    }

    internal static AuditEntryState Decode(DurableRecord record)
    {
        try
        {
            if (record.Revision != 0) { throw new StorageUnavailableException(); }
            var value = OwnedRecordCodec.Decode<AuditEntryState>(1, record); Entry(value); return value;
        }
        catch (Exception e) when (e is ArgumentException or NullReferenceException or InvalidOperationException)
        { throw new StorageUnavailableException(); }
    }
}
