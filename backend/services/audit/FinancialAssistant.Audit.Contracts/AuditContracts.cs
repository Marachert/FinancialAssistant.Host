using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.Audit.Contracts;

public static class AuditApiRoutes
{
    public const string Dashboard = "/admin/audit";
    public const string ServiceDashboard = "/api/v1/admin/audit";
    public const string InternalEvents = "/internal/audit/events";
}

public static class AuditHeaders
{
    public const string GatewayAuthentication = "X-Gateway-Authentication";
    public const string GatewayRoles = "X-Gateway-Roles";
    public const string ServiceAuthentication = "X-Audit-Authentication";
}

public static class AuditEventTypes
{
    public const string Recorded = "audit.recorded.v1";
    public const int SchemaVersion = 1;
}

public static class AuditDomains
{
    public const string Security = "security";
    public const string Business = "business";
    public const string Ai = "ai";
    public const string Admin = "admin";
    public const string Mcp = "mcp";
}

public sealed record AuditEventV1(
    string Domain,
    string Action,
    string Outcome,
    string ResourceType,
    string? FailureCategory,
    string RetentionClass,
    string? ActorType = null,
    string? ActorIdHash = null);

public sealed record AuditRecordResponse(
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

public sealed record AuditQueryResponse(
    string CorrelationId,
    IReadOnlyList<AuditRecordResponse> Records,
    string DataClassification);

public sealed record AuditAcceptedResponse(string Status, string AuditId);

public sealed record AuditApiErrorResponse(
    string? Title,
    string? Detail,
    int? Status,
    string? Code,
    string? TraceId);

public sealed record AuditServiceInfoResponse(
    string Service,
    string Status,
    string Environment,
    string Storage,
    string DataClassification);

public interface IAuditEventConsumer
{
    Task<string> ConsumeAsync(
        IntegrationEventEnvelope<AuditEventV1> integrationEvent,
        CancellationToken cancellationToken);
}
