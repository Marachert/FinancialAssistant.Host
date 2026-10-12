using System.Text.Json.Serialization;
using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Expense.Infrastructure.Persistence;

public sealed class PostgresExpenseRecordStore(ExpensePersistenceContext context) : IExpenseRecordStore
{
    public Task<ExpenseRecord> StoreIfMissingAsync(ExpenseRecord record, CancellationToken cancellationToken)
    {
        ExpenseRecordSnapshot.Validate(record);
        if (record.Origin != ExpenseRecordOrigins.ConfirmedTransaction || record.Revision != 0 || record.Status != ExpenseRecordStatuses.Active)
        { throw new ArgumentException("Confirmation must contain its original financial snapshot."); }
        return context.WithRecordsAsync(record.UserId, async (records, _) =>
        {
            await VerifyOwnerAsync(records, record, create: true);
            var original = await records.GetAsync("confirmed_originals", record.UserId, record.TransactionId);
            if (original is not null)
            {
                var existing = ExpenseRecordSnapshot.Decode(original, record.UserId, record.TransactionId, immutable: true);
                if (existing != record) { throw new FinancialConfirmationConflictException(); }
                return existing;
            }
            if (await records.GetAsync("financial_records", record.UserId, record.TransactionId) is not null)
            { throw new FinancialConfirmationConflictException(); }
            await records.InsertAsync("confirmed_originals", record.UserId, record.TransactionId, ExpenseRecordSnapshot.Encode(record));
            await records.InsertAsync("financial_records", record.UserId, record.TransactionId, ExpenseRecordSnapshot.Encode(record));
            return record;
        }, cancellationToken);
    }

    public Task<bool> CreateAsync(ExpenseRecord record, CancellationToken cancellationToken)
    {
        ExpenseRecordSnapshot.Validate(record);
        if (record.Origin != ExpenseRecordOrigins.Manual || record.Revision != 0 || record.Status != ExpenseRecordStatuses.Active)
        { throw new ArgumentException("Manual creation requires an initial financial record."); }
        return context.WithRecordsAsync(record.UserId, async (records, _) =>
        {
            if (await records.GetAsync("record_owners", "expense", record.TransactionId) is not null) { return false; }
            await VerifyOwnerAsync(records, record, create: true);
            return await records.InsertAsync("financial_records", record.UserId, record.TransactionId, ExpenseRecordSnapshot.Encode(record));
        }, cancellationToken);
    }

    public Task<ExpenseRecord?> GetAsync(string userId, string transactionId, CancellationToken cancellationToken)
    {
        ExpenseRecordSnapshot.ValidateIdentifier(transactionId);
        return context.WithRecordsAsync(userId, async (records, _) =>
        {
            var value = await records.GetAsync("financial_records", userId, transactionId);
            return value is null ? null : ExpenseRecordSnapshot.Decode(value, userId, transactionId);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ExpenseRecord>> ListAsync(string userId, DateOnly from, DateOnly to, bool includeArchived,
        CancellationToken cancellationToken) =>
        context.WithRecordsAsync<IReadOnlyList<ExpenseRecord>>(userId, async (records, _) =>
        {
            var result = new List<ExpenseRecord>();
            string? after = null;
            do
            {
                var page = await records.ReadPageAsync("financial_records", userId, after);
                foreach (var item in page)
                {
                    var record = ExpenseRecordSnapshot.Decode(new DurableRecord(item.Revision, item.Payload), userId);
                    if (record.Date >= from && record.Date <= to && (includeArchived || record.Status == ExpenseRecordStatuses.Active))
                    { result.Add(record); }
                }
                after = page.Count < 100 ? null : page[^1].Cursor;
            } while (after is not null);
            return result.OrderByDescending(record => record.Date).ThenBy(record => record.TransactionId, StringComparer.Ordinal).ToArray();
        }, cancellationToken);

    public Task<ExpenseRecordMutationResult> ReplaceAsync(string userId, string transactionId, long expectedRevision,
        ExpenseRecord replacement, CancellationToken cancellationToken)
    {
        ExpenseRecordSnapshot.Validate(replacement);
        if (replacement.UserId != userId || replacement.TransactionId != transactionId || expectedRevision < 0
            || expectedRevision >= long.MaxValue - 1 || replacement.Revision != expectedRevision + 1)
        { throw new ArgumentException("Invalid financial replacement identity or revision."); }
        return context.WithRecordsAsync(userId, async (records, _) =>
        {
            var existing = await records.GetAsync("financial_records", userId, transactionId);
            if (existing is null) { return new ExpenseRecordMutationResult(null, false); }
            var current = ExpenseRecordSnapshot.Decode(existing, userId, transactionId);
            if (current.Revision != expectedRevision) { return new ExpenseRecordMutationResult(current, false); }
            if (current.Origin != replacement.Origin || current.SourceDraftId != replacement.SourceDraftId
                || current.ConfirmedAtUtc != replacement.ConfirmedAtUtc || replacement.UpdatedAtUtc < current.UpdatedAtUtc)
            { throw new ArgumentException("Financial origin and source metadata are immutable."); }
            await records.ReplaceAsync("financial_records", userId, transactionId, existing.Revision, ExpenseRecordSnapshot.Encode(replacement));
            return new ExpenseRecordMutationResult(replacement, true);
        }, cancellationToken);
    }

    private static async Task VerifyOwnerAsync(OwnedRecordTransaction records, ExpenseRecord record, bool create)
    {
        var existing = await records.GetAsync("record_owners", "expense", record.TransactionId);
        if (existing is not null)
        {
            if (OwnedRecordCodec.Decode<OwnerMarker>(1, existing).Owner != record.UserId)
            { throw new FinancialConfirmationConflictException(); }
        }
        else if (create)
        { await records.InsertAsync("record_owners", "expense", record.TransactionId, OwnedRecordCodec.Encode(1, new OwnerMarker(record.UserId))); }
    }

    private sealed record OwnerMarker([property: JsonRequired] string Owner);
}

public sealed class FinancialConfirmationConflictException() : Exception("Conflicting financial confirmation.");
