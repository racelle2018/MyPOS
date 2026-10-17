using System.IO;
using Microsoft.Data.Sqlite;
using Serilog;

namespace RetailForma.Desktop;

public static class BackupService
{
    private static readonly SemaphoreSlim BackupGate = new(1, 1);
    private static string AppFolder => AppPaths.Folder;
    public static string DatabasePath => System.Windows.Application.Current is App { IsolatedTestHost: true }
        ? Path.Combine(AppFolder, "retailforma.db") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RetailForma", "Data", "retailforma.db");
    public static string BackupFolder => Path.Combine(AppFolder, "backups");
    private static string PendingRestorePath => Path.Combine(AppFolder, "restore-pending.db");

    public static DateTime? LastVerifiedBackupAt => LatestVerifiedBackup() is { } backup
        ? RetailForma.Core.BusinessClock.FromUtc(new DateTimeOffset(backup.LastWriteTimeUtc)) : null;
    private static string? legacyLastError;
    public static string? LastError { get => System.Windows.Application.Current is App { IsolatedTestHost: false } ? ServiceBackupFiles.LastError : legacyLastError; private set => legacyLastError = value; }
    private static IReadOnlyList<BackupCopies.Result> legacyLastCopies = [];
    public static IReadOnlyList<BackupCopies.Result> LastCopyResults { get => System.Windows.Application.Current is App { IsolatedTestHost: false } ? ServiceBackupFiles.LastCopies : legacyLastCopies; private set => legacyLastCopies = value; }

    public static void RunIfDue()
    {
        if (System.Windows.Application.Current is App { IsolatedTestHost: false }) { ServiceBackupFiles.RunIfDue(); return; }
        try
        {
            RetryPendingFolderCopies();
            var latest = LatestVerifiedBackup();
            if (latest != null && DateTime.UtcNow - latest.LastWriteTimeUtc < TimeSpan.FromHours(12)) return;
            CreateBackup(automatic: true);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Warning(ex, "Backup schedule check failed");
        }
    }

    public static string? BackupNow() => CreateBackup(automatic: false);

    private static string? CreateBackup(bool automatic) => System.Windows.Application.Current is App { IsolatedTestHost: false }
        ? ServiceBackupFiles.Create(automatic) : CreateBackupForInstallation(AppFolder, automatic);

    // Explicit installation paths let qualification tests avoid the live application data directory.
    public static string? CreateBackupForInstallation(string appFolder, bool automatic = false)
    {
        BackupGate.Wait();
        try
        {
            LastCopyResults = [];
            if (!Path.IsPathFullyQualified(appFolder)) throw new ArgumentException("Use a full installation data path.");
            var databasePath = Path.Combine(appFolder, "retailforma.db");
            var backupFolder = Path.Combine(appFolder, "backups");
            Directory.CreateDirectory(backupFolder);
            var password = ReadRecoveryPassword(databasePath);
            var kind = automatic ? "auto" : "manual";
            var target = Path.Combine(backupFolder, $"retailforma-{kind}-{RetailForma.Core.BusinessClock.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.zip");
            var destinations = ReadDestinations(databasePath);
            var pendingMarker = target + ".cloud-pending";
            if (destinations.Count > 0) File.WriteAllText(pendingMarker, "Encrypted folder copies pending; cloud completion is checked externally.");
            try { EncryptedBackup.Create(databasePath, target, password, automatic ? "Automatic" : "Manual"); }
            catch { File.Delete(pendingMarker); throw; }

            try
            {
                LastCopyResults = BackupCopies.Copy(target, password, destinations);
                var failures = LastCopyResults.Where(result => !result.Succeeded).ToArray();
                if (failures.Length > 0)
                {
                    LastError = "Local backup succeeded; folder-copy failures: " +
                        string.Join("; ", failures.Select(result => $"{result.Name}: {result.Message}"));
                    Log.Warning("{BackupIssue}", LastError);
                    return target;
                }
                if (destinations.Count > 0) File.Delete(pendingMarker);
            }
            catch (Exception ex)
            {
                LastError = $"Local backup succeeded; copy settings/verification failed: {ex.Message}";
                Log.Warning(ex, "Backup folder copies failed");
                return target;
            }

            LastError = null;
            if (automatic)
            {
                try
                {
                    var removed = BackupRetention.Prune(backupFolder, target, password);
                    Log.Information("Backup retention removed {Count} verified eligible automatic snapshots; manual/safety/legacy/pending copies preserved", removed);
                }
                catch (Exception ex)
                {
                    LastError = $"Backup succeeded; automatic retention could not finish: {ex.Message}";
                    Log.Warning(ex, "Backup retention failed");
                }
            }
            Log.Information("Verified backup created: {BackupPath}", target);
            return target;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Error(ex, "Backup failed");
            return null;
        }
        finally
        {
            BackupGate.Release();
        }
    }

