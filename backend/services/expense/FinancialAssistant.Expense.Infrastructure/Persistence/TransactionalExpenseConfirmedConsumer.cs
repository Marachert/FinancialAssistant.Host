using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinancialAssistant.Expense.Application;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.Expense.Infrastructure.Persistence;

internal sealed class TransactionalExpenseConfirmedConsumer(ExpenseTransactionConfirmedConsumer inner,
    ExpensePersistenceContext context, PostgresExpenseRecordStore store) : ITransactionConfirmedConsumer
{
    public async Task ConsumeAsync(TransactionConfirmedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (integrationEvent.TransactionType != "expense") { return; }
        ExpenseRecordSnapshot.ValidateIdentifier(integrationEvent.UserId);
        ExpenseRecordSnapshot.ValidateIdentifier(integrationEvent.EventId);
        ExpenseRecordSnapshot.ValidateIdentifier(integrationEvent.TransactionId);
        var candidate = new FinancialAssistant.Expense.Domain.ExpenseRecord(
            integrationEvent.TransactionId, integrationEvent.UserId, integrationEvent.DraftId,
            integrationEvent.Amount, integrationEvent.Currency, integrationEvent.CategoryId,
            integrationEvent.Merchant, integrationEvent.Date, integrationEvent.ConfirmedAtUtc);
        ExpenseRecordSnapshot.Validate(candidate);
        if (string.IsNullOrWhiteSpace(integrationEvent.CorrelationId) || integrationEvent.CorrelationId.Length > 128
            || integrationEvent.CorrelationId != integrationEvent.CorrelationId.Trim() || integrationEvent.CorrelationId.Any(char.IsControl)
            || integrationEvent.EventId.Length > 128)
        { throw new ArgumentException("Invalid financial confirmation metadata."); }
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(integrationEvent)));
        await context.RunAsync(integrationEvent.UserId, token => context.WithRecordsAsync(integrationEvent.UserId, async (records, _) =>
        {
            var receipt = await records.GetAsync("confirmation_receipts", "expense", integrationEvent.EventId);
            if (receipt is not null)
            {
                if (OwnedRecordCodec.Decode<Receipt>(1, receipt).Fingerprint != fingerprint) { throw new FinancialConfirmationConflictException(); }
                return true;
            }
            var original = await records.GetAsync("confirmed_originals", integrationEvent.UserId, integrationEvent.TransactionId);
            if (original is null) { await inner.ConsumeAsync(integrationEvent, token); }
            else
            {
                await store.StoreIfMissingAsync(candidate, token);
            }
            await records.InsertAsync("confirmation_receipts", "expense", integrationEvent.EventId, OwnedRecordCodec.Encode(1, new Receipt(fingerprint)));
            return true;
        }, token), cancellationToken, integrationEvent.TransactionId);
    }
    private sealed record Receipt([property: JsonRequired] string Fingerprint);
}
