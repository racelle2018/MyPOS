using Microsoft.Data.Sqlite;

namespace RetailForma.EncryptedStorage;

/// <summary>
/// For the dedicated service/test process only, never load alongside the desktop's plaintext
/// SQLite bundle. Keys are supplied by trusted service storage, not by cashier requests.
/// Does not install a service, change ACLs, migrate a database, or generate replacement keys.
/// </summary>
public static class SqlCipherConnections
{
    private static readonly Lazy<string> Provider = new(InitializeProvider);

    public static string ProviderVersion => Provider.Value;

    public static SqliteConnection OpenExisting(string path, ReadOnlySpan<byte> key)
    {
        Validate(path, key);
        if (!File.Exists(path)) throw new FileNotFoundException("The encrypted database is missing. Recover it; do not create a replacement.", path);
        if (new FileInfo(path).Length < 512)
            throw new InvalidDataException("The database is empty or incomplete. Recover it; do not initialize it as a new company.");
        return Open(path, key);
    }

    /// <summary>Only for an explicitly provisioned, service-owned directory; never a startup fallback.</summary>
    public static SqliteConnection CreateNew(string path, ReadOnlySpan<byte> key)
    {
        Validate(path, key);
        _ = Provider.Value; // No filesystem change if the native encryption provider is unavailable.
        using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        // Failures deliberately leave the reserved target for inspection, never overwrite another file.
        var connection = Open(path, key);
        try
        {
            Execute(connection, "CREATE TABLE __RetailFormaStorage (Format INTEGER NOT NULL CHECK(Format=1)); INSERT INTO __RetailFormaStorage VALUES (1);");
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    private static SqliteConnection Open(string path, ReadOnlySpan<byte> key)
    {
        _ = Provider.Value;
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false,
            Cache = SqliteCacheMode.Private, DefaultTimeout = 5
        }.ToString());
        try
        {
            connection.Open();
            // Only generated hex enters SQL. Do not log this statement or attach EF sensitive logging.
            // ConnectionString intentionally never contains the secret.
            Execute(connection, "PRAGMA key = \"x'" + Convert.ToHexString(key) + "'\";");
            Execute(connection, "PRAGMA temp_store=MEMORY; PRAGMA cipher_memory_security=ON; PRAGMA foreign_keys=ON;");
            using var check = connection.CreateCommand();
            check.CommandText = "SELECT count(*) FROM sqlite_master;";
            check.ExecuteScalar(); // Force actual page authentication; setting PRAGMA key alone is not proof.
            RequireEncryptionActive(connection);
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    /// <summary>Full scan for migration/restore/health checks, not every operation's connection.</summary>
    public static void VerifyIntegrity(SqliteConnection connection)
    {
        _ = Provider.Value;
        RequireEncryptionActive(connection);
        using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA cipher_integrity_check;";
        using (var rows = check.ExecuteReader())
            if (rows.Read()) throw new InvalidDataException("Encrypted database authentication failed.");
        check.CommandText = "PRAGMA integrity_check;";
        using (var integrity = check.ExecuteReader())
            if (!integrity.Read() || integrity.GetString(0) != "ok" || integrity.Read())
                throw new InvalidDataException("Database integrity validation failed.");
        check.CommandText = "PRAGMA foreign_key_check;";
        using var foreignKeys = check.ExecuteReader();
        if (foreignKeys.Read()) throw new InvalidDataException("Database relationships are inconsistent. Recover a verified backup; do not omit orphaned records.");
    }

    private static void RequireEncryptionActive(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA cipher_status;";
        if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "1", StringComparison.Ordinal))
            throw new InvalidDataException("This connection is not using database encryption.");
    }

    /// <summary>Consistent encrypted-to-encrypted snapshot. No plaintext staging or live-file replacement.</summary>
    public static void ExportEncrypted(SqliteConnection source, string destination, ReadOnlySpan<byte> destinationKey)
    {
        RequireEncryptionActive(source); Export(source, destination, destinationKey);
    }

