using System.Security.Cryptography;
using System.Text.Json;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Data.Sqlite;
using RetailForma.Security;

namespace RetailForma.EncryptedStorage;

/// <summary>
/// V2 portable ZIP: public bounded key envelopes, authenticated encrypted manifest and SQLCipher snapshot.
/// Both owner and developer can recover the backup key independently; private recovery keys never enter POS packages.
/// All work/output paths are selected by the trusted host, never extracted from ZIP filenames.
/// </summary>
public static class RecoveryBackupPackage
{
    public const long MaximumDatabaseBytes = 16L * 1024 * 1024 * 1024;
    public sealed record RecoveryHeader(int Format, Guid InstallationId, Guid BackupId, string OwnerEnvelope, string DeveloperEnvelope);
    private sealed record Manifest(int Format, long DatabaseBytes, string DatabaseSha256, string RecoveryHeaderSha256,
        DateTimeOffset CreatedUtc, string Purpose);
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 8, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    public static void Create(SqliteConnection source, string archivePath, string privateWorkDirectory,
        string ownerPassphrase, RSA developerPublicKey, Guid installationId, string purpose = "Manual")
    {
        OwnerRecoveryEnvelope.ValidatePassphrase(ownerPassphrase);
        if (purpose is not ("Manual" or "Automatic" or "Safety")) throw new ArgumentException("Invalid backup purpose.");
        if (!Path.IsPathFullyQualified(archivePath) || !Path.IsPathFullyQualified(privateWorkDirectory)) throw new ArgumentException("Use full trusted archive/work paths.");
        if (File.Exists(archivePath)) throw new IOException("The backup destination already exists.");
        using (var pages = source.CreateCommand())
        {
            pages.CommandText = "PRAGMA page_count;"; var count = Convert.ToInt64(pages.ExecuteScalar());
            pages.CommandText = "PRAGMA page_size;"; var size = checked(count * Convert.ToInt64(pages.ExecuteScalar()));
            if (size > MaximumDatabaseBytes) throw new InvalidDataException("The company exceeds the supported backup size.");
            StorageSpace.Require(privateWorkDirectory, checked(size * 3)); StorageSpace.Require(archivePath, size);
        }
        var key = RandomNumberGenerator.GetBytes(32);
        var backupId = Guid.NewGuid(); var context = new RecoveryContext(installationId, backupId, RecoveryPurpose.BackupKey);
        var snapshot = Path.Combine(privateWorkDirectory, "snapshot-" + Guid.NewGuid().ToString("N") + ".db");
        var temporary = Path.Combine(Path.GetDirectoryName(archivePath)!, ".backup-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            SqlCipherConnections.ExportEncrypted(source, snapshot, key);
            var size = new FileInfo(snapshot).Length;
            if (size > MaximumDatabaseBytes) throw new InvalidDataException("The backup exceeds the supported database size.");
            var header = new RecoveryHeader(2, installationId, backupId,
                Convert.ToBase64String(OwnerRecoveryEnvelope.Seal(key, context, ownerPassphrase)),
                Convert.ToBase64String(DeveloperRecoveryEnvelope.Seal(key, context, developerPublicKey)));
            var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header, Json);
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipOutputStream(file) { IsStreamOwner = false })
            {
                zip.SetLevel(0);
                WriteEntry(zip, "recovery.json", headerBytes, encrypted: false);
                zip.Password = Convert.ToHexString(key);
                WriteEntry(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(new Manifest(2, size, HashFile(snapshot),
                    Hash(headerBytes), DateTimeOffset.UtcNow, purpose), Json), encrypted: true);
                zip.PutNextEntry(new ZipEntry("retailforma.db") { AESKeySize = 256, Size = size });
                using (var input = new FileStream(snapshot, FileMode.Open, FileAccess.Read, FileShare.Read)) input.CopyTo(zip);
                zip.CloseEntry(); zip.Finish(); file.Flush(true);
            }
            using (var verified = OpenWithOwner(temporary, privateWorkDirectory, ownerPassphrase)) { }
            File.Move(temporary, archivePath); // No overwrite; publish only after complete recovery validation.
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            DeleteWork(snapshot); File.Delete(temporary);
        }
    }

    public static RecoveryHeader ReadHeader(string archivePath)
    {
        using var file = File.OpenRead(archivePath); using var zip = new ZipFile(file);
        return ReadHeader(zip).Header;
    }

    public static RecoveredBackup OpenCompatibleWithOwner(string archivePath, string privateWorkDirectory, string passphrase)
    {
        using (var input = File.OpenRead(archivePath))
        {
            Span<byte> header = stackalloc byte[16];
            if (input.Read(header) == 16 && header.SequenceEqual("SQLite format 3\0"u8))
            {
                if (!Path.IsPathFullyQualified(privateWorkDirectory) || !Directory.Exists(privateWorkDirectory))
                    throw new ArgumentException("Use a private service work directory for legacy conversion.");
                var destination = Path.Combine(privateWorkDirectory, "converted-" + Guid.NewGuid().ToString("N") + ".db");
                var key = RandomNumberGenerator.GetBytes(32);
                try
                {
                    SqlCipherConnections.ImportLegacySnapshot(archivePath, destination, key);
                    return new RecoveredBackup(destination, key, Guid.NewGuid());
                }
                catch { CryptographicOperations.ZeroMemory(key); DeleteWork(destination); throw; }
            }
        }
        using (var file = File.OpenRead(archivePath))
        using (var zip = new ZipFile(file))
        {
            // Presence of a V2 header commits to V2 validation. A damaged V2 package is never
            // retried as legacy, and legacy archives never acquire developer recovery retroactively.
            if (zip.GetEntry("recovery.json") == null) return LegacyBackupReader.Open(archivePath, privateWorkDirectory, passphrase);
        }
        return OpenWithOwner(archivePath, privateWorkDirectory, passphrase);
    }

    public static RecoveredBackup OpenWithOwner(string archivePath, string privateWorkDirectory, string passphrase)
        => Open(archivePath, privateWorkDirectory, header => OwnerRecoveryEnvelope.Open(Convert.FromBase64String(header.OwnerEnvelope),
            Context(header), passphrase));

    public static RecoveredBackup OpenWithDeveloper(string archivePath, string privateWorkDirectory, RSA privateKey)
        => Open(archivePath, privateWorkDirectory, header => DeveloperRecoveryEnvelope.Open(Convert.FromBase64String(header.DeveloperEnvelope),
            Context(header), privateKey));

    /// <summary>Programmer-only offline recovery: verify with the private key, then create a new archive with a new owner phrase/key.</summary>
    public static void ReprotectWithDeveloper(string inputPath, string outputPath, string privateWorkDirectory, RSA privateKey, string newOwnerPassphrase)
    {
        using var recovered = OpenWithDeveloper(inputPath, privateWorkDirectory, privateKey);
        using var source = recovered.OpenDatabase();
        Create(source, outputPath, privateWorkDirectory, newOwnerPassphrase, privateKey, recovered.InstallationId);
    }

    private static RecoveredBackup Open(string archivePath, string privateWorkDirectory, Func<RecoveryHeader, byte[]> unlock)
    {
        if (!Path.IsPathFullyQualified(privateWorkDirectory) || !Directory.Exists(privateWorkDirectory)) throw new ArgumentException("Select an existing private recovery work directory.");
        using var file = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var zip = new ZipFile(file);
        var (header, headerBytes) = ReadHeader(zip);
        var key = unlock(header); // No database output is created until credentials have been accepted.
        var outputPath = Path.Combine(privateWorkDirectory, "recovered-" + Guid.NewGuid().ToString("N") + ".db");
        var created = false;
        try
        {
            zip.Password = Convert.ToHexString(key);
            var manifestEntry = zip.GetEntry("manifest.json")!;
            if (!manifestEntry.IsCrypted || manifestEntry.AESKeySize != 256) throw new InvalidDataException("Backup manifest encryption is invalid.");
            using var manifestStream = zip.GetInputStream(manifestEntry);
            var manifest = JsonSerializer.Deserialize<Manifest>(ReadBounded(manifestStream, 32768), Json) ?? throw new InvalidDataException("Missing backup manifest.");
            if (manifest.Format != 2 || manifest.DatabaseBytes is < 512 or > MaximumDatabaseBytes || manifest.RecoveryHeaderSha256 != Hash(headerBytes) ||
                manifest.DatabaseSha256.Length != 64 || manifest.DatabaseSha256.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidDataException("Backup recovery metadata or manifest was altered or is unsupported.");
            var database = zip.GetEntry("retailforma.db")!;
            if (!database.IsCrypted || database.AESKeySize != 256 || database.Size != manifest.DatabaseBytes) throw new InvalidDataException("Backup database entry is invalid.");
            StorageSpace.Require(privateWorkDirectory, checked(manifest.DatabaseBytes * 3));
            using (var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                using var input = zip.GetInputStream(database); var buffer = new byte[65536]; long total = 0; int count;
                while ((count = input.Read(buffer)) != 0)
                {
                    total = checked(total + count);
                    if (total > manifest.DatabaseBytes) throw new InvalidDataException("Backup expands beyond its authenticated size.");
                    output.Write(buffer, 0, count);
                }
                if (total != manifest.DatabaseBytes) throw new InvalidDataException("Backup database is incomplete.");
                output.Flush(true);
            }
            if (HashFile(outputPath) != manifest.DatabaseSha256) throw new InvalidDataException("Backup database checksum failed.");
            using (var check = SqlCipherConnections.OpenExisting(outputPath, key)) SqlCipherConnections.VerifyIntegrity(check);
            return new RecoveredBackup(outputPath, key, header.InstallationId);
        }
        catch { CryptographicOperations.ZeroMemory(key); if (created) DeleteWork(outputPath); throw; }
    }

    private static (RecoveryHeader Header, byte[] Bytes) ReadHeader(ZipFile zip)
    {
        if (zip.Count != 3) throw new InvalidDataException("Unsupported backup contents. Older backups require the original legacy restore workflow/passphrase.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (ZipEntry entry in zip)
            if (!entry.IsFile || !names.Add(entry.Name) || entry.Name is not ("recovery.json" or "manifest.json" or "retailforma.db"))
                throw new InvalidDataException("Unexpected or duplicate backup entries.");
        var recovery = zip.GetEntry("recovery.json")!;
        if (recovery.IsCrypted || recovery.Size is < 1 or > 16384) throw new InvalidDataException("Invalid public recovery header.");
        using var stream = zip.GetInputStream(recovery); var bytes = ReadBounded(stream, 16384);
        var header = JsonSerializer.Deserialize<RecoveryHeader>(bytes, Json) ?? throw new InvalidDataException("Missing recovery header.");
        if (header.Format != 2 || header.InstallationId == Guid.Empty || header.BackupId == Guid.Empty ||
            header.OwnerEnvelope == null || header.OwnerEnvelope.Length > 256 || header.DeveloperEnvelope == null || header.DeveloperEnvelope.Length > 2048)
            throw new InvalidDataException("Unsupported recovery metadata.");
        return (header, bytes);
    }
    private static RecoveryContext Context(RecoveryHeader header) => new(header.InstallationId, header.BackupId, RecoveryPurpose.BackupKey);
    private static byte[] ReadBounded(Stream source, int maximum)
    {
        using var output = new MemoryStream(); var buffer = new byte[4096]; int count;
        while ((count = source.Read(buffer)) != 0)
        { if (output.Length + count > maximum) throw new InvalidDataException("Backup metadata is too large."); output.Write(buffer, 0, count); }
        return output.ToArray();
    }
    private static void WriteEntry(ZipOutputStream zip, string name, byte[] bytes, bool encrypted)
    { zip.PutNextEntry(new ZipEntry(name) { Size = bytes.Length, AESKeySize = encrypted ? 256 : 0 }); zip.Write(bytes); zip.CloseEntry(); }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string HashFile(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    internal static void DeleteWork(string path)
    {
        // Only caller-generated private snapshot names, never archive-supplied names or directories.
        File.Delete(path); File.Delete(path + "-wal"); File.Delete(path + "-shm"); File.Delete(path + "-journal");
    }

    public sealed class RecoveredBackup : IDisposable
    {
        private readonly string path;
        private readonly byte[] key;
        private bool disposed;
        public Guid InstallationId { get; }
        internal RecoveredBackup(string path, byte[] key, Guid installationId) { this.path = path; this.key = key; InstallationId = installationId; }
        public SqliteConnection OpenDatabase() { ObjectDisposedException.ThrowIf(disposed, this); return SqlCipherConnections.OpenExisting(path, key); }
        public void Dispose()
        { if (disposed) return; disposed = true; CryptographicOperations.ZeroMemory(key); DeleteWork(path); }
    }
}
