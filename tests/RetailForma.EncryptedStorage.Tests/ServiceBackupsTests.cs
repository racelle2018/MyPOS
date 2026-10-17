using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Entities;
using RetailForma.DataService;
using RetailForma.ServiceApplication;

namespace RetailForma.EncryptedStorage.Tests;

public sealed class ServiceBackupsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "RetailForma-backup-service-test-" + Guid.NewGuid().ToString("N"));
    private readonly RSA key = RSA.Create(3072);
    private const string Password = "Individual test technician password!";
    private const string Phrase = "Owner backup recovery test passphrase!";
    private readonly ServiceDatabase storage;
    private readonly ApplicationOperations operations;
    private readonly ServiceBackups backups;
    private readonly ApplicationSession session = new("TEST-OPERATOR-SID");
    public ServiceBackupsTests()
    {
        Directory.CreateDirectory(root);
        storage = new(root, key.ExportSubjectPublicKeyInfoPem(), () => { }, Security);
        storage.ProvisionNew(new("technician", "Test Technician", Password, Password, "ISOLATED COMPANY"), "TEST-OPERATOR");
        operations = new(storage.Open, () => { }); backups = new(storage, operations); Login(session);
    }
    private void Login(ApplicationSession target) => operations.Execute(target, Operation.Login, WireJson.Element(new LoginInput("technician", Password)));
    private T Call<T>(Operation operation, object input) => Assert.IsType<T>(backups.Execute(session, operation, WireJson.Element(input)));
    private void Configure()
    {
        var state = Call<BackupPhraseState>(Operation.BackupPhraseState, new { });
        Call<BackupPhraseState>(Operation.ChangeBackupPhrase, new ChangeBackupPhraseInput("", Phrase, Phrase, state.Version));
    }

    [Fact]
    public void ChangingPhraseRequiresCurrentValueVersionAndMatchingConfirmation()
    {
        Configure(); var state = Call<BackupPhraseState>(Operation.BackupPhraseState, new { }); Assert.True(state.Configured);
        Assert.Throws<ArgumentException>(() => Call<BackupPhraseState>(Operation.ChangeBackupPhrase, new ChangeBackupPhraseInput("wrong", Phrase + "new", Phrase + "new", state.Version)));
        Assert.Equal(state, Call<BackupPhraseState>(Operation.BackupPhraseState, new { }));
        Assert.Throws<ArgumentException>(() => Call<BackupPhraseState>(Operation.ChangeBackupPhrase, new ChangeBackupPhraseInput(Phrase, Phrase + "new", Phrase + "bad", state.Version)));
        var changed = Call<BackupPhraseState>(Operation.ChangeBackupPhrase, new ChangeBackupPhraseInput(Phrase, Phrase + "new", Phrase + "new", state.Version));
        Assert.NotEqual(state.Version, changed.Version);
        Assert.Throws<ArgumentException>(() => Call<BackupPhraseState>(Operation.ChangeBackupPhrase, new ChangeBackupPhraseInput(Phrase, Phrase, Phrase, state.Version)));
        using var db = storage.Open();
        Assert.DoesNotContain(Phrase, db.Settings.Find("Security.Backup.AutomationSecret")!.Value);
        Assert.DoesNotContain(Phrase, string.Join("|", db.AuditLogs.Select(a => a.Details).ToArray()));
    }

    [Fact]
    public void BackupTransfersAreConnectionBoundAndLockedSessionsCannotContinue()
    {
        Configure(); var download = Call<BackupDownload>(Operation.CreateBackup, new CreateBackupInput());
        var other = new ApplicationSession(session.WindowsSid); Login(other);
        Assert.Throws<UnauthorizedAccessException>(() => backups.Execute(other, Operation.ReadBackupChunk, WireJson.Element(new BackupChunkInput(download.TransferId, 0))));
        operations.Execute(session, Operation.Lock, WireJson.Element(new { }));
        Assert.Throws<UnauthorizedAccessException>(() => Call<BackupChunk>(Operation.ReadBackupChunk, new BackupChunkInput(download.TransferId, 0)));
        operations.Execute(session, Operation.Unlock, WireJson.Element(new PasswordInput(Password)));
        using var bytes = new MemoryStream();
        for (long offset = 0; offset < download.Length;)
        {
            var chunk = Call<BackupChunk>(Operation.ReadBackupChunk, new BackupChunkInput(download.TransferId, offset));
            Assert.InRange(chunk.Bytes.Length, 1, ServiceBackups.ChunkBytes); bytes.Write(chunk.Bytes); offset += chunk.Bytes.Length;
        }
        Assert.Equal(download.Sha256, Convert.ToHexString(SHA256.HashData(bytes.ToArray())));
        backups.Execute(session, Operation.FinishBackupDownload, WireJson.Element(new IdInput(download.TransferId)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "Work")));
    }

    [Fact]
    public void RestoreRequiresCompleteVerifiedUploadPreservesSafetyAndRevokesEverySession()
    {
        Configure(); var download = Call<BackupDownload>(Operation.CreateBackup, new CreateBackupInput());
        using var archive = new MemoryStream();
        for (long offset = 0; offset < download.Length;)
        {
            var chunk = Call<BackupChunk>(Operation.ReadBackupChunk, new BackupChunkInput(download.TransferId, offset)); archive.Write(chunk.Bytes); offset += chunk.Bytes.Length;
        }
        backups.Execute(session, Operation.FinishBackupDownload, WireJson.Element(new IdInput(download.TransferId)));
        using (var db = storage.Open()) { db.Products.Add(new Product { Name = "ADDED AFTER BACKUP", Unit = "PC", Category = "TEST" }); db.SaveChanges(); }
        var upload = Call<Guid>(Operation.BeginRestoreUpload, new RestoreUploadInput(archive.Length, download.Sha256));
        Assert.Throws<InvalidOperationException>(() => Call<RestorePreview>(Operation.PrepareRestore, new PrepareRestoreInput(upload, Phrase)));
        var bytes = archive.ToArray();
        for (var offset = 0; offset < bytes.Length;)
        {
            var count = Math.Min(ServiceBackups.ChunkBytes, bytes.Length - offset);
            Call<long>(Operation.WriteRestoreChunk, new RestoreChunkInput(upload, offset, bytes.AsSpan(offset, count).ToArray())); offset += count;
        }
        var rejected = Assert.Throws<ArgumentException>(() => Call<RestorePreview>(Operation.PrepareRestore, new PrepareRestoreInput(upload, Phrase + "wrong")));
        Assert.Contains("passphrase", rejected.Message, StringComparison.OrdinalIgnoreCase);
        using (var db = storage.Open()) Assert.True(db.Products.Any(p => p.Name == "ADDED AFTER BACKUP"));
        var preview = Call<RestorePreview>(Operation.PrepareRestore, new PrepareRestoreInput(upload, Phrase));
        Assert.Equal("ISOLATED COMPANY", preview.Company);
        var other = new ApplicationSession("OTHER-WINDOWS-SID"); Login(other);
        backups.Execute(session, Operation.CommitRestore, WireJson.Element(new IdInput(upload)));
        Assert.Throws<UnauthorizedAccessException>(() => operations.Execute(other, Operation.Products, WireJson.Element(new { })));
        Assert.Throws<UnauthorizedAccessException>(() => operations.Execute(session, Operation.CurrentUser, WireJson.Element(new { })));
        using (var db = storage.Open())
        {
            Assert.False(db.Products.Any(p => p.Name == "ADDED AFTER BACKUP"));
            Assert.Null(db.Settings.Find("Security.Backup.AutomationSecret"));
            Assert.Single(db.AuditLogs.Where(a => a.Action == "BackupRestored"));
        }
        var safety = Assert.Single(Directory.GetDirectories(Path.Combine(root, "Safety")));
        Assert.True(File.Exists(Path.Combine(safety, "retailforma.db"))); Assert.True(File.Exists(Path.Combine(safety, "key-state.json")));
        Assert.True(File.Exists(Path.Combine(safety, "replaced.db")));
    }

    [Fact]
    public void MalformedChunksAndChecksumFailuresNeverChangeTheLiveDatabase()
    {
        var before = storage.Status();
        var id = Call<Guid>(Operation.BeginRestoreUpload, new RestoreUploadInput(128, new string('0', 64)));
        Assert.Throws<ArgumentException>(() => Call<long>(Operation.WriteRestoreChunk, new RestoreChunkInput(id, 1, new byte[128])));
        Assert.Throws<ArgumentException>(() => Call<long>(Operation.WriteRestoreChunk, new RestoreChunkInput(id, 0, new byte[ServiceBackups.ChunkBytes + 1])));
        Call<long>(Operation.WriteRestoreChunk, new RestoreChunkInput(id, 0, new byte[128]));
        Assert.Throws<InvalidDataException>(() => Call<RestorePreview>(Operation.PrepareRestore, new PrepareRestoreInput(id, Phrase)));
        Assert.Equal(before, storage.Status());
        backups.Execute(session, Operation.CancelRestore, WireJson.Element(new IdInput(id)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "Work")));
    }

    [Fact]
    public void CashierCanDownloadOnlyScheduledEncryptedBackupAndCannotManagePhraseOrRestore()
    {
        Configure();
        using (var db = storage.Open())
        {
            db.Users.Add(new User { Username = "cashier", FullName = "Cashier", Role = UserRole.Cashier,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) }); db.SaveChanges();
        }
        var cashier = new ApplicationSession("CASHIER-WINDOWS-SID"); operations.Execute(cashier, Operation.Login, WireJson.Element(new LoginInput("cashier", Password)));
        Assert.Throws<UnauthorizedAccessException>(() => backups.Execute(cashier, Operation.BackupPhraseState, WireJson.Element(new { })));
        Assert.Throws<UnauthorizedAccessException>(() => backups.Execute(cashier, Operation.CreateBackup, WireJson.Element(new CreateBackupInput())));
        var package = Assert.IsType<BackupDownload>(backups.Execute(cashier, Operation.CreateBackup, WireJson.Element(new CreateBackupInput(true))));
        Assert.IsType<BackupChunk>(backups.Execute(cashier, Operation.ReadBackupChunk, WireJson.Element(new BackupChunkInput(package.TransferId, 0))));
        Assert.Throws<UnauthorizedAccessException>(() => backups.Execute(cashier, Operation.BeginRestoreUpload, WireJson.Element(new RestoreUploadInput(128, new string('0', 64)))));
        backups.Execute(cashier, Operation.FinishBackupDownload, WireJson.Element(new IdInput(package.TransferId)));
        Assert.Throws<InvalidOperationException>(() => backups.Execute(cashier, Operation.CreateBackup, WireJson.Element(new CreateBackupInput(true))));
    }

    private static DirectorySecurity Security()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var security = new DirectorySecurity(); security.SetOwner(identity.User!); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow)); return security;
    }
    public void Dispose() { backups.Dispose(); storage.Dispose(); key.Dispose(); Directory.Delete(root, true); }
}
