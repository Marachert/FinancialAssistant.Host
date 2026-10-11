using FinancialAssistant.Shared.Hosting;

namespace FinancialAssistant.Shared.Persistence;

public sealed class StorageIdentity
{
    public StorageIdentity(string serviceId)
    {
        NativeServiceCatalog.Get(serviceId);
        if (serviceId is "gateway" or "mcp")
        {
            throw new ArgumentException("This service has no business database.", nameof(serviceId));
        }

        ServiceId = serviceId;
        Schema = "fa_" + serviceId.Replace('-', '_');
    }

    public string ServiceId { get; }
    public string Schema { get; }
    public string RuntimeRole => Schema;
    public string MigrationRole => Schema + "_migration";
    public override string ToString() => ServiceId;
}

public sealed class StorageUnavailableException : Exception
{
    public StorageUnavailableException() : base("Owned durable storage is unavailable or invalid.") { }
}

public sealed class StorageConflictException : Exception
{
    public StorageConflictException() : base("Owned state changed concurrently; the transaction was not committed.") { }
}
