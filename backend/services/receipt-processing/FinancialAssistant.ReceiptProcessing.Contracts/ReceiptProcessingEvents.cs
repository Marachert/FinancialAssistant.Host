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

public static class OcrExtractionJobStatuses
{
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string SuggestionReady = "suggestion_ready";
    public const string Failed = "failed";
}

public sealed record OcrExtractionJobCommand(
    string CommandId,
    string JobId,
    string ReceiptId,
    string UserId,
    string ContentType,
    int Attempt,
    DateTimeOffset RequestedAtUtc)
{
    public const string Name = "ocr.extraction.requested.v1";

    public string CommandType => Name;
}

public sealed record OcrExtractionFailedIntegrationEvent(
    string EventId,
    string JobId,
    string ReceiptId,
    string UserId,
    string FailureCategory,
    bool Retryable,
    int Attempt,
    DateTimeOffset OccurredAtUtc)
{
    public const string Name = "ocr.extraction-failed.v1";

    public string EventType => Name;
}

public sealed record OcrExtractionStatusUpdatedIntegrationEvent(
    string EventId,
    string JobId,
    string ReceiptId,
    string UserId,
    string Status,
    int Attempt,
    DateTimeOffset OccurredAtUtc)
{
    public const string Name = "ocr.extraction-status-updated.v1";

    public string EventType => Name;
}

public interface IReceiptUploadedConsumer
{
    Task ConsumeAsync(
        ReceiptUploadedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}

public interface IOcrExtractionJobConsumer
{
    Task ConsumeAsync(
        OcrExtractionJobCommand command,
        CancellationToken cancellationToken);
}

public interface IOcrCompletedConsumer
{
    Task ConsumeAsync(
        OcrCompletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}

public interface IOcrExtractionFailedConsumer
{
    Task ConsumeAsync(
        OcrExtractionFailedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}

public interface IOcrExtractionStatusUpdatedConsumer
{
    Task ConsumeAsync(
        OcrExtractionStatusUpdatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}
