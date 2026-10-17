using Microsoft.EntityFrameworkCore;
using RetailForma.Core;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;

namespace RetailForma.Desktop;

public sealed class BackupPreferencesService(RetailFormaDbContext db)
{
    private const string SecretKey = "BackupRecoverySecret";
    private static readonly string[] AllowedKeys = ["BackupCopyFolder", "Backup.GoogleDrive.Enabled", "Backup.Dropbox.Enabled", "Backup.GoogleDrive.Folder", "Backup.Dropbox.Folder"];
    public sealed record Result(bool Changed, bool PassphraseChanged);
    public sealed record PassphraseState(bool Configured, string Version);
    private static string Version(string secret) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret)));
    private RetailFormaDbContext Session() => new(new DbContextOptionsBuilder<RetailFormaDbContext>()
        .UseSqlite(db.Database.GetDbConnection(), contextOwnsConnection: false).Options);

    public PassphraseState ReadPassphraseState(Guid actorId)
    {
        using var session = Session();
        if (!UserAccess.IsAdministrator(session.Users.AsNoTracking().SingleOrDefault(u => u.Id == actorId)))
            throw new UnauthorizedAccessException("Only an active Owner or System Administrator can manage the recovery passphrase.");
        var secret = session.Settings.AsNoTracking().Where(s => s.Key == SecretKey).Select(s => s.Value).SingleOrDefault() ?? "";
        return new(secret.Length > 0, Version(secret));
    }

    public void VerifyCurrent(Guid actorId, string current, string expectedVersion)
    {
        using var session = Session(); using var transaction = session.Database.BeginTransaction();
        var actor = session.Users.AsNoTracking().SingleOrDefault(u => u.Id == actorId);
        if (!UserAccess.IsAdministrator(actor))
        {
            session.AuditLogs.Add(Audit(actor?.Id, "PermissionDenied", "Attempted recovery passphrase verification; values omitted."));
            session.SaveChanges(); transaction.Commit();
            throw new UnauthorizedAccessException("Only an active Owner or System Administrator can manage the recovery passphrase.");
        }
        var secret = session.Settings.AsNoTracking().Where(s => s.Key == SecretKey).Select(s => s.Value).SingleOrDefault() ?? "";
        try
        {
            if (Version(secret) != expectedVersion) throw new ArgumentException("The saved passphrase changed. Close and reopen this dialog.");
            BackupPassphrase.VerifyCurrent(secret, current);
        }
        catch (ArgumentException)
        {
            session.AuditLogs.Add(Audit(actorId, "BackupPassphraseChangeDenied", "Current recovery passphrase verification failed; values omitted."));
            session.SaveChanges(); transaction.Commit(); throw;
        }
        session.AuditLogs.Add(Audit(actorId, "BackupPassphraseVerified", "Current recovery passphrase verified; no replacement saved and values omitted."));
        session.SaveChanges(); transaction.Commit();
    }

    public Result Save(Guid actorId, IReadOnlyDictionary<string, string> preferences, string current, string next, string confirmation, string? expectedVersion = null)
    {
        if (db.Database.CurrentTransaction != null)
            throw new InvalidOperationException("Finish the current database operation before saving backup preferences.");
        if (db.ChangeTracker.Entries<Setting>().Any(e =>
            (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) &&
            (e.Entity.Key == SecretKey || AllowedKeys.Contains(e.Entity.Key, StringComparer.Ordinal))))
            throw new InvalidOperationException("Finish pending backup-setting changes before saving again.");
        // A fresh, short-lived context verifies persisted roles/secrets and cannot flush unrelated pending UI edits.
        using var session = new RetailFormaDbContext(new DbContextOptionsBuilder<RetailFormaDbContext>()
            .UseSqlite(db.Database.GetDbConnection(), contextOwnsConnection: false).Options);
        using var transaction = session.Database.BeginTransaction();
        var actor = session.Users.AsNoTracking().SingleOrDefault(u => u.Id == actorId);
        if (!UserAccess.IsAdministrator(actor))
        {
            session.AuditLogs.Add(Audit(actor?.Id, "PermissionDenied", "Attempted to change backup preferences; no values recorded."));
            session.SaveChanges(); transaction.Commit();
            throw new UnauthorizedAccessException("Only an active Owner or System Administrator can change backup preferences.");
        }
        if (preferences.Keys.Any(k => !AllowedKeys.Contains(k, StringComparer.Ordinal)))
            throw new ArgumentException("Unsupported backup preference.");
        foreach (var pair in preferences.Where(p => p.Key.EndsWith(".Enabled", StringComparison.Ordinal)))
            if (pair.Value is not ("true" or "false")) throw new ArgumentException("Invalid provider enable setting.");
        var rows = session.Settings.Where(s => AllowedKeys.Contains(s.Key) || s.Key == SecretKey).ToDictionary(s => s.Key);
        var savedSecret = rows.GetValueOrDefault(SecretKey)?.Value ?? "";
        string? replacement;
        try
        {
            if (expectedVersion != null && Version(savedSecret) != expectedVersion)
                throw new ArgumentException("The saved passphrase changed. Close and reopen this dialog before saving.");
            replacement = BackupPassphrase.PrepareChange(savedSecret, current, next, confirmation);
        }
        catch (ArgumentException)
        {
            session.AuditLogs.Add(Audit(actorId, "BackupPassphraseChangeDenied", "Recovery passphrase validation failed; no preferences changed and no values recorded."));
            session.SaveChanges(); transaction.Commit(); throw;
        }
        var changedKeys = new List<string>();
        foreach (var pair in preferences)
        {
            var row = rows.GetValueOrDefault(pair.Key);
            var previous = row?.Value ?? (pair.Key.EndsWith(".Enabled", StringComparison.Ordinal) ? "false" : "");
            if (previous == pair.Value) continue;
            if (row == null) session.Settings.Add(new Setting { Key = pair.Key, Value = pair.Value });
            else row.Value = pair.Value;
            changedKeys.Add(pair.Key);
        }
        if (replacement != null)
        {
            if (rows.GetValueOrDefault(SecretKey) is { } row) row.Value = replacement;
            else session.Settings.Add(new Setting { Key = SecretKey, Value = replacement });
            changedKeys.Add(SecretKey);
            session.AuditLogs.Add(Audit(actorId, savedSecret.Length == 0 ? "BackupPassphraseConfigured" : "BackupPassphraseChanged",
                "Recovery passphrase saved; current/new/protected values omitted. Existing archives retain their original passphrase."));
        }
        if (changedKeys.Count == 0) return new(false, false);
        session.AuditLogs.Add(Audit(actorId, "SettingChanged", string.Join("; ", changedKeys) + "; values omitted"));
        session.SaveChanges(); transaction.Commit();
        foreach (var entry in db.ChangeTracker.Entries<Setting>().Where(e => changedKeys.Contains(e.Entity.Key) && e.State == EntityState.Unchanged).ToArray())
        {
            try { entry.Reload(); }
            catch { entry.State = EntityState.Detached; } // Persist succeeded; discard a stale read cache instead of reporting a failed save.
        }
        return new(true, replacement != null);
    }

    private static AuditLog Audit(Guid? actorId, string action, string details) => new()
    { Date = BusinessClock.Now, UserId = actorId, Action = action, EntityName = "Setting", Details = details };
}
