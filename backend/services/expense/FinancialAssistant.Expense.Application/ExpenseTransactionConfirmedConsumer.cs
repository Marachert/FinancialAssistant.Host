using FinancialAssistant.Expense.Domain;
using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.Expense.Application;

public sealed class ExpenseTransactionConfirmedConsumer : ITransactionConfirmedConsumer
{
    private readonly IExpenseRecordStore store;

    public ExpenseTransactionConfirmedConsumer(IExpenseRecordStore store)
    {
        this.store = store;
    }

    public async Task ConsumeAsync(
        TransactionConfirmedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (!string.Equals(integrationEvent.TransactionType, "expense", StringComparison.Ordinal))
        {
            return;
        }

        Validate(integrationEvent);
        await store.StoreIfMissingAsync(
            new ExpenseRecord(
                integrationEvent.TransactionId,
                integrationEvent.UserId,
                integrationEvent.DraftId,
                integrationEvent.Amount,
                integrationEvent.Currency,
                integrationEvent.CategoryId,
                integrationEvent.Merchant,
                integrationEvent.Date,
                integrationEvent.ConfirmedAtUtc),
            cancellationToken);
    }

    private static void Validate(TransactionConfirmedIntegrationEvent integrationEvent)
    {
        if (string.IsNullOrWhiteSpace(integrationEvent.TransactionId) ||
            string.IsNullOrWhiteSpace(integrationEvent.EventId) ||
            string.IsNullOrWhiteSpace(integrationEvent.UserId) ||
            string.IsNullOrWhiteSpace(integrationEvent.DraftId) ||
            integrationEvent.Amount <= 0 ||
            string.IsNullOrWhiteSpace(integrationEvent.Currency) ||
            integrationEvent.Currency.Length != 3 ||
            string.IsNullOrWhiteSpace(integrationEvent.CategoryId) ||
            integrationEvent.Date == default ||
            integrationEvent.ConfirmedAtUtc == default ||
            !integrationEvent.CategoryId.StartsWith("expense.", StringComparison.Ordinal))
        {
            throw new ArgumentException("Confirmed expense event is invalid.", nameof(integrationEvent));
        }
    }
}
