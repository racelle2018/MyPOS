using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using RetailForma.EncryptedStorage;
using RetailForma.Security;

namespace RetailForma.EncryptedStorage.Tests;

public sealed class EncryptedStorageTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "RetailForma-encryption-test-" + Guid.NewGuid().ToString("N"));
    public EncryptedStorageTests() => Directory.CreateDirectory(_folder);
    private string PathFor(string name) => Path.Combine(_folder, name);

    [Fact]
    public void EncryptedCrudWalRollbackAndDeveloperRecovery()
    {
        var path = PathFor("company.db");
        var key = RandomNumberGenerator.GetBytes(32);
        using var developer = RSA.Create(3072);
        var context = new RecoveryContext(Guid.NewGuid(), Guid.NewGuid(), RecoveryPurpose.DatabaseKey);
        var envelope = DeveloperRecoveryEnvelope.Seal(key, context, developer);
        const string secret = "CONFIDENTIAL PRODUCT RETAILFORMA 123456789";
        using (var db = SqlCipherConnections.CreateNew(path, key))
        {
            Assert.StartsWith("4.19.0 ", SqlCipherConnections.ProviderVersion);
            using var cmd = db.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE Items (Name TEXT NOT NULL); INSERT INTO Items VALUES ($name);";
            cmd.Parameters.AddWithValue("$name", secret);
            cmd.ExecuteNonQuery();
            using var walFile = new FileStream(path + "-wal", FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var walBytes = new MemoryStream();
            walFile.CopyTo(walBytes);
            var wal = walBytes.ToArray();
            Assert.False(Contains(wal, Encoding.UTF8.GetBytes(secret)));
            using (var tx = db.BeginTransaction())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE Items SET Name='should roll back';";
                cmd.ExecuteNonQuery();
                tx.Rollback();
            }
            cmd.Transaction = null;
            cmd.CommandText = "SELECT Name FROM Items;";
            Assert.Equal(secret, cmd.ExecuteScalar());
            Assert.DoesNotContain(Convert.ToHexString(key), db.ConnectionString, StringComparison.OrdinalIgnoreCase);
            SqlCipherConnections.VerifyIntegrity(db);
        }
        var file = File.ReadAllBytes(path);
        Assert.False(Contains(file, "SQLite format 3"u8.ToArray()));
        Assert.False(Contains(file, Encoding.UTF8.GetBytes(secret)));
        var recoveredKey = DeveloperRecoveryEnvelope.Open(envelope, context, developer);
        try
        {
            using var recovered = SqlCipherConnections.OpenExisting(path, recoveredKey);
            using var cmd = recovered.CreateCommand(); cmd.CommandText = "SELECT Name FROM Items;";
            Assert.Equal(secret, cmd.ExecuteScalar());
        }
        finally { CryptographicOperations.ZeroMemory(recoveredKey); CryptographicOperations.ZeroMemory(key); }
    }

    [Fact]
    public void WrongKeyMissingDatabaseAndExistingDestinationNeverFallBack()
    {
        var path = PathFor("company.db");
        var key = RandomNumberGenerator.GetBytes(32);
        using (SqlCipherConnections.CreateNew(path, key)) { }
        var before = File.ReadAllBytes(path);
        Assert.Throws<SqliteException>(() => SqlCipherConnections.OpenExisting(path, RandomNumberGenerator.GetBytes(32)));
        Assert.Throws<SqliteException>(() =>
        {
            using var unkeyed = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
            unkeyed.Open();
            using var command = unkeyed.CreateCommand(); command.CommandText = "SELECT count(*) FROM sqlite_master;";
            command.ExecuteScalar();
        });
        Assert.Throws<IOException>(() => SqlCipherConnections.CreateNew(path, key));
        Assert.Equal(before, File.ReadAllBytes(path));
        var absent = PathFor("missing.db");
        Assert.Throws<FileNotFoundException>(() => SqlCipherConnections.OpenExisting(absent, key));
        Assert.False(File.Exists(absent));
        var empty = PathFor("empty.db");
        using (File.Create(empty)) { }
        Assert.Throws<InvalidDataException>(() => SqlCipherConnections.OpenExisting(empty, key));
        Assert.Equal(0, new FileInfo(empty).Length);
        Assert.Throws<ArgumentException>(() => SqlCipherConnections.CreateNew(PathFor("invalid.db"), new byte[1]));
        Assert.False(File.Exists(PathFor("invalid.db")));
    }

    [Fact]
    public void PlaintextDatabaseIsRejectedRatherThanSilentlyUpgraded()
    {
        _ = SqlCipherConnections.ProviderVersion;
        var path = PathFor("plain.db");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            db.Open(); using var command = db.CreateCommand();
            command.CommandText = "CREATE TABLE Unencrypted (Id INTEGER);"; command.ExecuteNonQuery();
            Assert.Throws<InvalidDataException>(() => SqlCipherConnections.VerifyIntegrity(db));
        }
        var before = File.ReadAllBytes(path);
        Assert.Throws<SqliteException>(() => SqlCipherConnections.OpenExisting(path, RandomNumberGenerator.GetBytes(32)));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void CorruptedEncryptedPageFailsAuthenticationWithoutRepairingOrReplacingFile()
    {
        var path = PathFor("corrupted.db");
        var key = RandomNumberGenerator.GetBytes(32);
        using (var db = SqlCipherConnections.CreateNew(path, key))
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "CREATE TABLE Payload (Data BLOB); INSERT INTO Payload VALUES (zeroblob(12000));";
            cmd.ExecuteNonQuery();
        }
        using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            file.Seek(-1, SeekOrigin.End); var previous = file.ReadByte();
            file.Seek(-1, SeekOrigin.End); file.WriteByte((byte)(previous ^ 1));
        }
        var corrupted = File.ReadAllBytes(path);
        var error = Record.Exception(() =>
        {
            using var db = SqlCipherConnections.OpenExisting(path, key);
            SqlCipherConnections.VerifyIntegrity(db);
        });
        Assert.True(error is SqliteException or InvalidDataException);
        Assert.Equal(corrupted, File.ReadAllBytes(path));
    }

    private static bool Contains(byte[] bytes, byte[] value) => bytes.AsSpan().IndexOf(value) >= 0;
    public void Dispose() => Directory.Delete(_folder, recursive: true); // Only this test's generated GUID directory.
}
