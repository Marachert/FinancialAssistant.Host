using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.FinancialSummary.Infrastructure.Persistence;

public sealed class NativeFinancialSummaryStorage : IAsyncDisposable
{
    private readonly RecordProtection protection;
    private readonly OwnedPostgresStore database;

    public NativeFinancialSummaryStorage(NativePostgresSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var options = settings.CreateOptions("financial-summary");
        protection = settings.CreateProtection();
        try
        {
            database = new OwnedPostgresStore(options, protection);
            Store = new PostgresFinancialSummaryReadModelStore(database);
        }
        catch { protection.Dispose(); throw; }
    }

    public PostgresFinancialSummaryReadModelStore Store { get; }
    public Task<bool> IsStorageReadyAsync(CancellationToken token = default) => database.IsReadyAsync(token);

    public static async Task MigrateAsync(NativePostgresSettings separatelyProvisionedSettings, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(separatelyProvisionedSettings);
        var options = separatelyProvisionedSettings.CreateOptions("financial-summary");
        using var keys = separatelyProvisionedSettings.CreateProtection();
        await OwnedPostgresStore.MigrateAsync(options, keys, token);
    }

    public async ValueTask DisposeAsync()
    {
        try { await database.DisposeAsync(); }
        finally { protection.Dispose(); }
    }
}
