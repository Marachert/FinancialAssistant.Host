using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Storage;

internal sealed class TransactionalDraftReviewService(TransactionDraftReviewService inner, IntakePersistenceContext context) : ITransactionDraftReviewService
{
    public Task<TransactionDraftResponse?> ReviewAsync(string owner, string id, CancellationToken token) => inner.ReviewAsync(owner, id, token);
    public Task<TransactionDraftResponse?> ReviewReceiptAsync(string owner, string id, CancellationToken token) => inner.ReviewReceiptAsync(owner, id, token);
    public Task<TransactionDraftResponse?> UpdateAsync(string owner, string id, TransactionDraftUpdateRequest request, CancellationToken token)
    {
        TransactionDraftReviewService.ValidateUpdateRequest(owner, id, request);
        return context.RunAsync(Owner(owner), cancellation => inner.UpdateAsync(owner, id, request, cancellation), token);
    }
    public Task<TransactionDraftResponse?> RejectAsync(string owner, string id, CancellationToken token)
    {
        _ = Owner(id);
        return context.RunAsync(Owner(owner), cancellation => inner.RejectAsync(owner, id, cancellation), token);
    }
    internal static string Owner(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { throw new ArgumentException("Intake identity is required."); }
        var normalized = value.Trim();
        IntakeRecordCodec.Identifier(normalized);
        return normalized;
    }
}

internal sealed class TransactionalConfirmationService(TransactionConfirmationService inner, IntakePersistenceContext context) : ITransactionConfirmationService
{
    public Task<TransactionConfirmationResult?> ConfirmAsync(string owner, string id, string? correlation, CancellationToken token)
    {
        var normalizedOwner = TransactionalDraftReviewService.Owner(owner);
        _ = TransactionalDraftReviewService.Owner(id);
        if (correlation is not null && correlation.Any(char.IsControl)) { throw new ArgumentException("Invalid confirmation correlation."); }
        return context.RunAsync(normalizedOwner, cancellation => inner.ConfirmAsync(owner, id, correlation, cancellation), token);
    }
}

internal sealed class TransactionalOcrDraftConsumer(OcrCompletedDraftConsumer inner, IntakePersistenceContext context) : IOcrCompletedConsumer
{
    public Task ConsumeAsync(OcrCompletedIntegrationEvent value, CancellationToken token)
    {
        OcrCompletedDraftConsumer.Validate(value);
        return context.RunAsync(value.UserId, async cancellation =>
        {
            await inner.ConsumeAsync(value, cancellation);
            return true;
        }, token);
    }
}
