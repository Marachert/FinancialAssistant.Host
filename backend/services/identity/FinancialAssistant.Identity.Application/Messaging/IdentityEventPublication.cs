namespace FinancialAssistant.Identity.Application.Messaging;

public sealed record IdentityEventPublication(
    string EventName,
    string EventVersion,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId);
