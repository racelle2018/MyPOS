using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RetailForma.ServiceApplication;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;
using RetailForma.EncryptedStorage;
using RetailForma.LocalTransport;
using RetailForma.Security;

namespace RetailForma.DataService;

public sealed class ServiceDatabase : IDisposable
{
    private sealed record State(int Format, Guid InstallationId, Guid ResourceId, string KeyId, string ProtectedKey, string RecoveryEnvelope);
    private readonly string root;
    private readonly RSA recovery;
    private readonly Action demandIdentity;
    private readonly Func<DirectorySecurity> directorySecurity;
    private byte[]? key;
    private State? state;
    private DeveloperRecoveryAuthorization? authorization;
    public string DataDirectory => Path.Combine(root, "Data");
    public string DatabasePath => Path.Combine(DataDirectory, "retailforma.db");
    private string StatePath => Path.Combine(DataDirectory, "key-state.json");
    public bool Configured => state != null;
    internal string WorkDirectory => Path.Combine(root, "Work");
    internal Guid InstallationId => state?.InstallationId ?? throw new InvalidOperationException("Company setup is required.");
    internal DirectoryInfo CreatePrivateDirectory(string path)
    {
        demandIdentity();
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Work must remain inside service storage.");
        for (var parent = new DirectoryInfo(full); parent != null; parent = parent.Parent)
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Service work directories cannot be links.");
        var directory = new DirectoryInfo(full); directory.Create(directorySecurity()); return directory;
    }