    /// <summary>Explicit conversion of a private, uploaded legacy snapshot, not a runtime plaintext fallback.
    /// The original company database must never be passed here; the trusted host controls both paths.</summary>
    public static void ImportLegacySnapshot(string privatePlaintextSnapshot, string encryptedDestination, ReadOnlySpan<byte> key)
    {
        Validate(privatePlaintextSnapshot, key); _ = Provider.Value;
        using (var input = File.OpenRead(privatePlaintextSnapshot))
        {
            Span<byte> header = stackalloc byte[16]; input.ReadExactly(header);
            if (!header.SequenceEqual("SQLite format 3\0"u8)) throw new InvalidDataException("The legacy snapshot is not a plaintext SQLite database.");
        }
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = privatePlaintextSnapshot, Mode = SqliteOpenMode.ReadWrite, Pooling = false, Cache = SqliteCacheMode.Private, DefaultTimeout = 5 }.ToString());
        source.Open();
        Execute(source, "PRAGMA temp_store=MEMORY; PRAGMA cipher_memory_security=ON; PRAGMA trusted_schema=OFF;");
        using (var command = source.CreateCommand())
        {
            command.CommandText = "PRAGMA integrity_check;";
            using (var rows = command.ExecuteReader())
                if (!rows.Read() || rows.GetString(0) != "ok" || rows.Read()) throw new InvalidDataException("The legacy database failed its integrity check.");
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type IN ('trigger','view') OR sql LIKE '%CREATE VIRTUAL TABLE%';";
            if (Convert.ToInt64(command.ExecuteScalar()) != 0)
                throw new InvalidDataException("The legacy snapshot contains unsupported executable schema objects. Review it before conversion.");
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN ('Users','Settings','Sales','Accounts','AuditLogs');";
            if (Convert.ToInt64(command.ExecuteScalar()) != 5) throw new InvalidDataException("This snapshot is not an initialized RetailForma company.");
        }
        Export(source, encryptedDestination, key);
    }

    private static void Export(SqliteConnection source, string destination, ReadOnlySpan<byte> destinationKey)
    {
        Validate(destination, destinationKey);
        // The source deliberately uses ReadWrite, never ReadWriteCreate; ATTACH inherits that flag.
        // Atomically reserve an empty destination so ATTACH cannot create/overwrite another file.
        using (new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        using var command = source.CreateCommand();
        command.CommandText = "PRAGMA user_version;"; var version = Convert.ToInt32(command.ExecuteScalar());
        command.CommandText = "ATTACH DATABASE $destination AS rf_snapshot KEY \"x'" + Convert.ToHexString(destinationKey) + "'\";";
        command.Parameters.AddWithValue("$destination", destination);
        try { command.ExecuteNonQuery(); }
        finally { command.CommandText = ""; command.Parameters.Clear(); }
        try
        {
            using (var transaction = source.BeginTransaction(deferred: true))
            {
                command.Transaction = transaction; command.CommandText = "SELECT sqlcipher_export('rf_snapshot');"; command.ExecuteNonQuery();
                command.CommandText = "PRAGMA rf_snapshot.user_version=" + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";";
                command.ExecuteNonQuery(); transaction.Commit();
            }
        }
        finally { command.Transaction = null; command.CommandText = "DETACH DATABASE rf_snapshot;"; command.ExecuteNonQuery(); }
        using var verify = OpenExisting(destination, destinationKey); VerifyIntegrity(verify);
    }

    private static string InitializeProvider()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlcipher());
        using var probe = new SqliteConnection("Data Source=:memory:;Pooling=False");
        probe.Open();
        using var command = probe.CreateCommand();
        command.CommandText = "PRAGMA cipher_version;";
        var version = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(version) || !version.StartsWith("4.19.0 ", StringComparison.Ordinal))
            throw new InvalidOperationException("The verified SQLCipher Community 4.19.0 provider is required. Plain SQLite is not permitted.");
        command.CommandText = "SELECT sqlite_compileoption_used('TEMP_STORE=2');";
        if (Convert.ToInt64(command.ExecuteScalar()) != 1)
            throw new InvalidOperationException("SQLCipher must be compiled with SQLITE_TEMP_STORE=2.");
        return version;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        try { command.CommandText = sql; command.ExecuteNonQuery(); }
        finally { command.CommandText = ""; }
    }

    private static void Validate(string path, ReadOnlySpan<byte> key)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("Use a full service-owned database path.");
        if (key.Length != 32) throw new ArgumentException("A 256-bit random database key is required.");
    }
}
