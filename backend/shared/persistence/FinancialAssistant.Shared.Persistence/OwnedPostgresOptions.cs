using Npgsql;

namespace FinancialAssistant.Shared.Persistence;

public sealed class OwnedPostgresOptions
{
    private readonly string password;

    public OwnedPostgresOptions(StorageIdentity identity, string database, int port, string password)
    {
        if (!RecordProtection.ValidName(database) || database is "postgres" or "template0" or "template1" ||
            port is < 1024 or > 65535 || string.IsNullOrWhiteSpace(password) || password.Length is < 32 or > 256)
        {
            throw new ArgumentException("Owned PostgreSQL configuration is missing or invalid.");
        }

        Identity = identity;
        Database = database;
        Port = port;
        this.password = password;
    }

    public StorageIdentity Identity { get; }
    public string Database { get; }
    public int Port { get; }

    internal NpgsqlDataSource CreateDataSource(bool migration)
    {
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = Port,
            Database = Database,
            Username = migration ? Identity.MigrationRole : Identity.RuntimeRole,
            Password = password,
            SslMode = SslMode.Disable,
            Timeout = 3,
            CommandTimeout = 5,
            MaxPoolSize = 8,
            MinPoolSize = 0,
            Enlist = false,
            IncludeErrorDetail = false,
            LogParameters = false,
            ApplicationName = "FinancialAssistant." + Identity.ServiceId,
            Options = "-c search_path=pg_catalog -c statement_timeout=5000 -c lock_timeout=5000"
        };
        return new NpgsqlDataSourceBuilder(connection.ConnectionString).Build();
    }
}
