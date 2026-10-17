using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;
using RetailForma.EncryptedStorage;
using RetailForma.ServiceApplication;

namespace RetailForma.DataService;

/// <summary>Operation-specific, connection-bound transfers. No client filesystem paths or database keys cross IPC.</summary>
public sealed class ServiceBackups(ServiceDatabase storage, ApplicationOperations operations, TimeProvider? time = null) : IDisposable
{
    private const string Secret = "Security.Backup.AutomationSecret";
    public const int ChunkBytes = 192 * 1024;
    private const long MaximumArchiveBytes = RecoveryBackupPackage.MaximumDatabaseBytes + 1024 * 1024;
    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private readonly Dictionary<Guid, Transfer> transfers = [];
    private readonly Dictionary<Guid, (int Count, long Since)> phraseFailures = [];
    private sealed class Transfer(Guid id, ApplicationSession session, Guid actor, string directory, bool upload, long length, string sha, long started)
    {
        public Guid Id = id;
        public ApplicationSession Session = session;
        public Guid Actor = actor;
        public string Directory = directory;
        public string Path => System.IO.Path.Combine(Directory, Upload ? "input.zip" : "output.zip");
        public bool Upload = upload;
        public long Length = length;
        public string Sha = sha;
        public long Started = started;
        public long Written;
        public RecoveryBackupPackage.RecoveredBackup? Prepared;
        public bool Automatic;
    }

    public object? Execute(ApplicationSession session, Operation operation, JsonElement payload)
    {
        // Re-read persisted POS access and session state for every chunk and commit, not only at transfer creation.
        var actor = (ClientUser)operations.Execute(session, Operation.CurrentUser, WireJson.Element(new { }))!;
        var automated = operation == Operation.CreateBackup && WireJson.Read<CreateBackupInput>(payload).Automatic;
        var automaticDownload = operation switch
        {
            Operation.ReadBackupChunk => transfers.TryGetValue(WireJson.Read<BackupChunkInput>(payload).TransferId, out var reading) && reading.Automatic,
            Operation.FinishBackupDownload => transfers.TryGetValue(WireJson.Read<IdInput>(payload).Id, out var finishing) && finishing.Automatic,
            _ => false
        };
        if ((!UserAccess.IsAdministrator(new User { Role = actor.Role, IsActive = actor.IsActive }) && !automated && !automaticDownload) || actor.MustChangePassword)
            throw new UnauthorizedAccessException("Only an unlocked Owner or System Administrator can manage backups.");
        Expire();
        switch (operation)
        {
            case Operation.BackupPhraseState: return PhraseState();
            case Operation.VerifyBackupPhrase:
                var verify = WireJson.Read<VerifyBackupPhraseInput>(payload); VerifyCurrent(actor.Id, verify.Current, verify.Version); return null;
            case Operation.ChangeBackupPhrase: ChangePhrase(actor.Id, WireJson.Read<ChangeBackupPhraseInput>(payload)); return PhraseState();
            case Operation.CreateBackup: return Create(session, actor.Id, WireJson.Read<CreateBackupInput>(payload));
            case Operation.ReadBackupChunk:
                var read = WireJson.Read<BackupChunkInput>(payload); var download = Find(read.TransferId, session, actor.Id, false);
                if (read.Offset < 0 || read.Offset >= download.Length) throw new ArgumentException("Invalid backup chunk offset.");
                using (var input = File.OpenRead(download.Path))
                {
                    input.Position = read.Offset; var bytes = new byte[(int)Math.Min(ChunkBytes, download.Length - read.Offset)]; input.ReadExactly(bytes);
                    return new BackupChunk(read.Offset, bytes);
                }
            case Operation.FinishBackupDownload:
                var finished = Find(WireJson.Read<IdInput>(payload).Id, session, actor.Id, false);
                if (finished.Automatic)
                {
                    using var completed = storage.Open();
                    var row = completed.Settings.Find("Security.Backup.LastAutomaticUtc"); var value = clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                    if (row == null) completed.Settings.Add(new Setting { Key = "Security.Backup.LastAutomaticUtc", Value = value }); else row.Value = value;
                    Audit(completed, actor.Id, "BackupDownloadConfirmed", "Client confirmed the encrypted automatic archive was saved and matched its service-verified hash. Cloud completion not verified.");
                }
                Remove(finished); return null;
            case Operation.BeginRestoreUpload: return BeginUpload(session, actor.Id, WireJson.Read<RestoreUploadInput>(payload));
            case Operation.WriteRestoreChunk:
                var write = WireJson.Read<RestoreChunkInput>(payload); var upload = Find(write.TransferId, session, actor.Id, true);
                if (upload.Prepared != null || write.Bytes == null || write.Bytes.Length is < 1 or > ChunkBytes || write.Offset != upload.Written || write.Bytes.LongLength > upload.Length - upload.Written)
                    throw new ArgumentException("Restore chunks must be bounded, consecutive and inside the declared file size.");
                using (var output = new FileStream(upload.Path, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    if (output.Length != upload.Written) throw new InvalidDataException("The private restore upload changed unexpectedly.");
                    output.Position = upload.Written; output.Write(write.Bytes); output.Flush(true); upload.Written += write.Bytes.Length;
                }
                return upload.Written;
            case Operation.PrepareRestore: return Prepare(session, actor.Id, WireJson.Read<PrepareRestoreInput>(payload));
            case Operation.CommitRestore:
                var restore = Find(WireJson.Read<IdInput>(payload).Id, session, actor.Id, true);
                if (restore.Prepared == null) throw new InvalidOperationException("Validate and review the restore file first.");
                // Revoke every connection even if a post-replacement verification reports an error.
                // Clients must sign in to the restored company; no transaction is retried automatically.
                try { storage.RestoreVerified(restore.Prepared, actor.Id, session.WindowsSid); }
                finally { operations.InvalidateAllSessions(); Dispose(); }
                return null;
            case Operation.CancelRestore:
                var cancelled = Find(WireJson.Read<IdInput>(payload).Id, session, actor.Id, true); Remove(cancelled); return null;
            default: throw new UnauthorizedAccessException("Unsupported backup operation.");
        }
    }

