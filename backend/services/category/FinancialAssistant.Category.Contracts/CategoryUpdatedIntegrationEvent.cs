namespace FinancialAssistant.Category.Contracts;

public sealed record CategoryUpdatedIntegrationEvent(
    string UserId,
    string CategoryId,
    int Version,
    string ChangeType,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId)
{
    public const string Name = "category.updated.v1";

    public string EventType => Name;
}
