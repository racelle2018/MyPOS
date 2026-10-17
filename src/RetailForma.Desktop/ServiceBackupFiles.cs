using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using RetailForma.ServiceApplication;

namespace RetailForma.Desktop;

/// <summary>Unprivileged AppData/cloud-copy adapter. It sees only encrypted archives and service-verified hashes.</summary>
internal static class ServiceBackupFiles
{
    internal sealed record Receipt(int Format, long Length, string Sha256, DateTimeOffset CreatedUtc, bool Automatic);
    private static readonly SemaphoreSlim Gate = new(1);
    public static IReadOnlyList<BackupCopies.Result> LastCopies { get; private set; } = [];
    public static string? LastError { get; private set; }
    public static string? Create(bool automatic)
    {
        Gate.Wait();
        try
        {
            LastCopies = []; LastError = null;
            Directory.CreateDirectory(BackupService.BackupFolder);
            var package = App.Service.Call<BackupDownload>(Operation.CreateBackup, new CreateBackupInput(automatic));
            var target = Path.Combine(BackupService.BackupFolder, $"retailforma-{(automatic ? "auto" : "manual")}-{RetailForma.Core.BusinessClock.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.zip");
            var temporary = target + ".partial";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    for (long offset = 0; offset < package.Length;)
                    {
                        var chunk = App.Service.Call<BackupChunk>(Operation.ReadBackupChunk, new BackupChunkInput(package.TransferId, offset));
                        if (chunk.Offset != offset || chunk.Bytes.Length == 0 || chunk.Bytes.Length > package.Length - offset)
                            throw new InvalidDataException("The service returned an invalid backup chunk.");
                        output.Write(chunk.Bytes); offset += chunk.Bytes.Length;
                    }
                    output.Flush(true);
                }
                if (new FileInfo(temporary).Length != package.Length || Hash(temporary) != package.Sha256)
                    throw new InvalidDataException("The downloaded backup does not match the service-verified package.");
                File.Move(temporary, target);
                var receipt = new Receipt(2, package.Length, package.Sha256, package.CreatedUtc, automatic);
                using (var file = new FileStream(target + ".verified.json", FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { JsonSerializer.Serialize(file, receipt); file.Flush(true); }
                var destinations = Destinations();
                if (destinations.Count > 0)
                {
                    File.WriteAllText(target + ".cloud-pending", "Encrypted destination copies pending.");
                    LastCopies = CopyVerified(target, receipt, destinations);
                    if (LastCopies.All(c => c.Succeeded)) File.Delete(target + ".cloud-pending");
                    else LastError = "Local backup verified; some destination copies failed. Check each provider status.";
                }
                if (automatic) Prune(target);
                return target;
            }
            finally
            {
                File.Delete(temporary);
                try { App.Service.Call(Operation.FinishBackupDownload, new IdInput(package.TransferId)); }
                catch { /* Service expires private transfer files; a verified downloaded archive is not discarded. */ }
            }
        }
        catch (Exception ex) { LastError = ex.Message; return null; }
        finally { Gate.Release(); }
    }
    public static FileInfo? Latest()
    {
        if (!Directory.Exists(BackupService.BackupFolder)) return null;
        return Directory.GetFiles(BackupService.BackupFolder, "retailforma-*.zip").Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault(f => ReadReceipt(f.FullName) != null);
    }
    public static void RunIfDue()
    {
        // POS authentication remains mandatory; never silently sign in or retain a second credential.
        if (App.CurrentUser == null) return;
        RetryCopies();
        if (Latest() is { } latest && DateTime.UtcNow - latest.LastWriteTimeUtc < TimeSpan.FromHours(12)) return;
        Create(true);
    }
    public static Guid Upload(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".db", StringComparison.OrdinalIgnoreCase))
            foreach (var suffix in new[] { "-wal", "-journal" })
                if (File.Exists(path + suffix) && new FileInfo(path + suffix).Length > 0)
                    throw new InvalidOperationException("The legacy database has pending WAL/journal data. Close its POS normally and create a consistent backup; do not restore only the main file.");
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var length = input.Length; var sha = Convert.ToHexString(SHA256.HashData(input)); input.Position = 0;
        var id = App.Service.Call<Guid>(Operation.BeginRestoreUpload, new RestoreUploadInput(length, sha));
        try
        {
            var buffer = new byte[192 * 1024]; long offset = 0; int count;
            while ((count = input.Read(buffer)) != 0)
            {
                var written = App.Service.Call<long>(Operation.WriteRestoreChunk, new RestoreChunkInput(id, offset, buffer.AsSpan(0, count).ToArray()));
                if (written != offset + count) throw new InvalidDataException("The service did not acknowledge the restore chunk."); offset = written;
            }
            return id;
        }
        catch { Cancel(id); throw; }
    }
    public static void Cancel(Guid id)
    { try { App.Service.Call(Operation.CancelRestore, new IdInput(id)); } catch { /* Bounded service expiry handles a disconnected client. */ } }
    private static Receipt? ReadReceipt(string path)
    {
        try
        {
            var metadata = new FileInfo(path + ".verified.json");
            if (!metadata.Exists || metadata.Length > 4096 || (metadata.Attributes & FileAttributes.ReparsePoint) != 0) return null;
            var receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllBytes(metadata.FullName));
            return receipt is { Format: 2 } && new FileInfo(path).Length == receipt.Length && Hash(path) == receipt.Sha256 ? receipt : null;
        }
        catch { return null; }
    }
    private static List<BackupCopies.Destination> Destinations()
    {
        var values = App.Service.Settings(); var result = new List<BackupCopies.Destination>();
        if (values.GetValueOrDefault("BackupCopyFolder", "").Trim() is { Length: > 0 } folder) result.Add(new("Additional copy", folder));
        foreach (var provider in new[] { "GoogleDrive", "Dropbox" })
            if (values.GetValueOrDefault($"Backup.{provider}.Enabled") == "true") result.Add(new(provider == "GoogleDrive" ? "Google Drive" : "Dropbox", values.GetValueOrDefault($"Backup.{provider}.Folder", "")));
        BackupCopies.ValidateFolders(result, BackupService.BackupFolder); return result;
    }
    private static IReadOnlyList<BackupCopies.Result> CopyVerified(string source, Receipt receipt, List<BackupCopies.Destination> destinations)
    {
        if (Hash(source) != receipt.Sha256) throw new InvalidDataException("The local encrypted backup changed after service verification.");
        var results = new List<BackupCopies.Result>();
        foreach (var destination in destinations)
        {
            string? temporary = null;
            try
            {
                Directory.CreateDirectory(destination.Folder); var target = Path.Combine(destination.Folder, Path.GetFileName(source));
                if (File.Exists(target))
                { if (Hash(target) != receipt.Sha256) throw new IOException("A different destination file already exists; it was not overwritten."); }
                else
                {
                    temporary = Path.Combine(destination.Folder, ".retailforma-" + Guid.NewGuid().ToString("N") + ".partial");
                    File.Copy(source, temporary); if (Hash(temporary) != receipt.Sha256) throw new InvalidDataException("Destination copy verification failed.");
                    File.Move(temporary, target);
                }
                results.Add(new(destination.Name, destination.Folder, true, "Encrypted folder copy verified; provider cloud completion is not verified."));
            }
            catch (Exception ex) { results.Add(new(destination.Name, destination.Folder, false, ex.Message)); }
            finally { if (temporary != null) { try { File.Delete(temporary); } catch { } } }
        }
        return results;
    }
    private static void RetryCopies()
    {
        if (!Directory.Exists(BackupService.BackupFolder)) return;
        var destinations = Destinations(); if (destinations.Count == 0) return;
        foreach (var marker in Directory.GetFiles(BackupService.BackupFolder, "*.zip.cloud-pending").Take(8))
        {
            var archive = marker[..^".cloud-pending".Length]; var receipt = ReadReceipt(archive); if (receipt == null) continue;
            LastCopies = CopyVerified(archive, receipt, destinations); if (LastCopies.All(c => c.Succeeded)) File.Delete(marker);
        }
    }
    private static void Prune(string newest)
    {
        var snapshots = Directory.GetFiles(BackupService.BackupFolder, "retailforma-auto-*.zip")
            .Select(p => (Path: p, Receipt: ReadReceipt(p))).Where(p => p.Receipt is { Automatic: true })
            .Select(p => new BackupRetention.Snapshot(p.Path, p.Receipt!.CreatedUtc, File.Exists(p.Path + ".cloud-pending"))).ToArray();
        foreach (var candidate in BackupRetention.Eligible(snapshots))
        {
            if (candidate.Path == newest || ReadReceipt(newest) == null || File.Exists(candidate.Path + ".cloud-pending")) continue;
            File.Delete(candidate.Path); File.Delete(candidate.Path + ".verified.json");
        }
    }
    private static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
}
