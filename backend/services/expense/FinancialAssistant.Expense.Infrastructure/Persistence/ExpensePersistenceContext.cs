using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Expense.Infrastructure.Persistence;

public sealed class ExpensePersistenceContext
{
    private readonly OwnedPostgresStore database;
    private readonly AsyncLocal<Scope?> current = new();

    public ExpensePersistenceContext(OwnedPostgresStore database)
    {
        if (database.ServiceId != "expense") { throw new ArgumentException("Expense requires its owned storage role."); }
        this.database = database;
    }

    public Task<T> RunAsync<T>(string owner, Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default, string? confirmationId = null)
    {
        ExpenseRecordSnapshot.ValidateIdentifier(owner);
        if (current.Value is not null)
        {
            if (current.Value.Owner != owner) { throw new StorageUnavailableException(); }
            return operation(cancellationToken);
        }
        var locks = confirmationId is null ? new[] { "owner:" + owner } : new[] { "owner:" + owner, "confirmation:" + confirmationId };
        return database.ExecuteAsync(locks, async (records, token) =>
        {
            current.Value = new Scope(owner, records);
            try { return await operation(token); }
            finally { current.Value = null; }
        }, cancellationToken);
    }

    public Task<T> WithRecordsAsync<T>(string owner, Func<OwnedRecordTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) =>
        RunAsync(owner, token => operation(current.Value?.Records ?? throw new StorageUnavailableException(), token), cancellationToken);

    private sealed record Scope(string Owner, OwnedRecordTransaction Records);
}
