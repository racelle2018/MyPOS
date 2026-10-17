using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace RetailForma.Desktop;

/// <summary>Copy-only, current-Windows-profile migration. Never launches EF or changes source records.</summary>
public static class AppDataMigration
{
    public const string MarkerName = "retailforma-migration.json";
    public static bool Required => File.Exists(Path.Combine(AppPaths.LegacyFolder, LegacyMigrationIdentity.Database)) &&
        !File.Exists(Path.Combine(AppPaths.Folder, MarkerName));

    public static void Migrate(string sourceFolder, string destinationFolder)
    {
        if (!Path.IsPathFullyQualified(sourceFolder) || !Path.IsPathFullyQualified(destinationFolder))
            throw new ArgumentException("Migration requires absolute installation paths.");
        sourceFolder = Path.GetFullPath(sourceFolder).TrimEnd(Path.DirectorySeparatorChar);
        destinationFolder = Path.GetFullPath(destinationFolder).TrimEnd(Path.DirectorySeparatorChar);
        if (sourceFolder.Equals(destinationFolder, StringComparison.OrdinalIgnoreCase) ||
            destinationFolder.StartsWith(sourceFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            sourceFolder.StartsWith(destinationFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Source and destination must be separate installation folders.");
        if (Directory.Exists(destinationFolder) || File.Exists(destinationFolder))
            throw new IOException("RetailForma data already exists. Refusing to overwrite or merge installations; ask for recovery assistance.");
        var sourceDatabase = Path.Combine(sourceFolder, LegacyMigrationIdentity.Database);
        if (!File.Exists(sourceDatabase)) throw new FileNotFoundException("The original database was not found.");
        if ((File.GetAttributes(sourceFolder) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Migration does not follow linked installation folders.");
        var files = new List<string>();
        void Inspect(string folder)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Migration does not follow linked files/folders.");
                if ((attributes & FileAttributes.Directory) != 0) Inspect(entry);
                else files.Add(entry);
            }
        }
        Inspect(sourceFolder);
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = sourceDatabase, Mode = SqliteOpenMode.ReadOnly, Pooling = false, DefaultTimeout = 5 }.ToString());
        source.Open();
        long DataVersion()
        { using var command = source.CreateCommand(); command.CommandText = "PRAGMA data_version"; return Convert.ToInt64(command.ExecuteScalar()); }
        var originalVersion = DataVersion();
        using var secret = source.CreateCommand();
        secret.CommandText = "SELECT Value FROM Settings WHERE Key = 'BackupRecoverySecret' LIMIT 1";
        var password = EncryptedBackup.UnprotectPassword(secret.ExecuteScalar()?.ToString() ?? "");
        EncryptedBackup.ValidatePassword(password);
        var stage = destinationFolder + ".migration-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stage);
        static string Hash(string path) { using var input = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(input)); }
        try
        {
            var database = Path.Combine(stage, AppPaths.DatabaseName);
            using (var target = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = database, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
            { target.Open(); source.BackupDatabase(target); }
            BackupService.Verify(database);
            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(sourceFolder, file);
                if (relative is LegacyMigrationIdentity.Database or "mypos.db-wal" or "mypos.db-shm") continue;
                if (relative.Equals(AppPaths.DatabaseName, StringComparison.OrdinalIgnoreCase) || relative.Equals(MarkerName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Original folder contains conflicting RetailForma files.");
                // Keep old test activations recoverable, but never silently activate them under a new signed product identity.
                if (relative.StartsWith("licensing" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    relative = Path.Combine("legacy-activation", relative);
                var destination = Path.Combine(stage, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: false);
                if (Hash(file) != Hash(destination)) throw new IOException("A source file changed during migration; retry with the old application closed.");
            }
            if (DataVersion() != originalVersion) throw new IOException("Original database changed during migration; close the old application and retry.");
            var pending = Path.Combine(stage, "restore-pending.db");
            if (File.Exists(pending)) BackupService.Verify(pending);
            var backups = Path.Combine(stage, "backups"); Directory.CreateDirectory(backups);
            EncryptedBackup.Create(database, Path.Combine(backups, "retailforma-migration-safety-" + Guid.NewGuid().ToString("N") + ".zip"), password, "Safety");
            if (DataVersion() != originalVersion) throw new IOException("Original database changed before migration could finish.");
            File.WriteAllText(Path.Combine(stage, MarkerName), JsonSerializer.Serialize(new
            { Format = 1, Source = sourceFolder, DatabaseSha256 = Hash(database), MigratedUtc = DateTimeOffset.UtcNow, OriginalPreserved = true }));
            Directory.Move(stage, destinationFolder); // Atomic folder publication; never overwrite.
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true); } // Only our unpublished staging copy.
    }
}
