using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Contracts;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

public sealed class TransactionalReceiptService(ReceiptPersistenceContext context, ReceiptProcessingService service) : IReceiptProcessingService
{
    public Task<ReceiptUploadResult> UploadAsync(string userId, string key, string contentType, Stream content, CancellationToken token) =>
        context.RunAsync(userId, deadline => service.UploadAsync(userId, key, contentType, content, deadline), token);

    public Task<ReceiptResponse?> GetAsync(string userId, string receiptId, CancellationToken token) =>
        context.RunAsync(userId, deadline => service.GetAsync(userId, receiptId, deadline), token);
}

public sealed class TransactionalReceiptUploadedConsumer(ReceiptPersistenceContext context, ReceiptOcrProcessor processor) : IReceiptUploadedConsumer
{
    public Task ConsumeAsync(ReceiptUploadedIntegrationEvent value, CancellationToken token) =>
        context.RunAsync(value.UserId, async deadline => { await processor.ConsumeAsync(value, deadline); return true; }, token);
}
