using FinancialAssistant.ReceiptProcessing.Contracts;

namespace FinancialAssistant.ReceiptProcessing.Application;

public interface IReceiptProcessingService
{
    Task<ReceiptUploadResult> UploadAsync(
        string userId,
        string idempotencyKey,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);

    Task<ReceiptResponse?> GetAsync(
        string userId,
        string receiptId,
        CancellationToken cancellationToken);
}

public sealed record ReceiptUploadResult(ReceiptResponse Receipt, bool Replayed);