    public ServiceDatabase(string recoveryPublicPem) : this(Path.GetDirectoryName(ServiceStoragePolicy.DataDirectory)!, recoveryPublicPem,
        () =>
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (!Equals(identity.User, LocalPipeSecurity.ServiceSid)) throw new UnauthorizedAccessException("Run database access under the dedicated RetailForma data service identity.");
            VerifyRoot(Path.GetDirectoryName(ServiceStoragePolicy.DataDirectory)!, LocalPipeSecurity.ServiceSid);
            if (Directory.Exists(ServiceStoragePolicy.DataDirectory)) VerifyRoot(ServiceStoragePolicy.DataDirectory, LocalPipeSecurity.ServiceSid);
        }, () => ServiceStoragePolicy.DirectoryDescriptor(LocalPipeSecurity.ServiceSid, ownerIsService: true)) { }

    internal ServiceDatabase(string isolatedRoot, string publicPem, Action demandIdentity, Func<DirectorySecurity> directorySecurity)
    {
        root = Path.GetFullPath(isolatedRoot); this.demandIdentity = demandIdentity; this.directorySecurity = directorySecurity;
        recovery = RSA.Create();
        try
        {
            if (publicPem.Contains("PRIVATE KEY", StringComparison.Ordinal)) throw new ArgumentException("Only the recovery public key belongs in the service.");
            recovery.ImportFromPem(publicPem); _ = DeveloperRecoveryEnvelope.PublicKeyId(recovery); demandIdentity();
            Load();
        }
        catch { recovery.Dispose(); throw; }
    }

    private void Load()
    {
        if (!Directory.Exists(DataDirectory)) return;
        if ((File.GetAttributes(DataDirectory) & FileAttributes.ReparsePoint) != 0) throw new IOException("The data directory must not be a link.");
        foreach (var path in new[] { StatePath, DatabasePath })
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Protected database and key files cannot be links.");
        if (!File.Exists(StatePath) || !File.Exists(DatabasePath))
        {
            if (Directory.EnumerateFileSystemEntries(DataDirectory).Any()) throw new InvalidDataException("Incomplete protected database/key state. Recover it; no blank company will be created.");
            return;
        }
        if (new FileInfo(StatePath).Length > 16384) throw new InvalidDataException("Invalid protected key state.");
        var loaded = JsonSerializer.Deserialize<State>(File.ReadAllBytes(StatePath), WireJson.Options) ?? throw new InvalidDataException("Missing key state.");
        if (loaded.Format != 1 || loaded.InstallationId == Guid.Empty || loaded.ResourceId == Guid.Empty || loaded.KeyId != DeveloperRecoveryEnvelope.PublicKeyId(recovery))
            throw new InvalidDataException("The installation or recovery trust does not match its protected key state.");
        var plain = ProtectedData.Unprotect(Convert.FromBase64String(loaded.ProtectedKey), Entropy(loaded), DataProtectionScope.CurrentUser);
        try
        {
            if (plain.Length != 32) throw new InvalidDataException("Invalid database key.");
            state = loaded; key = plain; authorization = new(loaded.InstallationId, recovery.ExportSubjectPublicKeyInfo());
            using var db = EncryptedDbContexts.OpenExisting(DatabasePath, plain);
            CompanySchema.BeforeMigration(db);
            if (db.Database.GetPendingMigrations().Any())
            {
                CreateSafetySnapshot("Before protected schema upgrade");
                DbInitializer.Initialize(db);
                SqlCipherConnections.VerifyIntegrity((Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection());
            }
            CompanySchema.AfterMigration(db);
            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        }
        catch { CryptographicOperations.ZeroMemory(plain); throw; }
    }

    public RetailFormaDbContext Open()
    {
        demandIdentity();
        if (key == null || state == null) throw new InvalidOperationException("Complete technician setup before opening the company database.");
        return EncryptedDbContexts.OpenExisting(DatabasePath, key);
    }
    public ServiceStatusInfo Status()
    {
        demandIdentity();
        if (!Configured) return new(false, DatabasePath, "", false, Guid.Empty, DeveloperRecoveryEnvelope.PublicKeyId(recovery));
        using var db = Open();
        return new(true, DatabasePath, db.Settings.Find("CompanyName")?.Value ?? "", db.Users.Any(u => u.Role == UserRole.SystemAdministrator),
            state!.InstallationId, state.KeyId);
    }

    public void ProvisionNew(TechnicalProvisionInput input, string windowsOperator)
    {
        demandIdentity();
        if (Configured || Directory.Exists(DataDirectory)) throw new InvalidOperationException("Protected company storage already exists. Use the explicit migration/recovery workflow; existing files are never replaced by setup.");
        if (string.IsNullOrWhiteSpace(input.Company) || input.Company.Length > 200 || input.Password != input.Confirmation)
            throw new ArgumentException("Enter a company name and matching individual credentials.");
        var installation = Guid.NewGuid(); var resource = Guid.NewGuid(); var freshKey = RandomNumberGenerator.GetBytes(32);
        var staging = Path.Combine(root, ".provision-" + Guid.NewGuid().ToString("N"));
        new DirectoryInfo(staging).Create(directorySecurity());
        try
        {
            var context = new RecoveryContext(installation, resource, RecoveryPurpose.DatabaseKey);
            var envelope = DeveloperRecoveryEnvelope.Seal(freshKey, context, recovery);
            var next = new State(1, installation, resource, DeveloperRecoveryEnvelope.PublicKeyId(recovery), "", Convert.ToBase64String(envelope));
            next = next with { ProtectedKey = Convert.ToBase64String(ProtectedData.Protect(freshKey, Entropy(next), DataProtectionScope.CurrentUser)) };
            var path = Path.Combine(staging, "retailforma.db");
            using (SqlCipherConnections.CreateNew(path, freshKey)) { }
            using (var db = EncryptedDbContexts.OpenExisting(path, freshKey))
            {
                DbInitializer.Initialize(db);
                // Only the individual technician may claim a newly provisioned company.
                // This is a fresh unpublished database, never an imported company.
                db.Users.RemoveRange(db.Users.ToList());
                var company = db.Settings.Find("CompanyName");
                if (company == null) db.Settings.Add(new Setting { Key = "CompanyName", Value = input.Company.Trim() }); else company.Value = input.Company.Trim();
                db.SaveChanges();
                SystemAdministratorSetup.CreateFirst(db, input.Username, input.FullName, input.Password, windowsOperator);
                SqlCipherConnections.VerifyIntegrity((Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection());
            }
            using (var file = new FileStream(Path.Combine(staging, "key-state.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, next, WireJson.Options); file.Flush(true); }
            Directory.Move(staging, DataDirectory); // Publish database and key state together, never overwrite a destination.
            Load();
        }
        finally { CryptographicOperations.ZeroMemory(freshKey); }
        // On failure preserve the service-owned staging directory for recovery review, not destructive cleanup.
    }

    public void ProvisionTechnical(TechnicalProvisionInput input, string windowsOperator)
    {
        demandIdentity();
        if (input.Password != input.Confirmation) throw new ArgumentException("Passwords do not match.");
        using var db = Open(); SystemAdministratorSetup.CreateFirst(db, input.Username, input.FullName, input.Password, windowsOperator);
    }

    internal void ProvisionFromBackup(RecoveryBackupPackage.RecoveredBackup recovered, string ownerPhrase, string windowsOperator)
    {
        demandIdentity();
        if (Configured || Directory.Exists(DataDirectory)) throw new InvalidOperationException("A protected company already exists. Initial import never overwrites it.");
        RetailForma.Security.OwnerRecoveryEnvelope.ValidatePassphrase(ownerPhrase);
        var installation = Guid.NewGuid(); var resource = Guid.NewGuid(); var freshKey = RandomNumberGenerator.GetBytes(32);
        var staging = Path.Combine(root, ".import-" + Guid.NewGuid().ToString("N")); CreatePrivateDirectory(staging);
        try
        {
            var context = new RecoveryContext(installation, resource, RecoveryPurpose.DatabaseKey);
            var envelope = DeveloperRecoveryEnvelope.Seal(freshKey, context, recovery);
            var next = new State(1, installation, resource, DeveloperRecoveryEnvelope.PublicKeyId(recovery), "", Convert.ToBase64String(envelope));
            next = next with { ProtectedKey = Convert.ToBase64String(ProtectedData.Protect(freshKey, Entropy(next), DataProtectionScope.CurrentUser)) };
            var destination = Path.Combine(staging, "retailforma.db");
            using (var source = recovered.OpenDatabase()) SqlCipherConnections.ExportEncrypted(source, destination, freshKey);
            using (var db = EncryptedDbContexts.OpenExisting(destination, freshKey))
            {
                CompanySchema.BeforeMigration(db); DbInitializer.Initialize(db); CompanySchema.AfterMigration(db);
                foreach (var row in db.Settings.Where(s => s.Key == "BackupRecoverySecret" || s.Key.StartsWith("Security.Backup.")).ToList()) db.Settings.Remove(row);
                var phraseBytes = Encoding.UTF8.GetBytes(ownerPhrase);
                try
                {
                    var entropy = SHA256.HashData(Encoding.UTF8.GetBytes("RetailForma.Backup.Automation.v2|" + installation.ToString("N")));
                    db.Settings.Add(new Setting { Key = "Security.Backup.AutomationSecret", Value = Convert.ToBase64String(ProtectedData.Protect(phraseBytes, entropy, DataProtectionScope.CurrentUser)) });
                }
                finally { CryptographicOperations.ZeroMemory(phraseBytes); }
                db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, Action = "ProtectedCompanyImported",
                    Details = "Explicit encrypted-backup conversion by Windows operator " + windowsOperator + "; original source preserved; fresh installation key and independent developer recovery applied." });
                db.SaveChanges();
                var connection = (Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection();
                RecoveryBackupPackage.Create(connection, Path.Combine(staging, "import-safety.zip"), staging, ownerPhrase, recovery, installation, "Safety");
                SqlCipherConnections.VerifyIntegrity(connection); CheckpointAndCloseWal(connection);
            }
            WriteNew(Path.Combine(staging, "key-state.json"), next);
            Directory.Move(staging, DataDirectory); Load();
        }
        finally { CryptographicOperations.ZeroMemory(freshKey); }
        // Failed private staging is retained for review; the original input is never changed.
    }

    public AccountRecoveryRequest IssueReset(string username)
    {
        demandIdentity();
        if (username == null || username.Length > 128) throw new ArgumentException("Enter the account username.");
        using var db = Open();
        var user = db.Users.AsNoTracking().ToList().SingleOrDefault(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Account not found.");
        if (!user.IsActive) throw new InvalidOperationException("This account is disabled. Password recovery does not restore disabled access.");
        var challenge = authorization!.IssuePasswordReset(user.Id);
        return new(1, state!.InstallationId, user.Id, user.Username, db.Settings.Find("CompanyName")?.Value ?? "", state.KeyId, Convert.ToBase64String(challenge));
    }

    public void ResetAccount(AccountRecoveryInput input, string windowsOperator)
    {
        demandIdentity();
        if (input.Authorization.Format != 1 || input.TemporaryPassword != input.Confirmation ||
            string.IsNullOrWhiteSpace(input.TemporaryPassword) || input.TemporaryPassword.Length is < 16 or > 256)
            throw new ArgumentException("Enter a matching temporary passphrase of 16–256 characters.");
        if (input.Authorization.Challenge.Length > 1024 || input.Authorization.Signature.Length > 2048) throw new ArgumentException("Invalid recovery authorization.");
        var challenge = Convert.FromBase64String(input.Authorization.Challenge);
        var target = DeveloperRecoveryAuthorization.Describe(challenge).AccountId;
        authorization!.ConsumePasswordReset(challenge, Convert.FromBase64String(input.Authorization.Signature), target);
        var safetyId = CreateSafetySnapshot("Before developer-authorized account recovery");
        using var db = Open(); using var tx = db.Database.BeginTransaction();
        var user = db.Users.Find(target) ?? throw new ArgumentException("Account not found.");
        if (!user.IsActive) throw new UnauthorizedAccessException("The account is disabled; recovery does not enable it.");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(input.TemporaryPassword, 12);
        var flagKey = "Security.PasswordChangeRequired." + user.Id.ToString("N"); var flag = db.Settings.Find(flagKey);
        if (flag == null) db.Settings.Add(new Setting { Key = flagKey, Value = "true" }); else flag.Value = "true";
        db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, Action = "DeveloperAccountRecovery", UserId = target,
            EntityName = "User", EntityId = target, Details = "Authorized individual password reset by Windows operator " + windowsOperator + "; roles/access unchanged, all prior sessions revoked. Encrypted safety snapshot " + safetyId.ToString("N") + "." });
        db.SaveChanges(); tx.Commit();
    }

    /// <summary>Service-only safety copy, independently recoverable with the developer envelope in key-state.json.
    /// It is not a portable owner ZIP and never replaces or prunes the owner's AppData backups.</summary>
    internal Guid CreateSafetySnapshot(string purpose)
    {
        demandIdentity();
        if (!Configured) throw new InvalidOperationException("There is no configured company to preserve.");
        StorageSpace.Require(root, checked(new FileInfo(DatabasePath).Length * 2));
        var id = Guid.NewGuid();
        var folder = Path.Combine(root, "Safety", id.ToString("N"));
        CreatePrivateDirectory(Path.Combine(root, "Safety")); CreatePrivateDirectory(folder);
        using (var db = Open())
            SqlCipherConnections.ExportEncrypted((Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection(), Path.Combine(folder, "retailforma.db"), key!);
        var savedState = File.ReadAllBytes(StatePath);
        using (var file = new FileStream(Path.Combine(folder, "key-state.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { file.Write(savedState); file.Flush(true); }
        using (var check = SqlCipherConnections.OpenExisting(Path.Combine(folder, "retailforma.db"), key!)) SqlCipherConnections.VerifyIntegrity(check);
        WriteNew(Path.Combine(folder, "snapshot.json"), new { Format = 1, Id = id, state!.InstallationId, CreatedUtc = DateTimeOffset.UtcNow, Purpose = purpose,
            DatabaseSha256 = HashFile(Path.Combine(folder, "retailforma.db")), KeyStateSha256 = Convert.ToHexString(SHA256.HashData(savedState)) });
        return id;
    }

    internal void CreatePortableBackup(string output, string work, string ownerPhrase, bool automatic)
    {
        demandIdentity(); using var db = Open();
        RecoveryBackupPackage.Create((Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection(), output, work,
            ownerPhrase, recovery, InstallationId, automatic ? "Automatic" : "Manual");
    }

    internal void RestoreVerified(RecoveryBackupPackage.RecoveredBackup recovered, Guid actor, string windowsOperator)
    {
        demandIdentity(); CreatePrivateDirectory(WorkDirectory);
        var replacement = Path.Combine(WorkDirectory, "replacement-" + Guid.NewGuid().ToString("N") + ".db");
        // The destination keeps this installation's random key and pinned recovery trust. A backup's
        // historical DPAPI secret is not copied into this installation's backup automation settings.
        using (var source = recovered.OpenDatabase()) SqlCipherConnections.ExportEncrypted(source, replacement, key!);
        using (var db = EncryptedDbContexts.OpenExisting(replacement, key!))
        {
            CompanySchema.BeforeMigration(db); DbInitializer.Initialize(db); CompanySchema.AfterMigration(db);
            var secret = db.Settings.Find("Security.Backup.AutomationSecret"); if (secret != null) db.Settings.Remove(secret);
            var legacySecret = db.Settings.Find("BackupRecoverySecret"); if (legacySecret != null) db.Settings.Remove(legacySecret);
            db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, Action = "BackupRestored", UserId = db.Users.Any(u => u.Id == actor) ? actor : null,
                Details = "Verified encrypted backup restored by Windows operator " + windowsOperator + "; service sessions revoked. Backup passphrase must be set on this installation." });
            db.SaveChanges();
            SqlCipherConnections.VerifyIntegrity((Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection());
            CheckpointAndCloseWal((Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection());
        }
        var safety = CreateSafetySnapshot("Before encrypted backup restore");
        using (var current = Open()) CheckpointAndCloseWal((Microsoft.Data.Sqlite.SqliteConnection)current.Database.GetDbConnection());
        // Atomic same-volume file replacement; original remains in protected safety storage. No point
        // exists at which startup can mistake a missing live database for a new company.
        File.Replace(replacement, DatabasePath, Path.Combine(root, "Safety", safety.ToString("N"), "replaced.db"));
        using var verify = Open(); SqlCipherConnections.VerifyIntegrity((Microsoft.Data.Sqlite.SqliteConnection)verify.Database.GetDbConnection());
        verify.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }

    private static void CheckpointAndCloseWal(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        using var command = connection.CreateCommand(); command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        using (var rows = command.ExecuteReader())
            if (rows.Read() && rows.GetInt64(0) != 0) throw new InvalidOperationException("Another database connection is active. Close it before recovery.");
        command.CommandText = "PRAGMA journal_mode=DELETE;";
        if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "delete", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The live database could not be closed exclusively for recovery.");
    }
    private static void WriteNew<T>(string path, T value)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(file, value, WireJson.Options); file.Flush(true);
    }
    private static string HashFile(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }

    private static byte[] Entropy(State data) => SHA256.HashData(Encoding.UTF8.GetBytes("RetailForma.Service.Key.v1|" + data.InstallationId.ToString("N") + "|" + data.ResourceId.ToString("N") + "|" + data.KeyId + "|" + data.RecoveryEnvelope));

    private static void VerifyRoot(string path, SecurityIdentifier service)
    {
        var cursor = new DirectoryInfo(path);
        for (var parent = cursor; parent != null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Protected storage must not traverse directory links.");
        var security = cursor.GetAccessControl();
        var allowed = new[] { service.Value, "S-1-5-18", "S-1-5-32-544" };
        if (!security.AreAccessRulesProtected || !allowed.Contains(security.GetOwner(typeof(SecurityIdentifier))?.Value))
            throw new UnauthorizedAccessException("Repair the protected service directory through the installer.");
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && !allowed.Contains(rule.IdentityReference.Value))
                throw new UnauthorizedAccessException("The protected service directory grants access outside its security boundary.");
    }
    public void Dispose() { authorization?.Dispose(); if (key != null) CryptographicOperations.ZeroMemory(key); recovery.Dispose(); }
}
