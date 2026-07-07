namespace FinancialAssistant.Identity.Application.Messaging;

public sealed record IdentityIntegrationEventEnvelope(
    string EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    DateTimeOffset? PublishedAt,
    string Producer,
    int SchemaVersion,
    string CorrelationId,
    string CausationId,
    string? UserIdHash,
    IReadOnlyDictionary<string, string> Payload,
    IReadOnlyDictionary<string, string>? Metadata = null);
