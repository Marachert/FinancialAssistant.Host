using System.Data;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace FinancialAssistant.Shared.Persistence;

public sealed class OwnedPostgresStore : IAsyncDisposable
{
    private readonly StorageIdentity identity;
    private readonly NpgsqlDataSource source;
    private readonly RecordProtection protection;
    internal static string MigrationSql(string schema) => $"""
        CREATE TABLE {schema}.schema_versions(version integer PRIMARY KEY, checksum text NOT NULL,
            index_marker bytea NOT NULL CHECK(octet_length(index_marker)=32));
        CREATE TABLE {schema}.encryption_keys(key_id text PRIMARY KEY, marker bytea NOT NULL CHECK(octet_length(marker)=32));
        CREATE TABLE {schema}.records(
            bucket text NOT NULL CHECK(length(bucket) BETWEEN 1 AND 64),
            owner_hash bytea NOT NULL CHECK(octet_length(owner_hash)=32),
            record_hash bytea NOT NULL CHECK(octet_length(record_hash)=32),
            revision bigint NOT NULL CHECK(revision>=0),
            payload bytea NOT NULL CHECK(octet_length(payload) BETWEEN 31 AND 1048638),
            PRIMARY KEY(bucket,owner_hash,record_hash));
        """;

    public OwnedPostgresStore(OwnedPostgresOptions options, RecordProtection protection)
    {
        identity = options.Identity;
        source = options.CreateDataSource(false);
        this.protection = protection;
    }

