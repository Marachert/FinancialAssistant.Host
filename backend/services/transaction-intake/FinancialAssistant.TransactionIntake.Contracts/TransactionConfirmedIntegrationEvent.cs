namespace FinancialAssistant.TransactionIntake.Contracts;

public sealed record TransactionConfirmedIntegrationEvent(
    string EventId,
    string TransactionId,
    string UserId,
    string DraftId,
    string TransactionType,
    decimal Amount,
    string Currency,
    string CategoryId,
    string? Merchant,
    DateOnly Date,
    DateTimeOffset ConfirmedAtUtc,
    string CorrelationId)
{
    public const string Name = "transaction.confirmed.v1";

    public string EventType => Name;
}