    private BackupPhraseState PhraseState()
    {
        using var db = storage.Open(); var secret = db.Settings.Find(Secret)?.Value ?? "";
        return new(secret.Length > 0, Version(secret));
    }
    private byte[] Entropy => SHA256.HashData(Encoding.UTF8.GetBytes("RetailForma.Backup.Automation.v2|" + storage.InstallationId.ToString("N")));
    private string Unprotect(string secret)
    {
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(secret), Entropy, DataProtectionScope.CurrentUser);
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private void VerifyCurrent(Guid actor, string current, string version)
    {
        if (current == null || current.Length > 256 || version == null || version.Length != 64) throw new ArgumentException("Invalid passphrase input.");
        if (phraseFailures.TryGetValue(actor, out var failures) && failures.Count >= 5 && clock.GetElapsedTime(failures.Since) < TimeSpan.FromMinutes(1))
            throw new UnauthorizedAccessException("Too many passphrase attempts. Wait one minute.");
        using var db = storage.Open(); var secret = db.Settings.Find(Secret)?.Value ?? "";
        if (Version(secret) != version) throw new ArgumentException("The saved passphrase changed. Reopen the dialog.");
        if (secret.Length == 0) throw new ArgumentException("No backup passphrase is configured. Reopen the dialog to set one.");
        string expected;
        try { expected = Unprotect(secret); }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        { throw new InvalidOperationException("The service cannot unlock the saved automation secret. It has not been replaced; use authorized recovery."); }
        var enteredHash = SHA256.HashData(Encoding.UTF8.GetBytes(current));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        if (!CryptographicOperations.FixedTimeEquals(enteredHash, expectedHash))
        {
            var count = phraseFailures.TryGetValue(actor, out failures) && clock.GetElapsedTime(failures.Since) < TimeSpan.FromMinutes(1) ? failures.Count + 1 : 1;
            phraseFailures[actor] = (count, count == 1 ? clock.GetTimestamp() : failures.Since);
            Audit(db, actor, "BackupPassphraseChangeDenied", "Current passphrase rejected; no values recorded.");
            throw new ArgumentException("The current backup passphrase is incorrect. It has not been changed.");
        }
        phraseFailures.Remove(actor);
    }
    private void ChangePhrase(Guid actor, ChangeBackupPhraseInput input)
    {
        if (input.Next != input.Confirmation) throw new ArgumentException("The new backup passphrases do not match.");
        RetailForma.Security.OwnerRecoveryEnvelope.ValidatePassphrase(input.Next);
        var saved = PhraseState();
        if (saved.Version != input.Version) throw new ArgumentException("The saved passphrase changed. Reopen the dialog.");
        if (saved.Configured) VerifyCurrent(actor, input.Current, input.Version);
        else if (!string.IsNullOrEmpty(input.Current)) throw new ArgumentException("The passphrase is not configured. Reopen the Set passphrase dialog.");
        var bytes = Encoding.UTF8.GetBytes(input.Next);
        try
        {
            var protectedValue = Convert.ToBase64String(ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser));
            using var db = storage.Open(); using var tx = db.Database.BeginTransaction();
            var row = db.Settings.Find(Secret);
            if (row == null) db.Settings.Add(new Setting { Key = Secret, Value = protectedValue }); else row.Value = protectedValue;
            Audit(db, actor, saved.Configured ? "BackupPassphraseChanged" : "BackupPassphraseConfigured", "Future backups use the new passphrase. Existing archives retain their original passphrase; values omitted."); tx.Commit();
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private BackupDownload Create(ApplicationSession session, Guid actor, CreateBackupInput input)
    {
        string phrase;
        using (var db = storage.Open())
        {
            if (input.Automatic && DateTimeOffset.TryParse(db.Settings.Find("Security.Backup.LastAutomaticUtc")?.Value,
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var last) &&
                clock.GetUtcNow() - last < TimeSpan.FromHours(12))
                throw new InvalidOperationException("A scheduled encrypted backup was already created within the last 12 hours.");
            var secret = db.Settings.Find(Secret)?.Value;
            if (string.IsNullOrEmpty(secret)) throw new InvalidOperationException("Set the backup passphrase before creating backups.");
            phrase = Unprotect(secret);
        }
        var transfer = Allocate(session, actor, false, 0, "");
        transfer.Automatic = input.Automatic;
        try
        {
            storage.CreatePortableBackup(transfer.Path, transfer.Directory, phrase, input.Automatic);
            transfer.Length = new FileInfo(transfer.Path).Length; transfer.Sha = HashFile(transfer.Path);
            using var db = storage.Open();
            Audit(db, actor, "BackupCreated", $"Verified encrypted {(input.Automatic ? "automatic" : "manual")} backup; transfer {transfer.Id:N}. Local/cloud copies have not been confirmed.");
            return new(transfer.Id, transfer.Length, transfer.Sha, DateTimeOffset.UtcNow);
        }
        catch { Remove(transfer); throw; }
    }
    private Guid BeginUpload(ApplicationSession session, Guid actor, RestoreUploadInput input)
    {
        if (input.Length is < 128 or > MaximumArchiveBytes || input.Sha256 == null || input.Sha256.Length != 64 || input.Sha256.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Select a supported backup file with a valid size/checksum.");
        StorageSpace.Require(storage.WorkDirectory, checked(input.Length * 3));
        var transfer = Allocate(session, actor, true, input.Length, input.Sha256.ToUpperInvariant());
        using (new FileStream(transfer.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        return transfer.Id;
    }
    private RestorePreview Prepare(ApplicationSession session, Guid actor, PrepareRestoreInput input)
    {
        if (input.Passphrase == null || input.Passphrase.Length > 256) throw new ArgumentException("Enter the backup's original passphrase.");
        var transfer = Find(input.TransferId, session, actor, true);
        if (transfer.Prepared != null) throw new InvalidOperationException("The restore file is already prepared.");
        if (transfer.Written != transfer.Length) throw new InvalidOperationException("Upload the complete backup before validation.");
        if (HashFile(transfer.Path) != transfer.Sha) throw new InvalidDataException("The uploaded file checksum does not match. Select and upload the original backup again.");
        RecoveryBackupPackage.RecoveredBackup recovered;
        try { recovered = RecoveryBackupPackage.OpenCompatibleWithOwner(transfer.Path, transfer.Directory, input.Passphrase); }
        catch (CryptographicException)
        { throw new ArgumentException("The passphrase could not unlock this backup. It may be incorrect, or the recovery header may have been altered. The live company was not changed."); }
        try
        {
            using var connection = recovered.OpenDatabase();
            using var db = new RetailFormaDbContext(new DbContextOptionsBuilder<RetailFormaDbContext>().UseSqlite(connection, contextOwnsConnection: false).Options);
            CompanySchema.BeforeMigration(db); DbInitializer.Initialize(db); CompanySchema.AfterMigration(db); SqlCipherConnections.VerifyIntegrity(connection);
            var preview = new RestorePreview(transfer.Id, db.Settings.Find("CompanyName")?.Value ?? "", db.Users.Count(), db.Sales.Count(), transfer.Sha);
            transfer.Prepared = recovered; return preview;
        }
        catch { recovered.Dispose(); throw; }
    }
    private Transfer Allocate(ApplicationSession session, Guid actor, bool upload, long length, string sha)
    {
        if (transfers.Count >= 8 || transfers.Values.Count(t => t.Actor == actor) >= 2) throw new InvalidOperationException("Finish or cancel existing backup transfers first.");
        storage.CreatePrivateDirectory(storage.WorkDirectory);
        var id = Guid.NewGuid(); var directory = Path.Combine(storage.WorkDirectory, "transfer-" + id.ToString("N")); storage.CreatePrivateDirectory(directory);
        var transfer = new Transfer(id, session, actor, directory, upload, length, sha, clock.GetTimestamp()); transfers.Add(id, transfer); return transfer;
    }
    private Transfer Find(Guid id, ApplicationSession session, Guid actor, bool upload)
    {
        if (!transfers.TryGetValue(id, out var transfer) || !ReferenceEquals(transfer.Session, session) || transfer.Actor != actor || transfer.Upload != upload)
            throw new UnauthorizedAccessException("The transfer is unavailable on this connection. Select the file again; no automatic retry was performed.");
        return transfer;
    }
    private void Expire()
    {
        foreach (var transfer in transfers.Values.Where(t => clock.GetElapsedTime(t.Started) >= TimeSpan.FromMinutes(15)).ToArray()) Remove(transfer);
        foreach (var actor in phraseFailures.Where(p => clock.GetElapsedTime(p.Value.Since) >= TimeSpan.FromMinutes(1)).Select(p => p.Key).ToArray()) phraseFailures.Remove(actor);
    }
    private void Remove(Transfer transfer)
    {
        transfer.Prepared?.Dispose(); transfer.Prepared = null; File.Delete(transfer.Path);
        // No recursive deletion: unexpected files remain private for maintenance review.
        if (!Directory.EnumerateFileSystemEntries(transfer.Directory).Any()) Directory.Delete(transfer.Directory);
        transfers.Remove(transfer.Id);
    }
    public void Dispose() { foreach (var transfer in transfers.Values.ToArray()) Remove(transfer); }
    private static string Version(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    private static string HashFile(string path) { using var input = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(input)); }
    private static void Audit(RetailFormaDbContext db, Guid actor, string action, string detail)
    { db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, UserId = actor, Action = action, Details = detail }); db.SaveChanges(); }
}
