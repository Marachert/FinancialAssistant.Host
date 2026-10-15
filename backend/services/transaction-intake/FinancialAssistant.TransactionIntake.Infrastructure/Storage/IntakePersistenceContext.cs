using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Application.Abstractions;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Storage;

public sealed class IntakePersistenceContext : ITransactionIntakeMutationScope
{
    private readonly OwnedPostgresStore database;
    private readonly AsyncLocal<Scope?> current = new();

    public IntakePersistenceContext(OwnedPostgresStore database)
    {
        if (database.ServiceId != "transaction-intake") { throw new ArgumentException("Intake requires its owned storage role."); }
        this.database = database;
    }

    public Task<T> RunAsync<T>(string owner, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        IntakeRecordCodec.Identifier(owner);
        if (current.Value is not null)
        {
            if (current.Value.Owner != owner) { throw new StorageUnavailableException(); }
            return operation(cancellationToken);
        }
        return database.ExecuteAsync(new[] { "owner:" + owner }, async (records, token) =>
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
