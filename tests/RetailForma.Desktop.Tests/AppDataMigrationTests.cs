using System.IO;
using Microsoft.Data.Sqlite;
using RetailForma.Desktop;
using Xunit;

namespace RetailForma.Desktop.Tests;

public sealed class AppDataMigrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "retailforma-migration-test-" + Guid.NewGuid().ToString("N"));
    private const string Password = "Migration test-only recovery passphrase!";
    private string Source => Path.Combine(root, "legacy");
    private string Target => Path.Combine(root, "RetailForma");
    public AppDataMigrationTests() => Directory.CreateDirectory(Source);
    public void Dispose() => Directory.Delete(root, recursive: true);
    private SqliteConnection Database(bool secret = true)
    {
        var connection = new SqliteConnection($"Data Source={Path.Combine(Source, "mypos.db")};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE Sales(Id TEXT, SaleDate TEXT, TotalAmount TEXT); CREATE TABLE Products(Id TEXT, Name TEXT, StockQty TEXT); CREATE TABLE Users(Id TEXT, Username TEXT, PasswordHash TEXT, Role INTEGER); CREATE TABLE Settings(Key TEXT, Value TEXT); CREATE TABLE JournalEntries(Id TEXT, Reference TEXT); INSERT INTO Sales VALUES('SALE-ID','2026-10-13','123.45'); INSERT INTO Users VALUES('USER-ID','owner','UNCHANGED-HASH',1); INSERT INTO JournalEntries VALUES('JOURNAL-ID','SALE-ID'); INSERT INTO Products VALUES('PRODUCT-ID','UNCHANGED PRODUCT','7'); INSERT INTO Settings VALUES('BackupRecoverySecret',$secret);";
        command.Parameters.AddWithValue("$secret", secret ? EncryptedBackup.ProtectPassword(Password) : "invalid-secret");
        command.ExecuteNonQuery();
        return connection;
    }
    [Fact]
    public void Copies_committed_wal_records_settings_pending_restore_and_verified_encrypted_safety_without_changing_source()
    {
        using var source = Database(); // Keep WAL open: migration must use SQLite, not raw file copying.
        Directory.CreateDirectory(Path.Combine(Source, "licensing"));
        File.WriteAllText(Path.Combine(Source, "licensing", "license.aes"), "OLD TEST TOKEN");
        BackupService.CreateVerifiedBackup(Path.Combine(Source, "mypos.db"), Path.Combine(Source, "restore-pending.db"));
        AppDataMigration.Migrate(Source, Target);
        Assert.True(File.Exists(Path.Combine(Source, "mypos.db")));
        Assert.Equal("OLD TEST TOKEN", File.ReadAllText(Path.Combine(Source, "licensing", "license.aes")));
        Assert.Equal("OLD TEST TOKEN", File.ReadAllText(Path.Combine(Target, "legacy-activation", "licensing", "license.aes")));
        Assert.False(File.Exists(Path.Combine(Target, "licensing", "license.aes")));
        Assert.Equal(File.ReadAllBytes(Path.Combine(Source, "restore-pending.db")), File.ReadAllBytes(Path.Combine(Target, "restore-pending.db")));
        BackupService.Verify(Path.Combine(Target, "restore-pending.db"));
        Assert.True(File.Exists(Path.Combine(Target, AppDataMigration.MarkerName)));
        using var migrated = new SqliteConnection($"Data Source={Path.Combine(Target, AppPaths.DatabaseName)};Pooling=False");
        migrated.Open();
        using var check = migrated.CreateCommand();
        check.CommandText = "SELECT TotalAmount FROM Sales WHERE Id='SALE-ID'"; Assert.Equal("123.45", check.ExecuteScalar());
        check.CommandText = "SELECT PasswordHash FROM Users WHERE Id='USER-ID'"; Assert.Equal("UNCHANGED-HASH", check.ExecuteScalar());
        check.CommandText = "SELECT Reference FROM JournalEntries WHERE Id='JOURNAL-ID'"; Assert.Equal("SALE-ID", check.ExecuteScalar());
        check.CommandText = "SELECT Value FROM Settings WHERE Key='BackupRecoverySecret'";
        Assert.Equal(Password, EncryptedBackup.UnprotectPassword((string)check.ExecuteScalar()!));
        var backup = Assert.Single(Directory.GetFiles(Path.Combine(Target, "backups"), "*.zip"));
        Assert.Equal("Safety", EncryptedBackup.Verify(backup, Password).Purpose);
    }
    [Fact]
    public void Existing_destination_is_not_merged_or_overwritten()
    {
        using var source = Database(); Directory.CreateDirectory(Target);
        var sentinel = Path.Combine(Target, "existing.txt"); File.WriteAllText(sentinel, "KEEP");
        Assert.Throws<IOException>(() => AppDataMigration.Migrate(Source, Target));
        Assert.Equal("KEEP", File.ReadAllText(sentinel));
    }
    [Fact]
    public void Invalid_recovery_secret_does_not_publish_or_create_a_blank_database()
    {
        using var source = Database(secret: false);
        Assert.Throws<InvalidOperationException>(() => AppDataMigration.Migrate(Source, Target));
        Assert.False(Directory.Exists(Target)); Assert.True(File.Exists(Path.Combine(Source, "mypos.db")));
    }
    [Fact]
    public void Missing_source_and_overlapping_targets_are_rejected()
    {
        Assert.Throws<FileNotFoundException>(() => AppDataMigration.Migrate(Source, Target));
        Assert.Throws<InvalidOperationException>(() => AppDataMigration.Migrate(Source, Path.Combine(Source, "nested")));
        Assert.False(Directory.Exists(Target));
    }
    [Fact]
    public void Failed_database_verification_preserves_original_and_removes_only_unpublished_stage()
    {
        using var source = Database();
        using var drop = source.CreateCommand(); drop.CommandText = "DROP TABLE Products"; drop.ExecuteNonQuery();
        Assert.Throws<InvalidDataException>(() => AppDataMigration.Migrate(Source, Target));
        Assert.False(Directory.Exists(Target));
        Assert.Empty(Directory.GetDirectories(root, "RetailForma.migration-*"));
        Assert.True(File.Exists(Path.Combine(Source, "mypos.db")));
    }
}
