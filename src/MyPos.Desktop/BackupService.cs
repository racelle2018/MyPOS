using System.IO;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace MyPos.Desktop;

public static class BackupService
{
    private static string AppFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyPos");

    public static void RunIfDue()
    {
        try
        {
            var folder = Path.Combine(AppFolder, "backups");
            Directory.CreateDirectory(folder);
            var latest = new DirectoryInfo(folder).GetFiles("mypos-*.db")
                .OrderByDescending(file => file.LastWriteTime)
                .FirstOrDefault();
            if (latest != null && DateTime.Now - latest.LastWriteTime < TimeSpan.FromHours(12)) return;
            BackupNow();
        }
        catch (Exception ex)
        {
            // Backups must never prevent the store from opening.
            Log.Warning(ex, "Backup schedule check failed");
        }
    }

    public static string? BackupNow()
    {
        try
        {
            var folder = Path.Combine(AppFolder, "backups");
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, $"mypos-{DateTime.Now:yyyyMMdd-HHmmss}.db");
            App.Db.Database.ExecuteSql($"VACUUM INTO {target}");
            Prune(folder, 14);
            Log.Information("Backup created: {BackupPath}", target);
            return target;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Backup failed");
            return null;
        }
    }

    private static void Prune(string folder, int keep)
    {
        foreach (var file in new DirectoryInfo(folder).GetFiles("mypos-*.db")
                     .OrderByDescending(file => file.LastWriteTime).Skip(keep))
        {
            try { file.Delete(); } catch { }
        }
    }
}
