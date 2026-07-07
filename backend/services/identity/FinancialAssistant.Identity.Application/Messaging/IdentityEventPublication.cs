namespace FinancialAssistant.Identity.Application.Messaging;

public sealed record IdentityEventPublication(
    string EventName,
    int SchemaVersion,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    string CausationId,
    string? SubjectId,
    IReadOnlyDictionary<string, string> Data);
