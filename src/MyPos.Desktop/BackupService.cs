using System.IO;
using Microsoft.Data.Sqlite;
using Serilog;

namespace MyPos.Desktop;

public static class BackupService
{
    private static readonly SemaphoreSlim BackupGate = new(1, 1);
    private static string AppFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyPos");
    public static string DatabasePath => Path.Combine(AppFolder, "mypos.db");
    public static string BackupFolder => Path.Combine(AppFolder, "backups");
    private static string PendingRestorePath => Path.Combine(AppFolder, "restore-pending.db");

    public static DateTime? LastVerifiedBackupAt => LatestVerifiedBackup()?.LastWriteTime;
    public static string? LastError { get; private set; }

    public static void RunIfDue()
    {
        try
        {
            var latest = LatestVerifiedBackup();
            if (latest != null && DateTime.Now - latest.LastWriteTime < TimeSpan.FromHours(12)) return;
            BackupNow();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Warning(ex, "Backup schedule check failed");
        }
    }

    public static string? BackupNow()
    {
        BackupGate.Wait();
        try
        {
            Directory.CreateDirectory(BackupFolder);
            var target = Path.Combine(BackupFolder, $"mypos-{DateTime.Now:yyyyMMdd-HHmmss-fff}.db");
            CreateVerifiedBackup(DatabasePath, target);

            var offsite = ReadSecondCopyFolder();
            if (offsite.Length > 0)
            {
                try
                {
                    Directory.CreateDirectory(offsite);
                    var copy = Path.Combine(offsite, Path.GetFileName(target));
                    File.Copy(target, copy);
                    Verify(copy);
                }
                catch (Exception ex)
                {
                    LastError = $"Local backup succeeded; second copy failed: {ex.Message}";
                    Log.Warning(ex, "Backup second copy failed");
                    return target;
                }
            }

            LastError = null;
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
            throw new InvalidDataException("The file is not a complete MyPos database.");
    }

    public static void StageRestore(string backupPath)
    {
        Verify(backupPath);
        if (File.Exists(PendingRestorePath))
            throw new InvalidOperationException("A restore is already pending. Restart MyPos first.");
        File.Copy(backupPath, PendingRestorePath);
        try { Verify(PendingRestorePath); }
        catch
        {
            File.Delete(PendingRestorePath);
            throw;
        }
    }

    // Called before EF opens the database. A safety copy is verified before replacement.
    public static void ApplyPendingRestore() => ApplyPendingRestore(AppFolder);

    public static void ApplyPendingRestore(string appFolder)
    {
        var pendingPath = Path.Combine(appFolder, "restore-pending.db");
        var databasePath = Path.Combine(appFolder, "mypos.db");
        var backupFolder = Path.Combine(appFolder, "backups");
        if (!File.Exists(pendingPath)) return;
        Verify(pendingPath);
        Directory.CreateDirectory(backupFolder);

        if (File.Exists(databasePath))
        {
            var safetyPath = Path.Combine(backupFolder, $"pre-restore-{DateTime.Now:yyyyMMdd-HHmmss-fff}.db");
            CreateVerifiedBackup(databasePath, safetyPath);
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
        Log.Warning("MyPos database restored from a verified backup");
    }

    private static string ReadSecondCopyFolder()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadOnly
        };
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = 'BackupCopyFolder' LIMIT 1";
        return command.ExecuteScalar()?.ToString()?.Trim() ?? "";
    }

    private static FileInfo? LatestVerifiedBackup()
    {
        Directory.CreateDirectory(BackupFolder);
        foreach (var file in new DirectoryInfo(BackupFolder).GetFiles("mypos-*.db")
                     .OrderByDescending(file => file.LastWriteTime))
        {
            try { Verify(file.FullName); return file; }
            catch (Exception ex) { Log.Warning(ex, "Backup verification failed: {BackupPath}", file.FullName); }
        }
        return null;
    }
}
