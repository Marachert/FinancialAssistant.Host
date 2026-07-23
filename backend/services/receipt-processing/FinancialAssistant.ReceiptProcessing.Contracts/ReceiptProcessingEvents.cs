namespace FinancialAssistant.ReceiptProcessing.Contracts;

public sealed record ReceiptUploadedIntegrationEvent(
    string EventType,
    string EventId,
    string ReceiptId,
    string UserId,
    string ContentType,
    DateTimeOffset OccurredAtUtc)
{
    public const string Name = "receipt.uploaded.v1";
}

public sealed record OcrCompletedIntegrationEvent(
    string EventType,
    string EventId,
    string ReceiptId,
    string UserId,
    string? TransactionType,
    decimal? Amount,
    string? Currency,
    string? CategoryId,
    string? Merchant,
    DateOnly? Date,
    decimal Confidence,
    IReadOnlyList<string> Ambiguities,
    DateTimeOffset OccurredAtUtc)
{
    public const string Name = "ocr.completed.v1";
}

public interface IReceiptUploadedConsumer
{
    Task ConsumeAsync(
        ReceiptUploadedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}

public interface IOcrCompletedConsumer
{
    Task ConsumeAsync(
        OcrCompletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}