    public static void CreateVerifiedBackup(string databasePath, string targetPath)
    {
        if (File.Exists(targetPath))
            throw new IOException($"Backup target already exists: {targetPath}");

        var source = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };
        using (var connection = new SqliteConnection(source.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "VACUUM INTO $target";
            command.Parameters.AddWithValue("$target", targetPath);
            command.ExecuteNonQuery();
        }

        try { Verify(targetPath); }
        catch
        {
            File.Delete(targetPath);
            throw;
        }
    }

    public static void Verify(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw new InvalidDataException("Backup file is missing or empty.");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA integrity_check";
        if (!string.Equals(check.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("SQLite integrity check failed.");

        using var schema = connection.CreateCommand();
        schema.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Sales', 'Products', 'Users', 'Settings')";
        if (Convert.ToInt32(schema.ExecuteScalar()) != 4)
            throw new InvalidDataException("The file is not a complete RetailForma database.");
        foreach (var (table, required) in new[]
        {
            ("Sales", new[] { "Id", "SaleDate", "TotalAmount" }),
            ("Products", new[] { "Id", "Name", "StockQty" }),
            ("Users", new[] { "Id", "Username", "PasswordHash", "Role" }),
            ("Settings", new[] { "Key", "Value" })
        })
        {
            using var columns = connection.CreateCommand();
            columns.CommandText = $"PRAGMA table_info(\"{table}\")"; // table names are fixed above.
            using var reader = columns.ExecuteReader();
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read()) found.Add(reader.GetString(1));
            if (required.Any(name => !found.Contains(name)))
                throw new InvalidDataException($"Backup has an incompatible {table} schema.");
        }
    }

    public static void StageRestore(string backupPath)
    {
        Verify(backupPath);
        if (File.Exists(PendingRestorePath))
            throw new InvalidOperationException("A restore is already pending. Restart RetailForma first.");
        File.Copy(backupPath, PendingRestorePath);
        try { Verify(PendingRestorePath); }
        catch
        {
            File.Delete(PendingRestorePath);
            throw;
        }
    }

    public static void StageEncryptedRestore(string backupPath, string password, string? appFolder = null)
    {
        BackupGate.Wait();
        try
        {
            var pending = Path.Combine(appFolder ?? AppFolder, "restore-pending.db");
            if (File.Exists(pending))
                throw new InvalidOperationException("A restore is already pending. Restart RetailForma first.");
            EncryptedBackup.Extract(backupPath, pending, password);
        }
        finally { BackupGate.Release(); }
    }

    // Called before EF opens the database. A safety copy is verified before replacement.
    public static void ApplyPendingRestore()
    {
        if (!File.Exists(PendingRestorePath)) return;
        ApplyPendingRestore(AppFolder, ReadRecoveryPassword());
    }

    public static void ApplyPendingRestore(string appFolder, string? safetyPassword = null)
    {
        var pendingPath = Path.Combine(appFolder, "restore-pending.db");
        var databasePath = Path.Combine(appFolder, "retailforma.db");
        var backupFolder = Path.Combine(appFolder, "backups");
        if (!File.Exists(pendingPath)) return;
        Verify(pendingPath);
        Directory.CreateDirectory(backupFolder);

        if (File.Exists(databasePath))
        {
            var extension = safetyPassword == null ? "db" : "zip";
            var safetyPath = Path.Combine(backupFolder, $"pre-restore-{RetailForma.Core.BusinessClock.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.{extension}");
            if (safetyPassword == null) CreateVerifiedBackup(databasePath, safetyPath);
            else EncryptedBackup.Create(databasePath, safetyPath, safetyPassword, "Safety");
            Log.Warning("Pre-restore safety backup created: {BackupPath}", safetyPath);
        }

        // After a clean shutdown, reopening and closing SQLite checkpoints WAL.
        // Remove stale sidecars only after the current database is safely copied.
        if (File.Exists(databasePath))
            File.Replace(pendingPath, databasePath, null);
        else
            File.Move(pendingPath, databasePath);
        foreach (var sidecar in new[] { databasePath + "-wal", databasePath + "-shm" })
            if (File.Exists(sidecar)) File.Delete(sidecar);
        Log.Warning("RetailForma database restored from a verified backup");
    }

    private static IReadOnlyList<BackupCopies.Destination> ReadDestinations(string? databasePath = null)
    {
        var destinations = new List<BackupCopies.Destination>();
        var external = ReadSetting("BackupCopyFolder", databasePath).Trim();
        if (external.Length > 0) destinations.Add(new("External/legacy copy", external));
        foreach (var provider in new[] { "GoogleDrive", "Dropbox" })
            if (ReadSetting($"Backup.{provider}.Enabled", databasePath) == "true")
                destinations.Add(new(provider == "GoogleDrive" ? "Google Drive" : "Dropbox", ReadSetting($"Backup.{provider}.Folder", databasePath).Trim()));
        return destinations;
    }

    public static void RetryPendingFolderCopies(string? appFolder = null)
    {
        BackupGate.Wait();
        try
        {
            var backupFolder = Path.Combine(appFolder ?? AppFolder, "backups");
            var databasePath = Path.Combine(appFolder ?? AppFolder, "retailforma.db");
            if (!Directory.Exists(backupFolder)) return;
            var markers = Directory.GetFiles(backupFolder, "*.zip.cloud-pending")
                .OrderBy(File.GetLastWriteTimeUtc).Take(8).ToArray();
            if (markers.Length == 0) return;
            var destinations = ReadDestinations(databasePath);
            if (destinations.Count == 0) return; // Disabling a destination does not discard outstanding recovery copies.
            var password = ReadRecoveryPassword(databasePath);
            foreach (var marker in markers)
            {
                var archive = marker[..^".cloud-pending".Length];
                try
                {
                    LastCopyResults = BackupCopies.Copy(archive, password, destinations);
                    if (LastCopyResults.All(result => result.Succeeded)) File.Delete(marker);
                    else Log.Warning("Folder-copy retry incomplete for {Archive}", archive);
                }
                catch (Exception ex) { Log.Warning(ex, "Folder-copy retry failed; recovery copy preserved: {Archive}", archive); }
            }
            if (Directory.GetFiles(backupFolder, "*.zip.cloud-pending").Length == 0 &&
                LastError is { } issue && (issue.StartsWith("Local backup succeeded; folder-copy failures:", StringComparison.Ordinal) ||
                                          issue.StartsWith("Local backup succeeded; copy settings/verification failed:", StringComparison.Ordinal)))
                LastError = null;
        }
        finally { BackupGate.Release(); }
    }

    private static string ReadRecoveryPassword(string? databasePath = null)
        => EncryptedBackup.UnprotectPassword(ReadSetting("BackupRecoverySecret", databasePath));

    private static string ReadSetting(string key, string? databasePath = null)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath ?? DatabasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key LIMIT 1";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar()?.ToString() ?? "";
    }

    private static FileInfo? LatestVerifiedBackup()
    {
        if (System.Windows.Application.Current is App { IsolatedTestHost: true }) return null; // UI qualification must not probe a real profile's backups or recovery secret.
        if (System.Windows.Application.Current is App) return ServiceBackupFiles.Latest();
        if (!Directory.Exists(BackupFolder)) return null; // Status reads must not create a data destination before migration.
        foreach (var file in new DirectoryInfo(BackupFolder).GetFiles()
                     .Where(file => file.Extension is ".db" or ".zip")
                     .OrderByDescending(file => file.LastWriteTime))
        {
            try
            {
                if (file.Extension == ".zip") EncryptedBackup.Verify(file.FullName, ReadRecoveryPassword());
                else Verify(file.FullName);
                return file;
            }
            catch (Exception ex) { Log.Warning(ex, "Backup verification failed: {BackupPath}", file.FullName); }
        }
        return null;
    }
}
