using System.IO;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Entities;
using RetailForma.Core.Data;
using RetailForma.Desktop;
using Xunit;

namespace RetailForma.Desktop.Tests;

public sealed class BackupCopiesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "retailforma-copy-tests-" + Guid.NewGuid().ToString("N"));
    private const string Password = "Only test portable backup phrase!";
    public BackupCopiesTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, recursive: true);

    private string Archive()
    {
        var local = Path.Combine(root, "local"); Directory.CreateDirectory(local);
        var database = Path.Combine(local, "retailforma.db");
        using (var db = new RetailFormaDbContext(new DbContextOptionsBuilder<RetailFormaDbContext>()
                   .UseSqlite($"Data Source={database};Pooling=False").Options)) DbInitializer.Initialize(db);
        var archive = Path.Combine(local, "retailforma-test.zip");
        EncryptedBackup.Create(database, archive, Password);
        return archive;
    }

    [Fact]
    public void Both_provider_copies_are_verified_idempotent_and_never_claim_cloud_success()
    {
        var archive = Archive();
        BackupCopies.Destination[] destinations = [new("Google Drive", Path.Combine(root, "drive")), new("Dropbox", Path.Combine(root, "dropbox"))];
        foreach (var attempt in Enumerable.Range(0, 2))
        {
            var results = BackupCopies.Copy(archive, Password, destinations);
            Assert.Equal(2, results.Count);
            Assert.All(results, result => { Assert.True(result.Succeeded); Assert.Contains("not verified", result.Message); });
        }
        foreach (var destination in destinations)
        {
            var copy = Assert.Single(Directory.GetFiles(destination.Folder));
            Assert.EndsWith(".zip", copy);
            Assert.Equal(File.ReadAllBytes(archive), File.ReadAllBytes(copy));
        }
    }

    [Fact]
    public void One_failed_destination_does_not_prevent_other_copy_or_modify_local_backup()
    {
        var archive = Archive(); var original = File.ReadAllBytes(archive);
        var blocked = Path.Combine(root, "not-a-folder"); File.WriteAllText(blocked, "untouched");
        var results = BackupCopies.Copy(archive, Password, [new("Google Drive", blocked), new("Dropbox", Path.Combine(root, "dropbox"))]);
        Assert.False(results[0].Succeeded); Assert.True(results[1].Succeeded);
        Assert.Equal(original, File.ReadAllBytes(archive)); Assert.Equal("untouched", File.ReadAllText(blocked));
    }

    [Fact]
    public void Conflicting_destination_file_is_preserved_and_not_reported_as_verified()
    {
        var archive = Archive(); var destination = Path.Combine(root, "drive"); Directory.CreateDirectory(destination);
        var collision = Path.Combine(destination, Path.GetFileName(archive)); File.WriteAllText(collision, "existing different data");
        var result = Assert.Single(BackupCopies.Copy(archive, Password, [new("Google Drive", destination)]));
        Assert.False(result.Succeeded); Assert.Equal("existing different data", File.ReadAllText(collision));
        Assert.Single(Directory.GetFiles(destination));
    }

    [Theory]
    [InlineData("drive", "drive")]
    [InlineData("drive", "drive/nested")]
    [InlineData("local", "dropbox")]
    public void Rejects_overlapping_destinations_and_local_backup_folder(string left, string right)
    {
        Assert.Throws<ArgumentException>(() => BackupCopies.ValidateFolders(
            [new("Google Drive", Path.Combine(root, left)), new("Dropbox", Path.Combine(root, right))], Path.Combine(root, "local")));
    }

    [Fact]
    public void Failed_copy_remains_pending_and_retries_after_destination_recovers()
    {
        var installation = Path.Combine(root, "installation"); Directory.CreateDirectory(installation);
        var destination = Path.Combine(root, "blocked"); File.WriteAllText(destination, "blocker fixture");
        var options = new DbContextOptionsBuilder<RetailFormaDbContext>()
            .UseSqlite($"Data Source={Path.Combine(installation, "retailforma.db")};Pooling=False").Options;
        using (var db = new RetailFormaDbContext(options))
        {
            DbInitializer.Initialize(db);
            db.Settings.AddRange(new Setting { Key = "BackupRecoverySecret", Value = EncryptedBackup.ProtectPassword(Password) },
                new Setting { Key = "Backup.GoogleDrive.Enabled", Value = "true" },
                new Setting { Key = "Backup.GoogleDrive.Folder", Value = destination });
            db.SaveChanges();
        }
        var archive = BackupService.CreateBackupForInstallation(installation, automatic: true);
        Assert.NotNull(archive); EncryptedBackup.Verify(archive, Password);
        Assert.True(File.Exists(archive + ".cloud-pending"));
        Assert.Contains("Google Drive", BackupService.LastError);
        // Remove only this temporary blocker fixture; the recovery archive remains untouched.
        File.Delete(destination); Directory.CreateDirectory(destination);
        BackupService.RetryPendingFolderCopies(installation);
        Assert.False(File.Exists(archive + ".cloud-pending"));
        Assert.True(File.Exists(Path.Combine(destination, Path.GetFileName(archive))));
        Assert.True(Assert.Single(BackupService.LastCopyResults).Succeeded);
        Assert.Null(BackupService.LastError);
    }
}
