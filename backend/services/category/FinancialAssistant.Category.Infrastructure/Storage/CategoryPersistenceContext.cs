using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Category.Infrastructure.Storage;

public sealed class CategoryPersistenceContext
{
    private readonly OwnedPostgresStore database;
    public CategoryPersistenceContext(OwnedPostgresStore database)
    {
        if (database.ServiceId != "category") { throw new ArgumentException("Category requires its owned storage role."); }
        this.database = database;
    }
    private readonly AsyncLocal<Scope?> current = new();

    public Task<T> RunAsync<T>(string userId, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ValidateOwner(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (current.Value is not null)
        {
            if (current.Value.Owner != userId) { throw new StorageUnavailableException(); }
            return operation(cancellationToken);
        }
        return database.ExecuteAsync(new[] { userId }, async (records, token) =>
        {
            current.Value = new Scope(userId, records);
            try { return await operation(token); }
            finally { current.Value = null; }
        }, cancellationToken);
    }

    public Task<T> WithRecordsAsync<T>(string userId, Func<OwnedRecordTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) => RunAsync(userId,
            token => operation(current.Value?.Records ?? throw new StorageUnavailableException(), token), cancellationToken);

    private static void ValidateOwner(string owner)
    {
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 128 || owner != owner.Trim() || owner.Any(char.IsControl))
        { throw new ArgumentException("Invalid category owner."); }
    }
    private sealed record Scope(string Owner, OwnedRecordTransaction Records);
}
