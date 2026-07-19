namespace FinancialAssistant.Category.Contracts;

public sealed record UserRegisteredCategoryEvent(
    string UserId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    string CausationId);
