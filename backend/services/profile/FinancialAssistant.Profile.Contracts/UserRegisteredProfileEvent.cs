namespace FinancialAssistant.Profile.Contracts;

public sealed record UserRegisteredProfileEvent(
    string UserId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    string CausationId);
