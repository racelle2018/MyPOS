using System.IO;
using RetailForma.Core;

namespace RetailForma.Desktop;

public static class BackupRetention
{
    public sealed record Snapshot(string Path, DateTimeOffset CreatedUtc, bool PendingCloud);

    /// <summary>Retain newest per 30 distinct Philippine days and 8 distinct Monday-start weeks.</summary>
    public static IReadOnlyList<Snapshot> Eligible(IReadOnlyList<Snapshot> verifiedAutomatic)
    {
        var ordered = verifiedAutomatic.OrderByDescending(s => s.CreatedUtc).ToArray();
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in ordered.GroupBy(s => BusinessClock.FromUtc(s.CreatedUtc).Date).Take(30).Select(g => g.First()))
            keep.Add(snapshot.Path);
        foreach (var snapshot in ordered.GroupBy(s => WeekStart(BusinessClock.FromUtc(s.CreatedUtc).Date)).Take(8).Select(g => g.First()))
            keep.Add(snapshot.Path);
        return ordered.Where(s => !s.PendingCloud && !keep.Contains(s.Path)).ToArray();
    }

    public static int Prune(string backupFolder, string newlyVerifiedPath, string password)
    {
        var folder = Path.GetFullPath(backupFolder);
        var latest = Path.GetFullPath(newlyVerifiedPath);
        if (!SameFolder(latest, folder)) throw new InvalidOperationException("New backup is outside the managed backup folder.");
        if (EncryptedBackup.Verify(latest, password).Purpose != "Automatic") return 0;
        var snapshots = new List<Snapshot>();
        foreach (var candidate in Directory.GetFiles(folder, "retailforma-auto-*.zip"))
        {
            try
            {
                var metadata = EncryptedBackup.Verify(candidate, password);
                if (metadata.Purpose == "Automatic")
                    snapshots.Add(new Snapshot(Path.GetFullPath(candidate), metadata.CreatedUtc, File.Exists(candidate + ".cloud-pending")));
            }
            catch { /* Unverifiable/older-password archives are preserved, never deemed disposable. */ }
        }
        var removed = 0;
        foreach (var candidate in Eligible(snapshots))
        {
            if (string.Equals(candidate.Path, latest, StringComparison.OrdinalIgnoreCase) ||
                !SameFolder(candidate.Path, folder) || File.Exists(candidate.Path + ".cloud-pending")) continue;
            // Recheck newest recovery copy immediately before deleting an eligible automatic snapshot.
            EncryptedBackup.Verify(latest, password);
            File.Delete(candidate.Path);
            removed++;
        }
        return removed;
    }

    private static DateTime WeekStart(DateTime day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    private static bool SameFolder(string file, string folder) => string.Equals(
        Path.GetDirectoryName(Path.GetFullPath(file))?.TrimEnd(Path.DirectorySeparatorChar),
        folder.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
}
