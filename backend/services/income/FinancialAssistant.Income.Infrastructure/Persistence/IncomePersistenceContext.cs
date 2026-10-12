using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Income.Infrastructure.Persistence;

public sealed class IncomePersistenceContext
{
    private readonly OwnedPostgresStore database;
    private readonly AsyncLocal<Scope?> current = new();

    public IncomePersistenceContext(OwnedPostgresStore database)
    {
        if (database.ServiceId != "income") { throw new ArgumentException("Income requires its owned storage role."); }
        this.database = database;
    }

    public Task<T> RunAsync<T>(string owner, Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default, string? confirmationId = null)
    {
        IncomeRecordSnapshot.ValidateIdentifier(owner);
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