    private static string Checksum(string schema) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(MigrationSql(schema))));

    public static async Task MigrateAsync(OwnedPostgresOptions options, RecordProtection protection, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var token = deadline.Token;
        try
        {
            await using var migrationSource = options.CreateDataSource(true);
            await using var connection = await migrationSource.OpenConnectionAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);
            await ValidateOwnershipAsync(connection, options.Identity, true, token);
            await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", connection))
            {
                gate.Parameters.AddWithValue("migration:" + options.Identity.Schema);
                await gate.ExecuteNonQueryAsync(token);
            }

            var schema = options.Identity.Schema;
            await using var existing = new NpgsqlCommand("SELECT to_regclass($1)::text", connection);
            existing.Parameters.AddWithValue(schema + ".schema_versions");
            if (await existing.ExecuteScalarAsync(token) is not string)
            {
                await using var create = new NpgsqlCommand(MigrationSql(schema), connection);
                await create.ExecuteNonQueryAsync(token);
                await using var version = new NpgsqlCommand($"INSERT INTO {schema}.schema_versions VALUES (1,$1,$2)", connection);
                version.Parameters.AddWithValue(Checksum(schema));
                version.Parameters.AddWithValue(protection.Index(options.Identity.ServiceId, "index-key-marker", "v1"));
                await version.ExecuteNonQueryAsync(token);
            }

            await ValidateVersionAsync(connection, options.Identity, true, token);
            await ValidateKeysAsync(connection, options.Identity, protection, true, token);
            await transaction.CommitAsync(token);
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            throw new StorageUnavailableException();
        }
    }

    internal static async Task ValidateOwnershipAsync(NpgsqlConnection connection, StorageIdentity identity,
        bool migration, CancellationToken token)
    {
        const string sql = """
            SELECT current_user, r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls,
              EXISTS(SELECT 1 FROM pg_auth_members WHERE member=r.oid),
              (SELECT pg_get_userbyid(nspowner) FROM pg_namespace WHERE nspname=$1),
              has_schema_privilege(current_user,$1,'USAGE'),
              has_schema_privilege(current_user,$1,'CREATE'),
              EXISTS(SELECT 1 FROM pg_namespace n WHERE n.nspname !~ '^pg_' AND n.nspname NOT IN($1,'information_schema','public')
                AND (has_schema_privilege(current_user,n.oid,'USAGE') OR has_schema_privilege(current_user,n.oid,'CREATE'))),
              has_schema_privilege(current_user,'public','CREATE') OR has_schema_privilege(current_user,'public','USAGE'),
              has_database_privilege(current_user,current_database(),'CREATE')
            FROM pg_roles r WHERE r.rolname=current_user
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(identity.Schema);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token) || reader.GetString(0) != (migration ? identity.MigrationRole : identity.RuntimeRole) ||
            reader.GetBoolean(1) || reader.GetBoolean(2) || reader.IsDBNull(3) || reader.GetString(3) != identity.MigrationRole ||
            !reader.GetBoolean(4) || reader.GetBoolean(5) != migration || reader.GetBoolean(6) || reader.GetBoolean(7) || reader.GetBoolean(8))
        {
            throw new StorageUnavailableException();
        }
    }

    private static async Task ValidateVersionAsync(NpgsqlConnection connection, StorageIdentity identity, bool migration, CancellationToken token)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT version,checksum,
              (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname=$1 AND c.relname IN('records','schema_versions','encryption_keys') AND c.relkind='r'
                  AND pg_get_userbyid(c.relowner)=$2),
              has_table_privilege(current_user,$3,'SELECT') AND has_table_privilege(current_user,$3,'INSERT')
                AND has_table_privilege(current_user,$3,'UPDATE') AND has_table_privilege(current_user,$3,'DELETE'),
              has_table_privilege(current_user,$3,'TRUNCATE') OR has_table_privilege(current_user,$3,'TRIGGER')
                OR has_table_privilege(current_user,$3,'REFERENCES'),
              has_table_privilege(current_user,$4,'INSERT') OR has_table_privilege(current_user,$4,'UPDATE')
                OR has_table_privilege(current_user,$4,'DELETE') OR has_table_privilege(current_user,$4,'TRUNCATE')
                OR has_table_privilege(current_user,$4,'TRIGGER') OR has_table_privilege(current_user,$4,'REFERENCES'),
              (SELECT string_agg(a.attname || ':' || format_type(a.atttypid,a.atttypmod) || ':' || a.attnotnull::text,'|' ORDER BY a.attnum)
                FROM pg_attribute a WHERE a.attrelid=to_regclass($3) AND a.attnum>0 AND NOT a.attisdropped),
              (SELECT string_agg(a.attname || ':' || format_type(a.atttypid,a.atttypmod) || ':' || a.attnotnull::text,'|' ORDER BY a.attnum)
                FROM pg_attribute a WHERE a.attrelid=to_regclass($4) AND a.attnum>0 AND NOT a.attisdropped),
              (SELECT string_agg(a.attname || ':' || format_type(a.atttypid,a.atttypmod) || ':' || a.attnotnull::text,'|' ORDER BY a.attnum)
                FROM pg_attribute a WHERE a.attrelid=to_regclass($5) AND a.attnum>0 AND NOT a.attisdropped),
              has_table_privilege(current_user,$5,'INSERT') OR has_table_privilege(current_user,$5,'UPDATE')
                OR has_table_privilege(current_user,$5,'DELETE') OR has_table_privilege(current_user,$5,'TRUNCATE')
                OR has_table_privilege(current_user,$5,'TRIGGER') OR has_table_privilege(current_user,$5,'REFERENCES'),
              (SELECT count(*) FROM pg_constraint WHERE contype='p' AND convalidated AND
                ((conrelid=to_regclass($3) AND conkey=ARRAY[1,2,3]::smallint[]) OR
                 (conrelid=to_regclass($4) AND conkey=ARRAY[1]::smallint[]) OR
                 (conrelid=to_regclass($5) AND conkey=ARRAY[1]::smallint[])))
            FROM {identity.Schema}.schema_versions
            """, connection);
        command.Parameters.AddWithValue(identity.Schema);
        command.Parameters.AddWithValue(identity.MigrationRole);
        command.Parameters.AddWithValue(identity.Schema + ".records");
        command.Parameters.AddWithValue(identity.Schema + ".schema_versions");
        command.Parameters.AddWithValue(identity.Schema + ".encryption_keys");
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token) || reader.GetInt32(0) != 1 || reader.GetString(1) != Checksum(identity.Schema) ||
            reader.GetInt64(2) != 3 || (!migration && (!reader.GetBoolean(3) || reader.GetBoolean(4) || reader.GetBoolean(5))) ||
            reader.IsDBNull(6) || reader.GetString(6) != "bucket:text:true|owner_hash:bytea:true|record_hash:bytea:true|revision:bigint:true|payload:bytea:true" ||
            reader.IsDBNull(7) || reader.GetString(7) != "version:integer:true|checksum:text:true|index_marker:bytea:true" ||
            reader.IsDBNull(8) || reader.GetString(8) != "key_id:text:true|marker:bytea:true" || (!migration && reader.GetBoolean(9)) || reader.GetInt64(10) != 3 ||
            await reader.ReadAsync(token))
        {
            throw new StorageUnavailableException();
        }
    }

    private static async Task ValidateKeysAsync(NpgsqlConnection connection, StorageIdentity identity,
        RecordProtection protection, bool migration, CancellationToken token)
    {
        await using (var index = new NpgsqlCommand($"SELECT index_marker FROM {identity.Schema}.schema_versions", connection))
        {
            if (await index.ExecuteScalarAsync(token) is not byte[] marker ||
                !CryptographicOperations.FixedTimeEquals(marker, protection.Index(identity.ServiceId, "index-key-marker", "v1")))
            {
                throw new StorageUnavailableException();
            }
        }
        var expected = protection.KeyMarkers(identity.ServiceId);
        var existing = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand($"SELECT key_id,marker FROM {identity.Schema}.encryption_keys", connection))
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
            {
                var id = reader.GetString(0);
                if (!expected.TryGetValue(id, out var marker) || !CryptographicOperations.FixedTimeEquals(marker, reader.GetFieldValue<byte[]>(1)))
                {
                    throw new StorageUnavailableException();
                }
                existing.Add(id);
            }
        }
        if (!migration && existing.Count != expected.Count) { throw new StorageUnavailableException(); }
        foreach (var key in expected.Where(pair => !existing.Contains(pair.Key)))
        {
            await using var command = new NpgsqlCommand($"INSERT INTO {identity.Schema}.encryption_keys VALUES($1,$2)", connection);
            command.Parameters.AddWithValue(key.Key);
            command.Parameters.AddWithValue(key.Value);
            await command.ExecuteNonQueryAsync(token);
        }
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(Array.Empty<string>(), (_, _) => Task.FromResult(true), cancellationToken);
        }
        catch (Exception exception) when (exception is StorageUnavailableException or OperationCanceledException)
        {
            return false;
        }
    }

    public async Task<T> ExecuteAsync<T>(IReadOnlyCollection<string> lockKeys,
        Func<OwnedRecordTransaction, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (lockKeys.Count > 32) { throw new ArgumentException("Too many transaction locks.", nameof(lockKeys)); }
        var locks = lockKeys.Select(key => System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(
            protection.Index(identity.ServiceId, "lock", key))).Distinct().Order().ToArray();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var token = deadline.Token;
        try
        {
            await using var connection = await source.OpenConnectionAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            await ValidateOwnershipAsync(connection, identity, false, token);
            await ValidateVersionAsync(connection, identity, false, token);
            await ValidateKeysAsync(connection, identity, protection, false, token);
            foreach (var key in locks)
            {
                await using var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection);
                gate.Parameters.AddWithValue(key);
                await gate.ExecuteNonQueryAsync(token);
            }

            var session = new OwnedRecordTransaction(connection, identity, protection, token);
            try
            {
                var result = await operation(session, token).WaitAsync(token);
                session.Seal();
                await transaction.CommitAsync(token);
                return result;
            }
            finally { session.Close(); }
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            throw new StorageUnavailableException();
        }
    }

    public ValueTask DisposeAsync() => source.DisposeAsync();
}
