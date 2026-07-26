namespace FinancialAssistant.TransactionIntake.Contracts;

public sealed record TransactionDraftCreatedIntegrationEvent(
    string EventId,
    string JobId,
    string DraftId,
    string UserId,
    string SourcePayloadReferenceId,
    DateTimeOffset OccurredAtUtc)
{
    public const string Name = "transaction.draft-created.v1";

    public string EventType => Name;
}

public interface ITransactionDraftCreatedConsumer
{
    Task ConsumeAsync(
        TransactionDraftCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}
