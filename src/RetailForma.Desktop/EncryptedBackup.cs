using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ICSharpCode.SharpZipLib.Zip;

namespace RetailForma.Desktop;

public enum BackupFailure { PassphraseRejected, CannotAuthenticate, DamagedArchive, UnsupportedPackage, NewerVersion, DamagedContents, InsufficientSpace }
public sealed class BackupRestoreException(BackupFailure failure, string message) : IOException(message)
{
    public BackupFailure Failure { get; } = failure;
}

/// <summary>Portable password-encrypted backups. Never extracts archive-supplied paths.</summary>
public static class EncryptedBackup
{
    private const long MaximumDatabaseBytes = 16L * 1024 * 1024 * 1024;
    public sealed record Manifest(int Format, long DatabaseBytes, string Sha256, DateTimeOffset CreatedUtc,
        string Purpose = "Unknown", string ApplicationVersion = "");

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 16 || password.Length > 256)
            throw new ArgumentException("Use a unique recovery passphrase of 16–256 characters. Keep an independent safe copy.");
    }

    public static string ProtectPassword(string password)
    {
        ValidatePassword(password);
        var bytes = Encoding.UTF8.GetBytes(password);
        try { return Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static string UnprotectPassword(string protectedPassword)
    {
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedPassword), null, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(bytes); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            throw new InvalidOperationException("Save your recovery password in Backup & Restore on this Windows profile before creating backups.");
        }
    }

    public static void Create(string databasePath, string archivePath, string password, string purpose = "Manual")
    {
        ValidatePassword(password);
        if (purpose is not ("Manual" or "Automatic" or "Safety")) throw new ArgumentException("Invalid backup purpose.");
        if (File.Exists(archivePath)) throw new IOException("The backup target already exists.");
        var folder = Path.GetDirectoryName(Path.GetFullPath(archivePath))!;
        Directory.CreateDirectory(folder);
        EnsureSpace(folder, checked(new FileInfo(databasePath).Length * 3));
        var workFolder = Path.Combine(Path.GetTempPath(), $"retailforma-backup-work-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workFolder);
        var snapshot = Path.Combine(workFolder, "snapshot.db");
        var temporaryArchive = Path.Combine(folder, $".archive-{Guid.NewGuid():N}.tmp");
        try
        {
            EnsureSpace(workFolder, checked(new FileInfo(databasePath).Length * 2));
            BackupService.CreateVerifiedBackup(databasePath, snapshot);
            var length = new FileInfo(snapshot).Length;
            if (length > MaximumDatabaseBytes) throw new InvalidDataException("Database exceeds the supported backup size.");
            using (var output = new ZipOutputStream(File.Create(temporaryArchive)))
            {
                output.Password = password;
                output.SetLevel(6);
                WriteEntry(output, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(
                    new Manifest(1, length, Hash(snapshot), DateTimeOffset.UtcNow, purpose, AppVersion.Current)));
                output.PutNextEntry(new ZipEntry("retailforma.db") { AESKeySize = 256, Size = length });
                using var input = File.OpenRead(snapshot);
                input.CopyTo(output);
                output.CloseEntry();
                output.Finish();
            }
            Verify(temporaryArchive, password);
            File.Move(temporaryArchive, archivePath); // No overwrite; publish only after full verification.
        }
        finally
        {
            File.Delete(snapshot);
            File.Delete(temporaryArchive);
            Directory.Delete(workFolder);
        }
    }

    public static Manifest Verify(string archivePath, string password)
    {
        // Never write a decrypted verification database into an external/cloud-sync destination.
        var folder = Path.Combine(Path.GetTempPath(), $"retailforma-backup-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var check = Path.Combine(folder, "verify.db");
        try { return Extract(archivePath, check, password); }
        finally { File.Delete(check); Directory.Delete(folder); }
    }

    public static Manifest Extract(string archivePath, string targetDatabasePath, string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Enter this backup's recovery password.");
        if (File.Exists(targetDatabasePath)) throw new IOException("The restore target already exists.");
        var folder = Path.GetDirectoryName(Path.GetFullPath(targetDatabasePath))!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".restore-{Guid.NewGuid():N}.db");
        var readingEncryptedContent = false;
        var manifestAuthenticated = false;
        try
        {
            using var archive = new ZipFile(File.OpenRead(archivePath)) { Password = password };
            var entries = archive.Cast<ZipEntry>().ToArray();
            if (entries.Length != 2 || entries.Count(e => e.Name is AppPaths.DatabaseName or LegacyMigrationIdentity.Database) != 1 ||
                entries.Count(e => e.Name == "manifest.json") != 1 ||
                entries.Any(e => !e.IsFile || !e.IsCrypted || e.AESKeySize != 256))
                throw new BackupRestoreException(BackupFailure.UnsupportedPackage, "This ZIP is not a supported RetailForma AES-256 backup. Select a backup ZIP created by RetailForma.");
            var metadata = entries.Single(e => e.Name == "manifest.json");
            if (metadata.Size is <= 0 or > 4096) throw new BackupRestoreException(BackupFailure.DamagedContents, "The backup's metadata is invalid or incomplete. Choose another backup copy.");
            using var json = new MemoryStream();
            readingEncryptedContent = true;
            using (var input = archive.GetInputStream(metadata)) CopyBounded(input, json, metadata.Size);
            manifestAuthenticated = true;
            var manifest = JsonSerializer.Deserialize<Manifest>(json.ToArray())
                ?? throw new InvalidDataException("Missing backup manifest.");
            var database = entries.Single(e => e.Name is AppPaths.DatabaseName or LegacyMigrationIdentity.Database);
            if (manifest.Format != 1)
                throw new BackupRestoreException(BackupFailure.UnsupportedPackage, "This backup uses an unsupported backup format. Use a compatible RetailForma version.");
            if (manifest.DatabaseBytes is <= 0 or > MaximumDatabaseBytes || database.Size != manifest.DatabaseBytes ||
                !Version.TryParse(manifest.ApplicationVersion, out var version))
                throw new BackupRestoreException(BackupFailure.DamagedContents, "The backup's metadata is invalid or incomplete. Choose another backup copy.");
            if (version > Version.Parse(AppVersion.Current))
                throw new BackupRestoreException(BackupFailure.NewerVersion, $"This backup was created by RetailForma {version}, newer than this app ({AppVersion.Current}). Update RetailForma before restoring it.");
            EnsureSpace(folder, manifest.DatabaseBytes);
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var input = archive.GetInputStream(database)) CopyBounded(input, output, manifest.DatabaseBytes);
            if (!string.Equals(Hash(temporary), manifest.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("Backup content verification failed.");
            BackupService.Verify(temporary);
            File.Move(temporary, targetDatabasePath);
            return manifest;
        }
        catch (ZipException ex) when (!manifestAuthenticated && ex.Message == "Invalid password for AES")
        {
            // SharpZipLib 1.4.2's explicit AES verifier rejection, not a generic archive error.
            throw new BackupRestoreException(BackupFailure.PassphraseRejected,
                "The passphrase was not accepted for this backup. Re-enter the passphrase used when this ZIP was created. Passphrases are case-sensitive. No restore was staged.");
        }
        catch (ZipException)
        {
            throw new BackupRestoreException(!readingEncryptedContent ? BackupFailure.DamagedArchive :
                manifestAuthenticated ? BackupFailure.DamagedContents : BackupFailure.CannotAuthenticate,
                !readingEncryptedContent ? "The ZIP file is damaged, incomplete, or not a readable ZIP archive. Choose another backup copy." :
                manifestAuthenticated ? "The passphrase opened the backup metadata, but the backup data failed integrity checks. The file is damaged or inconsistent. Choose another backup copy." :
                "The encrypted backup could not be authenticated. Its passphrase may be incorrect or its encrypted data may be damaged; the exact cause cannot be determined. No restore was staged.");
        }
        catch (EndOfStreamException)
        { throw new BackupRestoreException(BackupFailure.DamagedArchive, "The backup file is incomplete or truncated. Choose another backup copy."); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or Microsoft.Data.Sqlite.SqliteException)
        { throw new BackupRestoreException(manifestAuthenticated ? BackupFailure.DamagedContents : BackupFailure.CannotAuthenticate,
            manifestAuthenticated ? "The backup contents are invalid, damaged, or incomplete. Choose another backup copy. No restore was staged." :
            "The encrypted backup could not be authenticated. Its passphrase may be incorrect or its encrypted data may be damaged; the exact cause cannot be determined. No restore was staged."); }
        catch (ICSharpCode.SharpZipLib.SharpZipBaseException)
        { throw new BackupRestoreException(manifestAuthenticated ? BackupFailure.DamagedContents : BackupFailure.CannotAuthenticate,
            manifestAuthenticated ? "The backup data failed integrity checks. Choose another backup copy." :
            "The encrypted backup could not be authenticated. Its passphrase may be incorrect or its encrypted data may be damaged; the exact cause cannot be determined."); }
        finally { File.Delete(temporary); }
    }

    private static void CopyBounded(Stream input, Stream output, long expected)
    {
        var buffer = new byte[81920];
        long total = 0;
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
        {
            total += count;
            if (total > expected) throw new InvalidDataException("Backup entry exceeds its declared size.");
            output.Write(buffer, 0, count);
        }
        if (total != expected) throw new InvalidDataException("Backup entry is incomplete.");
    }

    private static string Hash(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private static void WriteEntry(ZipOutputStream output, string name, byte[] bytes)
    {
        output.PutNextEntry(new ZipEntry(name) { AESKeySize = 256, Size = bytes.Length });
        output.Write(bytes);
        output.CloseEntry();
    }

    private static void EnsureSpace(string folder, long required)
    {
        var root = Path.GetPathRoot(folder)!;
        if (root.StartsWith("\\\\", StringComparison.Ordinal)) return; // UNC quota is checked by actual writes.
        var drive = new DriveInfo(root);
        if (drive.AvailableFreeSpace < required + 64L * 1024 * 1024)
            throw new BackupRestoreException(BackupFailure.InsufficientSpace, "Insufficient free space for a verified backup/restore and a 64 MB safety reserve. Free disk space and try again.");
    }
}
