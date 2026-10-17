using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Data.Sqlite;
using RetailForma.Desktop;
using Xunit;

namespace RetailForma.Desktop.Tests;

public sealed class EncryptedBackupTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "retailforma-encryption-test-" + Guid.NewGuid().ToString("N"));
    private const string Password = "Test-only portable passphrase 2026!";
    public EncryptedBackupTests() => Directory.CreateDirectory(folder);
    public void Dispose() => Directory.Delete(folder, true);

    private string Database()
    {
        var path = Path.Combine(folder, "retailforma.db");
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Sales(Id TEXT, SaleDate TEXT, TotalAmount TEXT); CREATE TABLE Products(Id TEXT, Name TEXT, StockQty TEXT); CREATE TABLE Users(Id TEXT, Username TEXT, PasswordHash TEXT, Role INTEGER); CREATE TABLE Settings(Key TEXT, Value TEXT); INSERT INTO Products(Name) VALUES('Portable recovery test');";
        command.ExecuteNonQuery();
        return path;
    }

    [Fact]
    public void Archive_is_aes256_and_can_be_restored_with_only_portable_password()
    {
        var database = Database();
        var archive = Path.Combine(folder, "backup.zip");
        EncryptedBackup.Create(database, archive, Password);
        using (var zip = new ZipFile(File.OpenRead(archive)))
        {
            Assert.Equal(2, zip.Count);
            Assert.All(zip.Cast<ZipEntry>(), e => { Assert.True(e.IsCrypted); Assert.Equal(256, e.AESKeySize); });
        }
        var otherProfile = Path.Combine(folder, "other-profile");
        BackupService.StageEncryptedRestore(archive, Password, otherProfile);
        BackupService.Verify(Path.Combine(otherProfile, "restore-pending.db"));
        Assert.Throws<InvalidOperationException>(() => BackupService.StageEncryptedRestore(archive, Password, otherProfile));
        Assert.DoesNotContain(Directory.GetFiles(folder), p => Path.GetFileName(p).StartsWith('.'));
    }

    [Fact]
    public void Legacy_archive_entry_can_still_be_verified_and_restored()
    {
        var database = Database(); var current = Path.Combine(folder, "current.zip");
        var legacy = Path.Combine(folder, "legacy.zip");
        EncryptedBackup.Create(database, current, Password);
        using (var input = new ZipFile(File.OpenRead(current)) { Password = Password })
        using (var output = new ZipOutputStream(File.Create(legacy)) { Password = Password })
        {
            foreach (ZipEntry entry in input)
            {
                output.PutNextEntry(new ZipEntry(entry.Name == "retailforma.db" ? "mypos.db" : entry.Name) { AESKeySize = 256, Size = entry.Size });
                using var content = input.GetInputStream(entry); content.CopyTo(output); output.CloseEntry();
            }
            output.Finish();
        }
        var restored = Path.Combine(folder, "restored-legacy.db");
        EncryptedBackup.Extract(legacy, restored, Password); BackupService.Verify(restored);
    }

    [Fact]
    public void Wrong_password_and_truncation_never_stage_a_restore_or_overwrite_database()
    {
        var original = Database();
        var originalBytes = File.ReadAllBytes(original);
        var archive = Path.Combine(folder, "backup.zip");
        EncryptedBackup.Create(original, archive, Password);
        Assert.ThrowsAny<Exception>(() => BackupService.StageEncryptedRestore(archive, "Incorrect passphrase!", folder));
        Assert.False(File.Exists(Path.Combine(folder, "restore-pending.db")));
        var truncated = Path.Combine(folder, "truncated.zip");
        var bytes = File.ReadAllBytes(archive);
        File.WriteAllBytes(truncated, bytes[..(bytes.Length / 2)]);
        Assert.ThrowsAny<Exception>(() => BackupService.StageEncryptedRestore(truncated, Password, folder));
        var damaged = Path.Combine(folder, "damaged.zip");
        bytes[bytes.Length / 2] ^= 0x40;
        File.WriteAllBytes(damaged, bytes);
        Assert.ThrowsAny<Exception>(() => BackupService.StageEncryptedRestore(damaged, Password, folder));
        Assert.Equal(originalBytes, File.ReadAllBytes(original));
        Assert.DoesNotContain(Directory.GetFiles(folder), p => Path.GetFileName(p).StartsWith('.'));
        Assert.Throws<IOException>(() => EncryptedBackup.Extract(archive, original, Password));
    }

    [Theory]
    [InlineData("../retailforma.db", true)]
    [InlineData("retailforma.db", false)]
    [InlineData("extra.txt", true)]
    public void Rejects_unsafe_paths_unencrypted_entries_and_extra_content(string entryName, bool encrypted)
    {
        var archive = Path.Combine(folder, "unsafe.zip");
        using (var output = new ZipOutputStream(File.Create(archive)))
        {
            if (encrypted) output.Password = Password;
            foreach (var name in new[] { "manifest.json", entryName })
            {
                output.PutNextEntry(new ZipEntry(name) { AESKeySize = encrypted ? 256 : 0 });
                output.Write(Encoding.UTF8.GetBytes("invalid"));
                output.CloseEntry();
            }
            output.Finish();
        }
        Assert.ThrowsAny<Exception>(() => BackupService.StageEncryptedRestore(archive, Password, folder));
        Assert.False(File.Exists(Path.Combine(folder, "restore-pending.db")));
    }

    [Fact]
    public void Restore_creates_encrypted_safety_copy_and_preserves_previous_data()
    {
        var database = Database();
        var archive = Path.Combine(folder, "backup.zip");
        EncryptedBackup.Create(database, archive, Password);
        using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Products(Name) VALUES('Later product');";
            command.ExecuteNonQuery();
        }
        BackupService.StageEncryptedRestore(archive, Password, folder);
        BackupService.ApplyPendingRestore(folder, Password);
        var safety = Assert.Single(Directory.GetFiles(Path.Combine(folder, "backups")));
        Assert.EndsWith(".zip", safety);
        var recovered = Path.Combine(folder, "safety.db");
        EncryptedBackup.Extract(safety, recovered, Password);
        using var check = new SqliteConnection($"Data Source={recovered};Pooling=False");
        check.Open();
        using var query = check.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM Products";
        Assert.Equal(2L, query.ExecuteScalar());
    }

    [Fact]
    public void Automatic_secret_is_protected_and_weak_new_passwords_are_rejected()
    {
        var secret = EncryptedBackup.ProtectPassword(Password);
        Assert.DoesNotContain(Password, secret);
        Assert.Equal(Password, EncryptedBackup.UnprotectPassword(secret));
        Assert.Throws<ArgumentException>(() => EncryptedBackup.ProtectPassword("short"));
        Assert.Throws<InvalidOperationException>(() => EncryptedBackup.UnprotectPassword(""));
    }

    [Fact]
    public void Passphrase_rejection_is_distinct_from_unreadable_zip()
    {
        var archive = Path.Combine(folder, "backup.zip"); EncryptedBackup.Create(Database(), archive, Password);
        var rejected = Assert.Throws<BackupRestoreException>(() => EncryptedBackup.Verify(archive, "Incorrect passphrase!"));
        Assert.Equal(BackupFailure.PassphraseRejected, rejected.Failure);
        Assert.Contains("passphrase was not accepted", rejected.Message);
        Assert.DoesNotContain("damaged", rejected.Message);
        var broken = Path.Combine(folder, "broken.zip"); File.WriteAllText(broken, "not a ZIP");
        Assert.Equal(BackupFailure.DamagedArchive, Assert.Throws<BackupRestoreException>(() => EncryptedBackup.Verify(broken, Password)).Failure);
        Assert.False(File.Exists(Path.Combine(folder, "restore-pending.db")));
    }

    private void RewriteArchive(string source, string target, Func<ZipEntry, byte[], byte[]> rewrite)
    {
        using var input = new ZipFile(File.OpenRead(source)) { Password = Password };
        using var output = new ZipOutputStream(File.Create(target)) { Password = Password };
        foreach (ZipEntry entry in input)
        {
            using var bytes = new MemoryStream(); using (var content = input.GetInputStream(entry)) content.CopyTo(bytes);
            var rewritten = rewrite(entry, bytes.ToArray());
            output.PutNextEntry(new ZipEntry(entry.Name) { AESKeySize = 256, Size = rewritten.Length });
            output.Write(rewritten); output.CloseEntry();
        }
        output.Finish();
    }

    [Theory]
    [InlineData("hash", BackupFailure.DamagedContents)]
    [InlineData("json", BackupFailure.DamagedContents)]
    [InlineData("version", BackupFailure.NewerVersion)]
    [InlineData("format", BackupFailure.UnsupportedPackage)]
    public void Authenticated_metadata_distinguishes_damage_version_and_format(string change, BackupFailure expected)
    {
        var source = Path.Combine(folder, "source.zip"); EncryptedBackup.Create(Database(), source, Password);
        var archive = Path.Combine(folder, "modified.zip");
        RewriteArchive(source, archive, (entry, bytes) =>
        {
            if (entry.Name != "manifest.json") return bytes;
            if (change == "json") return Encoding.UTF8.GetBytes("invalid-json");
            var manifest = System.Text.Json.JsonSerializer.Deserialize<EncryptedBackup.Manifest>(bytes)!;
            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(change switch
            {
                "hash" => manifest with { Sha256 = new string('0', 64) },
                "version" => manifest with { ApplicationVersion = "999.0.0" },
                _ => manifest with { Format = 999 }
            });
        });
        Assert.Equal(expected, Assert.Throws<BackupRestoreException>(() => BackupService.StageEncryptedRestore(archive, Password, folder)).Failure);
        Assert.False(File.Exists(Path.Combine(folder, "restore-pending.db")));
        Assert.DoesNotContain(Directory.GetFiles(folder), file => Path.GetFileName(file).StartsWith('.'));
    }

    [Fact]
    public void Missing_file_is_not_reported_as_wrong_password_or_damage()
    {
        Assert.Throws<FileNotFoundException>(() => EncryptedBackup.Verify(Path.Combine(folder, "missing.zip"), Password));
    }

    [Fact]
    public void Encrypted_metadata_authentication_failure_is_reported_without_guessing_the_cause()
    {
        var archive = Path.Combine(folder, "backup.zip"); EncryptedBackup.Create(Database(), archive, Password);
        long encryptedLength;
        using (var zip = new ZipFile(File.OpenRead(archive))) encryptedLength = zip.GetEntry("manifest.json").CompressedSize;
        var bytes = File.ReadAllBytes(archive);
        var nameLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26, 2));
        var extraLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28, 2));
        var lastAuthenticationByte = checked((int)(30 + nameLength + extraLength + encryptedLength - 1));
        bytes[lastAuthenticationByte] ^= 0x40;
        var damaged = Path.Combine(folder, "authentication-failure.zip"); File.WriteAllBytes(damaged, bytes);
        var failure = Assert.Throws<BackupRestoreException>(() => BackupService.StageEncryptedRestore(damaged, Password, folder));
        Assert.Equal(BackupFailure.CannotAuthenticate, failure.Failure);
        Assert.Contains("exact cause cannot be determined", failure.Message);
        Assert.False(File.Exists(Path.Combine(folder, "restore-pending.db")));
    }

    [Fact]
    public void Retention_deletes_only_verified_eligible_automatic_archives_in_the_exact_folder()
    {
        var source = Database();
        var disposable = Path.Combine(folder, "retailforma-auto-old.zip");
        var pending = Path.Combine(folder, "retailforma-auto-pending.zip");
        var manual = Path.Combine(folder, "retailforma-auto-misnamed-manual.zip");
        var safety = Path.Combine(folder, "pre-restore-safety.zip");
        var unknown = Path.Combine(folder, "retailforma-auto-corrupt.zip");
        var latest = Path.Combine(folder, "retailforma-auto-new.zip");
        EncryptedBackup.Create(source, disposable, Password, "Automatic");
        EncryptedBackup.Create(source, pending, Password, "Automatic");
        File.WriteAllText(pending + ".cloud-pending", "GoogleDrive");
        EncryptedBackup.Create(source, manual, Password);
        EncryptedBackup.Create(source, safety, Password, "Safety");
        File.WriteAllText(unknown, "not an archive");
        EncryptedBackup.Create(source, latest, Password, "Automatic");
        Assert.Equal(1, BackupRetention.Prune(folder, latest, Password));
        Assert.False(File.Exists(disposable));
        foreach (var preserved in new[] { pending, manual, safety, unknown, latest, source }) Assert.True(File.Exists(preserved));
        Assert.Throws<InvalidOperationException>(() => BackupRetention.Prune(Path.Combine(folder, "other"), latest, Password));
    }
}
