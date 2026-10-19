using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

public static class ReceiptRecordCodec
{
    public static void Identifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(c =>
            !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '~')))
        { throw new ArgumentException("Invalid opaque Receipt identifier."); }
    }

    public static void File(ReceiptFileMetadata value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Identifier(value.ReceiptId); Identifier(value.UserId); Identifier(value.ReceiptUploadedEventId);
        if (value.ReceiptId.Length > 100 || value.ContentType is not ("image/jpeg" or "image/png" or "image/webp") ||
            value.SizeBytes is < 1 or > ReceiptProcessingService.MaximumReceiptSizeBytes ||
            value.ContentDigest is null || value.ContentDigest.Length != 64 || !value.ContentDigest.All(char.IsAsciiHexDigit) ||
            value.UploadedAtUtc == default || value.UploadedAtUtc.Offset != TimeSpan.Zero)
        { throw new ArgumentException("Invalid durable Receipt file metadata."); }
    }

    public static void Uploaded(ReceiptUploadedIntegrationEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Identifier(value.ReceiptId); Identifier(value.UserId); Identifier(value.EventId);
        if (value.EventType != ReceiptUploadedIntegrationEvent.Name || value.ReceiptId.Length > 100 ||
            value.ContentType is not ("image/jpeg" or "image/png" or "image/webp") ||
            value.OccurredAtUtc == default || value.OccurredAtUtc.Offset != TimeSpan.Zero)
        { throw new ArgumentException("Invalid durable Receipt upload intent."); }
    }

    public static void Processing(StoredOcrProcessing value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var m = value.Metadata ?? throw new ArgumentException("Missing OCR metadata.");
        Identifier(m.UserId); Identifier(m.ReceiptId);
        var a = m.Audit ?? throw new ArgumentException("Missing OCR audit metadata.");
        Identifier(a.RequestId); Identifier(a.ProviderName); Identifier(a.ModelKey); Identifier(a.TraceId);
        if (m.Status is not (ReceiptProcessingStatuses.OcrCompleted or ReceiptProcessingStatuses.OcrFailed) ||
            m.CompletedAtUtc == default || m.CompletedAtUtc.Offset != TimeSpan.Zero ||
            m.Confidence is < 0 or > 1 || a.Confidence != m.Confidence || a.DurationMilliseconds < 0 ||
            a.ProviderUsage is null || a.ProviderUsage.RequestBytes is < 1 or > ReceiptProcessingService.MaximumReceiptSizeBytes ||
            a.ProviderUsage.ProviderRequestUnits is < 0 or > 1 || !Month(a.ProviderUsage.BillingMonth))
        { throw new ArgumentException("Invalid durable OCR metadata."); }
        Codes(m.Ambiguities);
        if (a.FailureCategory is not null && a.FailureCategory is not ("provider_failure" or "provider_timeout" or
            "provider_unavailable" or "provider_response_invalid" or "provider_disabled" or "provider_request_too_large" or
            "daily_usage_limit_exceeded" or "ocr_output_invalid" or "invalid_provider_response" or "invalid_receipt_content"))
        { throw new ArgumentException("Unsafe OCR failure category."); }
        var e = value.IntegrationEvent;
        if ((m.Status == ReceiptProcessingStatuses.OcrCompleted) != (e is not null) ||
            m.Status == ReceiptProcessingStatuses.OcrCompleted && (m.Confidence is null || a.FailureCategory is not null))
        { throw new ArgumentException("OCR result and status disagree."); }
        if (e is null) { return; }
        Completed(e);
        if (e.EventType != OcrCompletedIntegrationEvent.Name || e.UserId != m.UserId || e.ReceiptId != m.ReceiptId ||
            e.OccurredAtUtc != m.CompletedAtUtc || e.Confidence != m.Confidence ||
            !e.Ambiguities.SequenceEqual(m.Ambiguities))
        { throw new ArgumentException("Invalid OCR candidate identity or values."); }
    }

    public static void Completed(OcrCompletedIntegrationEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Identifier(e.EventId); Identifier(e.UserId); Identifier(e.ReceiptId);
        if (e.EventType != OcrCompletedIntegrationEvent.Name || e.ReceiptId.Length > 100 ||
            e.OccurredAtUtc == default || e.OccurredAtUtc.Offset != TimeSpan.Zero || e.Confidence is < 0 or > 1 ||
            e.TransactionType is not (null or "income" or "expense") || e.Amount is < 0 ||
            e.Currency is not null && (e.Currency.Length != 3 || !e.Currency.All(c => c is >= 'A' and <= 'Z')) || e.Merchant?.Length > 500)
        { throw new ArgumentException("Invalid durable OCR completion intent."); }
        if (e.CategoryId is not null) { Identifier(e.CategoryId); }
        Codes(e.Ambiguities);
    }

    public static T Decode<T>(DurableRecord record, Action<T> validate)
    {
        try { var value = OwnedRecordCodec.Decode<T>(1, record); validate(value); return value; }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private static void Codes(IReadOnlyList<string> values)
    {
        if (values is null || values.Count > 64 || values.Any(v => string.IsNullOrEmpty(v) || v.Length > 100 ||
            v.Any(c => !(c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c == '_'))))
        { throw new ArgumentException("Unsafe OCR ambiguity code."); }
    }

    private static bool Month(string value) => value is { Length: 7 } && value[4] == '-' &&
        int.TryParse(value.AsSpan(0, 4), out var year) && year is >= 1 and <= 9999 &&
        int.TryParse(value.AsSpan(5), out var month) && month is >= 1 and <= 12;
}
