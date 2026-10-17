using System.Security.Cryptography;
using System.Text.Json;
using ICSharpCode.SharpZipLib.Zip;

namespace RetailForma.EncryptedStorage;

/// <summary>Original two-entry AES ZIP compatibility. Legacy plaintext is decrypted only into
/// a service-owned private work folder, immediately converted to SQLCipher, then deleted.
/// Neither the archive's paths nor a privileged client-selected filesystem path are used.</summary>
public static class LegacyBackupReader
{
    private sealed record Manifest(int Format, long DatabaseBytes, string Sha256, DateTimeOffset CreatedUtc,
        string Purpose = "Unknown", string ApplicationVersion = "");

    public static RecoveryBackupPackage.RecoveredBackup Open(string archivePath, string privateWorkDirectory, string passphrase)
    {
        if (string.IsNullOrEmpty(passphrase) || passphrase.Length > 256) throw new ArgumentException("Enter this legacy backup's original passphrase.");
        if (!Path.IsPathFullyQualified(privateWorkDirectory) || !Directory.Exists(privateWorkDirectory)) throw new ArgumentException("Use an existing private service work directory.");
        var id = Guid.NewGuid().ToString("N");
        var plain = Path.Combine(privateWorkDirectory, "legacy-" + id + ".db");
        var encrypted = Path.Combine(privateWorkDirectory, "converted-" + id + ".db");
        var key = RandomNumberGenerator.GetBytes(32); var manifestAuthenticated = false; var manifestReadStarted = false;
        try
        {
            using var archive = new ZipFile(File.OpenRead(archivePath)) { Password = passphrase };
            var entries = archive.Cast<ZipEntry>().ToArray();
            if (entries.Length != 2 || entries.Count(e => e.Name == "manifest.json") != 1 ||
                entries.Count(e => e.Name is "retailforma.db" or "mypos.db") != 1 || entries.Any(e => !e.IsFile || !e.IsCrypted || e.AESKeySize != 256))
                throw new InvalidDataException("This is not a supported legacy AES-256 backup package.");
            var metadata = entries.Single(e => e.Name == "manifest.json");
            if (metadata.Size is < 1 or > 4096) throw new InvalidDataException("The legacy backup metadata is invalid.");
            Manifest manifest;
            using (var json = new MemoryStream())
            {
                manifestReadStarted = true;
                using (var input = archive.GetInputStream(metadata)) Copy(input, json, metadata.Size);
                manifestAuthenticated = true;
                manifest = JsonSerializer.Deserialize<Manifest>(json.ToArray(), new JsonSerializerOptions { MaxDepth = 8,
                    UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidDataException("Missing legacy manifest.");
            }
            var database = entries.Single(e => e.Name is "retailforma.db" or "mypos.db");
            if (manifest.Format != 1 || manifest.DatabaseBytes is < 512 or > RecoveryBackupPackage.MaximumDatabaseBytes || database.Size != manifest.DatabaseBytes ||
                manifest.Sha256 == null || manifest.Sha256.Length != 64 || manifest.Sha256.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidDataException("The authenticated legacy backup metadata is invalid or unsupported.");
            if (!Version.TryParse(manifest.ApplicationVersion, out var version)) throw new InvalidDataException("The legacy backup has no valid application version.");
            var supported = typeof(LegacyBackupReader).Assembly.GetName().Version!;
            if (version > supported) throw new InvalidOperationException($"This backup was made by version {version}, newer than this reader ({supported}). Update before restoring it.");
            StorageSpace.Require(privateWorkDirectory, checked(manifest.DatabaseBytes * 3));
            using (var output = new FileStream(plain, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { using var input = archive.GetInputStream(database); Copy(input, output, manifest.DatabaseBytes); output.Flush(true); }
            using (var file = File.OpenRead(plain))
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(file)), manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The passphrase opened the legacy metadata, but the database checksum failed. Select another copy.");
            SqlCipherConnections.ImportLegacySnapshot(plain, encrypted, key);
            return new RecoveryBackupPackage.RecoveredBackup(encrypted, key, Guid.NewGuid());
        }
        catch (ZipException ex) when (!manifestAuthenticated && ex.Message == "Invalid password for AES")
        {
            CryptographicOperations.ZeroMemory(key); SqlCipherConnectionsCleanup(encrypted);
            throw new ArgumentException("The passphrase was not accepted for this legacy backup. Enter the original, case-sensitive passphrase. The live company was not changed.");
        }
        catch (ZipException)
        {
            CryptographicOperations.ZeroMemory(key); SqlCipherConnectionsCleanup(encrypted);
            throw new InvalidDataException(!manifestReadStarted ? "The legacy ZIP is damaged, truncated or unreadable." : manifestAuthenticated ?
                "The passphrase opened the legacy metadata, but the encrypted database failed integrity checks. Select another backup copy." :
                "The legacy backup could not be authenticated. The passphrase may be incorrect or the encrypted data may be damaged; the exact cause cannot be determined.");
        }
        catch { CryptographicOperations.ZeroMemory(key); SqlCipherConnectionsCleanup(encrypted); throw; }
        finally { SqlCipherConnectionsCleanup(plain); }
    }

    private static void Copy(Stream input, Stream output, long expected)
    {
        var buffer = new byte[65536]; long total = 0; int count;
        try
        {
            while ((count = input.Read(buffer)) != 0)
            { total = checked(total + count); if (total > expected) throw new InvalidDataException("The legacy entry exceeds its declared size."); output.Write(buffer, 0, count); }
            if (total != expected) throw new InvalidDataException("The legacy backup entry is incomplete.");
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }
    private static void SqlCipherConnectionsCleanup(string path) => RecoveryBackupPackage.DeleteWork(path);
}
