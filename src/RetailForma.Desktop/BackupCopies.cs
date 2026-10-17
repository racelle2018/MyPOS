using System.IO;
using System.Security.Cryptography;

namespace RetailForma.Desktop;

public static class BackupCopies
{
    public sealed record Destination(string Name, string Folder);
    public sealed record Result(string Name, string Folder, bool Succeeded, string Message);

    public static void ValidateFolders(IEnumerable<Destination> destinations, string localBackupFolder)
    {
        var paths = new List<string>();
        var local = Normalize(localBackupFolder);
        foreach (var destination in destinations)
        {
            if (string.IsNullOrWhiteSpace(destination.Folder) || !Path.IsPathFullyQualified(destination.Folder))
                throw new ArgumentException($"{destination.Name}: select a full dedicated backup-folder path.");
            var path = Normalize(destination.Folder);
            if (string.Equals(path, Normalize(Path.GetPathRoot(path)!), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"{destination.Name}: choose a dedicated subfolder, not a drive/share root.");
            if (Overlaps(path, local))
                throw new ArgumentException($"{destination.Name}: use a folder separate from local RetailForma backups, not their parent or child.");
            if (paths.Any(other => Overlaps(other, path)))
                throw new ArgumentException("Use separate, non-nested folders for each backup destination.");
            paths.Add(path);
        }
    }

    public static IReadOnlyList<Result> Copy(string archivePath, string password, IReadOnlyList<Destination> destinations)
    {
        EncryptedBackup.Verify(archivePath, password);
        ValidateFolders(destinations, Path.GetDirectoryName(Path.GetFullPath(archivePath))!);
        var results = new List<Result>();
        foreach (var destination in destinations)
        {
            string? temporary = null;
            try
            {
                Directory.CreateDirectory(destination.Folder);
                var target = Path.Combine(destination.Folder, Path.GetFileName(archivePath));
                if (File.Exists(target))
                {
                    if (!EqualContent(archivePath, target))
                        throw new IOException("A different file already occupies the backup destination; it was not overwritten.");
                    EncryptedBackup.Verify(target, password);
                }
                else
                {
                    temporary = Path.Combine(destination.Folder, $".retailforma-{Guid.NewGuid():N}.partial");
                    File.Copy(archivePath, temporary);
                    if (!EqualContent(archivePath, temporary)) throw new IOException("Copied archive does not match the local backup.");
                    EncryptedBackup.Verify(temporary, password);
                    File.Move(temporary, target);
                }
                results.Add(new Result(destination.Name, destination.Folder, true,
                    "Encrypted folder copy verified. Cloud synchronization is not verified by RetailForma."));
            }
            catch (Exception ex)
            {
                results.Add(new Result(destination.Name, destination.Folder, false, ex.Message));
            }
            finally
            {
                if (temporary != null)
                {
                    try { File.Delete(temporary); }
                    catch { /* Keep the encrypted partial file if cleanup is denied; never touch a published backup. */ }
                }
            }
        }
        return results;
    }

    private static bool EqualContent(string left, string right)
    {
        using var a = File.OpenRead(left);
        using var b = File.OpenRead(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(SHA256.HashData(a), SHA256.HashData(b));
    }
    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static bool Overlaps(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ||
        a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
