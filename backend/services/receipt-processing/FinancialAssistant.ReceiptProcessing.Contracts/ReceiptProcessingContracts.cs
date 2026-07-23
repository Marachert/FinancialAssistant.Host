namespace FinancialAssistant.ReceiptProcessing.Contracts;

public static class ReceiptProcessingApiRoutes
{
    public const string Upload = "/api/v1/receipts";

    public const string GatewayUpload = "/receipts";

    public const string Get = "/api/v1/receipts/{receiptId}";

    public const string GatewayGet = "/receipts/{receiptId}";

    public const string OcrCompletedEvent = "/internal/events/ocr-completed";
}

public static class ReceiptProcessingHeaders
{
    public const string GatewayAuthentication = "X-Gateway-Authentication";

    public const string GatewayUserId = "X-Gateway-User-Id";

    public const string IdempotencyKey = "Idempotency-Key";

    public const string EventAuthentication = "X-Receipt-Event-Authentication";
}

public sealed record ReceiptResponse(
    string ReceiptId,
    string Status,
    string ContentType,
    long SizeBytes,
    decimal? OcrConfidence,
    IReadOnlyList<string> OcrAmbiguities,
    DateTimeOffset UploadedAtUtc);

public sealed record ReceiptErrorResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Code,
    string TraceId);
