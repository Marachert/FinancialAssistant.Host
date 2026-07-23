namespace FinancialAssistant.ReceiptProcessing.Domain;

public static class ReceiptProcessingStatuses
{
    public const string Uploaded = "uploaded";

    public const string OcrCompleted = "ocr_completed";

    public const string OcrFailed = "ocr_failed";
}

public sealed record ReceiptFileMetadata(
    string ReceiptId,
    string UserId,
    string ContentType,
    long SizeBytes,
    string ContentDigest,
    DateTimeOffset UploadedAtUtc,
    string ReceiptUploadedEventId,
    bool ReceiptUploadedPublished);

public sealed record ReceiptOcrMetadata(
    string ReceiptId,
    string UserId,
    string Status,
    decimal? Confidence,
    IReadOnlyList<string> Ambiguities,
    DateTimeOffset CompletedAtUtc,
    bool OcrCompletedPublished);

public sealed class ReceiptProcessingDomainAssemblyMarker;
