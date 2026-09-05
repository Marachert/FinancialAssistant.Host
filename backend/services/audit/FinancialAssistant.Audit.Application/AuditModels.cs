namespace FinancialAssistant.Audit.Application;

public sealed record AuditRecord(
    string AuditId,
    string SourceEventId,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset RecordedAtUtc,
    string Producer,
    string CorrelationId,
    string CausationId,
    string? SubjectIdHash,
    string Domain,
    string Action,
    string Outcome,
    string ResourceType,
    string? FailureCategory,
    string RetentionClass,
    DateTimeOffset ExpiresAtUtc,
    string ActorType,
    string? ActorIdHash);

public enum AuditAppendResult
{
    Appended,
    AlreadyPresent
}

public interface IAuditRecordStore
{
    Task<AuditAppendResult> AppendAsync(
        AuditRecord record,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditRecord>> FindByCorrelationAsync(
        string correlationId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);
}
