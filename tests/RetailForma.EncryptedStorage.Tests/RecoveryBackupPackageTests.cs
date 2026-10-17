using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using RetailForma.EncryptedStorage;

namespace RetailForma.EncryptedStorage.Tests;

public sealed class RecoveryBackupPackageTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "RetailForma-dual-backup-test-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] liveKey = RandomNumberGenerator.GetBytes(32);
    private readonly RSA developer = RSA.Create(3072);
    private const string Owner = "Original owner backup phrase!";
    private string PathFor(string name) => Path.Combine(folder, name);
    public RecoveryBackupPackageTests() => Directory.CreateDirectory(folder);
    private SqliteConnection CreateLive()
    {
        var db = SqlCipherConnections.CreateNew(PathFor("live.db"), liveKey);
        using var command = db.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE Evidence (Value TEXT); INSERT INTO Evidence VALUES ('SOURCE WAL CONTENT'); PRAGMA user_version=17;";
        command.ExecuteNonQuery(); return db;
    }
    private static void AssertEvidence(RecoveryBackupPackage.RecoveredBackup recovered)
    {
        using var db = recovered.OpenDatabase(); using var command = db.CreateCommand();
        command.CommandText = "SELECT Value FROM Evidence;"; Assert.Equal("SOURCE WAL CONTENT", command.ExecuteScalar());
        command.CommandText = "PRAGMA user_version;"; Assert.Equal(17L, command.ExecuteScalar());
    }

    [Fact]
    public void ConsistentWalSnapshotHasIndependentOwnerAndDeveloperRecoveryWithoutPlaintextWorkFiles()
    {
        using var live = CreateLive(); var archive = PathFor("manual.zip");
        RecoveryBackupPackage.Create(live, archive, folder, Owner, developer, Guid.NewGuid());
        Assert.Equal(2, RecoveryBackupPackage.ReadHeader(archive).Format);
        using (var owner = RecoveryBackupPackage.OpenWithOwner(archive, folder, Owner)) AssertEvidence(owner);
        using (var technical = RecoveryBackupPackage.OpenWithDeveloper(archive, folder, developer)) AssertEvidence(technical);
        Assert.Throws<CryptographicException>(() => RecoveryBackupPackage.OpenWithOwner(archive, folder, "A wrong but long enough phrase"));
        using var other = RSA.Create(3072);
        Assert.Throws<CryptographicException>(() => RecoveryBackupPackage.OpenWithDeveloper(archive, folder, other));
        Assert.Empty(Directory.GetFiles(folder, "recovered-*.db"));
        Assert.Empty(Directory.GetFiles(folder, "snapshot-*.db"));
        using var command = live.CreateCommand(); command.CommandText = "SELECT Value FROM Evidence;";
        Assert.Equal("SOURCE WAL CONTENT", command.ExecuteScalar());
    }

    [Fact]
    public void ProgrammerCanReprotectForgottenOwnerPhraseWithoutChangingOriginalArchive()
    {
        using var live = CreateLive(); var original = PathFor("original.zip"); var recovered = PathFor("recovered.zip");
        RecoveryBackupPackage.Create(live, original, folder, Owner, developer, Guid.NewGuid());
        var before = File.ReadAllBytes(original); var phrase = "New owner phrase after recovery!";
        RecoveryBackupPackage.ReprotectWithDeveloper(original, recovered, folder, developer, phrase);
        Assert.Equal(before, File.ReadAllBytes(original));
        Assert.NotEqual(RecoveryBackupPackage.ReadHeader(original).BackupId, RecoveryBackupPackage.ReadHeader(recovered).BackupId);
        using (var reopened = RecoveryBackupPackage.OpenWithOwner(recovered, folder, phrase)) AssertEvidence(reopened);
        using (var reopened = RecoveryBackupPackage.OpenWithDeveloper(recovered, folder, developer)) AssertEvidence(reopened);
        Assert.Throws<CryptographicException>(() => RecoveryBackupPackage.OpenWithOwner(recovered, folder, Owner));
        Assert.Throws<IOException>(() => RecoveryBackupPackage.ReprotectWithDeveloper(original, recovered, folder, developer, phrase));
    }

    [Fact]
    public void ExistingSnapshotOrArchiveCannotBeOverwritten()
    {
        using var live = CreateLive(); var target = PathFor("already.db"); File.WriteAllText(target, "PRESERVE");
        Assert.Throws<IOException>(() => SqlCipherConnections.ExportEncrypted(live, target, RandomNumberGenerator.GetBytes(32)));
        Assert.Equal("PRESERVE", File.ReadAllText(target));
        var archive = PathFor("already.zip"); File.WriteAllText(archive, "PRESERVE");
        Assert.Throws<IOException>(() => RecoveryBackupPackage.Create(live, archive, folder, Owner, developer, Guid.NewGuid()));
        Assert.Equal("PRESERVE", File.ReadAllText(archive));
    }
    public void Dispose() { developer.Dispose(); CryptographicOperations.ZeroMemory(liveKey); Directory.Delete(folder, true); }
}
