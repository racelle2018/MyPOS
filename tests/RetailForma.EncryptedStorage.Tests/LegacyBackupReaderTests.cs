using System.Security.Cryptography;
using System.Text.Json;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.EncryptedStorage;

namespace RetailForma.EncryptedStorage.Tests;

public sealed class LegacyBackupReaderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "RetailForma-legacy-conversion-test-" + Guid.NewGuid().ToString("N"));
    private const string Phrase = "Original legacy archive test password!";
    public LegacyBackupReaderTests() => Directory.CreateDirectory(root);

    private string Archive(string databaseName = "retailforma.db", bool executableSchema = false, string version = "1.2.3")
    {
        _ = SqlCipherConnections.ProviderVersion;
        var source = Path.Combine(root, "source-" + Guid.NewGuid().ToString("N") + ".db");
        using (var connection = new SqliteConnection($"Data Source={source};Pooling=False"))
        {
            connection.Open();
            using var db = new RetailFormaDbContext(new DbContextOptionsBuilder<RetailFormaDbContext>().UseSqlite(connection).Options);
            DbInitializer.Initialize(db);
            if (executableSchema) db.Database.ExecuteSqlRaw("CREATE TRIGGER Unexpected AFTER UPDATE ON Users BEGIN SELECT 1; END;");
            using var checkpoint = connection.CreateCommand(); checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);"; checkpoint.ExecuteNonQuery();
            checkpoint.CommandText = "PRAGMA journal_mode=DELETE;"; checkpoint.ExecuteScalar();
        }
        var bytes = File.ReadAllBytes(source); var archive = Path.Combine(root, Guid.NewGuid().ToString("N") + ".zip");
        using var zip = new ZipOutputStream(File.Create(archive)) { Password = Phrase };
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new { Format = 1, DatabaseBytes = bytes.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), CreatedUtc = DateTimeOffset.UtcNow, Purpose = "Manual", ApplicationVersion = version });
        foreach (var pair in new[] { ("manifest.json", manifest), (databaseName, bytes) })
        { zip.PutNextEntry(new ZipEntry(pair.Item1) { AESKeySize = 256, Size = pair.Item2.Length }); zip.Write(pair.Item2); zip.CloseEntry(); }
        zip.Finish(); return archive;
    }

    [Theory]
    [InlineData("retailforma.db")]
    [InlineData("mypos.db")]
    public void OriginalArchivesConvertOnlyInsidePrivateWorkAndRetainTheirPassphrase(string name)
    {
        var archive = Archive(name); var before = File.ReadAllBytes(archive);
        var work = Path.Combine(root, "private-work"); Directory.CreateDirectory(work);
        Assert.Throws<ArgumentException>(() => RecoveryBackupPackage.OpenCompatibleWithOwner(archive, work, Phrase + "wrong")); Assert.Empty(Directory.GetFiles(work));
        using (var recovered = RecoveryBackupPackage.OpenCompatibleWithOwner(archive, work, Phrase))
        {
            Assert.Single(Directory.GetFiles(work));
            Assert.DoesNotContain(Directory.GetFiles(work), path => Path.GetFileName(path).StartsWith("legacy-"));
            using var database = recovered.OpenDatabase(); SqlCipherConnections.VerifyIntegrity(database);
            using var command = database.CreateCommand(); command.CommandText = "SELECT count(*) FROM Users;"; Assert.Equal(1L, command.ExecuteScalar());
        }
        Assert.Empty(Directory.GetFiles(work)); Assert.Equal(before, File.ReadAllBytes(archive));
    }

    [Fact]
    public void ExecutableSchemaAndNewerArchivesAreRejectedWithoutRetainingPlaintextWork()
    {
        var work = Path.Combine(root, "private-work"); Directory.CreateDirectory(work);
        var executable = Archive(executableSchema: true);
        Assert.Throws<InvalidDataException>(() => LegacyBackupReader.Open(executable, work, Phrase)); Assert.Empty(Directory.GetFiles(work));
        var newer = Archive(version: "99.0.0");
        Assert.Throws<InvalidOperationException>(() => LegacyBackupReader.Open(newer, work, Phrase)); Assert.Empty(Directory.GetFiles(work));
    }

    [Fact]
    public void RawLegacyDatabaseBackupConvertsWithoutChangingTheOriginalOrRequiringAnArchivePassword()
    {
        _ = Archive(); var source = Assert.Single(Directory.GetFiles(root, "source-*.db")); var before = File.ReadAllBytes(source);
        var work = Path.Combine(root, "private-work"); Directory.CreateDirectory(work);
        using (var converted = RecoveryBackupPackage.OpenCompatibleWithOwner(source, work, ""))
        {
            using var db = converted.OpenDatabase(); SqlCipherConnections.VerifyIntegrity(db);
            Assert.Single(Directory.GetFiles(work));
        }
        Assert.Empty(Directory.GetFiles(work)); Assert.Equal(before, File.ReadAllBytes(source));
    }
    public void Dispose() => Directory.Delete(root, true);
}
