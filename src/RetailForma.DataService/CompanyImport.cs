using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.EncryptedStorage;
using RetailForma.ServiceApplication;

namespace RetailForma.DataService;

/// <summary>Elevated initial-import workflow, separate from authenticated in-service restore.
/// Import proves possession of the original encrypted backup and passphrase, never promotes existing accounts.</summary>
internal sealed class CompanyImport(ServiceDatabase storage) : IDisposable
{
    private Guid id;
    private ApplicationSession? owner;
    private string? folder;
    private long length, written;
    private string sha = "";
    private long started;
    private RecoveryBackupPackage.RecoveredBackup? recovered;
    private string phrase = "";
    private string Path => System.IO.Path.Combine(folder!, "import.zip");

    public object? Execute(ApplicationSession session, Operation operation, JsonElement payload)
    {
        if (storage.Configured) throw new InvalidOperationException("Initial import is disabled because protected company storage already exists.");
        if (owner != null && System.Diagnostics.Stopwatch.GetElapsedTime(started) >= TimeSpan.FromMinutes(15)) Dispose();
        if (operation == Operation.BeginCompanyImport)
        {
            var input = WireJson.Read<RestoreUploadInput>(payload);
            if (owner != null) throw new InvalidOperationException("Cancel or finish the active initial import first.");
            if (input.Length is < 128 or > RecoveryBackupPackage.MaximumDatabaseBytes + 1024 * 1024 || input.Sha256 == null ||
                input.Sha256.Length != 64 || input.Sha256.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Invalid import file size or checksum.");
            StorageSpace.Require(storage.WorkDirectory, checked(input.Length * 3));
            storage.CreatePrivateDirectory(storage.WorkDirectory);
            id = Guid.NewGuid(); folder = System.IO.Path.Combine(storage.WorkDirectory, "initial-import-" + id.ToString("N")); storage.CreatePrivateDirectory(folder);
            using (new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            owner = session; length = input.Length; sha = input.Sha256.ToUpperInvariant(); written = 0; started = System.Diagnostics.Stopwatch.GetTimestamp(); return id;
        }
        if (!ReferenceEquals(owner, session) || owner == null) throw new UnauthorizedAccessException("Initial import belongs to another connection or has expired.");
        switch (operation)
        {
            case Operation.WriteCompanyImportChunk:
                var chunk = WireJson.Read<RestoreChunkInput>(payload);
                if (chunk.TransferId != id || recovered != null || chunk.Bytes == null || chunk.Bytes.Length is < 1 or > ServiceBackups.ChunkBytes ||
                    chunk.Offset != written || chunk.Bytes.LongLength > length - written) throw new ArgumentException("Invalid initial-import chunk.");
                using (var output = new FileStream(Path, FileMode.Open, FileAccess.Write, FileShare.None))
                { if (output.Length != written) throw new InvalidDataException("Private upload changed."); output.Position = written; output.Write(chunk.Bytes); output.Flush(true); }
                written += chunk.Bytes.Length; return written;
            case Operation.PreviewCompanyImport:
                var prepare = WireJson.Read<PrepareRestoreInput>(payload);
                if (prepare.TransferId != id || written != length || recovered != null) throw new InvalidOperationException("Upload and validate the complete archive first.");
                RetailForma.Security.OwnerRecoveryEnvelope.ValidatePassphrase(prepare.Passphrase);
                using (var input = File.OpenRead(Path))
                    if (Convert.ToHexString(SHA256.HashData(input)) != sha) throw new InvalidDataException("The uploaded backup checksum failed.");
                try { recovered = RecoveryBackupPackage.OpenCompatibleWithOwner(Path, folder!, prepare.Passphrase); }
                catch (CryptographicException) { throw new ArgumentException("The passphrase could not unlock this backup, or its recovery header was altered. No company was created."); }
                try
                {
                    using var connection = recovered.OpenDatabase();
                    using var db = new RetailFormaDbContext(new DbContextOptionsBuilder<RetailFormaDbContext>().UseSqlite(connection, false).Options);
                    CompanySchema.BeforeMigration(db); DbInitializer.Initialize(db); CompanySchema.AfterMigration(db); SqlCipherConnections.VerifyIntegrity(connection); phrase = prepare.Passphrase;
                    return new RestorePreview(id, db.Settings.Find("CompanyName")?.Value ?? "", db.Users.Count(), db.Sales.Count(), sha);
                }
                catch { recovered.Dispose(); recovered = null; throw; }
            case Operation.CommitCompanyImport:
                if (WireJson.Read<IdInput>(payload).Id != id || recovered == null) throw new InvalidOperationException("Review the validated import first.");
                storage.ProvisionFromBackup(recovered, phrase, session.WindowsSid); Dispose(); return storage.Status();
            case Operation.CancelCompanyImport:
                if (WireJson.Read<IdInput>(payload).Id != id) throw new ArgumentException("Unknown import."); Dispose(); return null;
            default: throw new UnauthorizedAccessException("Unsupported initial-import operation.");
        }
    }
    public void Dispose()
    {
        recovered?.Dispose(); recovered = null; phrase = "";
        if (folder != null) { File.Delete(Path); if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder); }
        owner = null; folder = null; id = Guid.Empty;
    }
}
