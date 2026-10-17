using System.IO;
using System.Security.Principal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Services;

namespace RetailForma.Setup;

// Historical plaintext provisioning regression fixture only. Not compiled into the technician/POS packages.
internal sealed class TechnicianProvisioning
{
    public sealed record DatabasePreview(string Path, string Company, bool HasSystemAdministrator);
    private readonly Func<bool> elevated;
    private readonly Func<string> installationOperator;
    private readonly string[] guards;
    public TechnicianProvisioning() : this(
        () => { using var id = WindowsIdentity.GetCurrent(); return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator); },
        () => { using var id = WindowsIdentity.GetCurrent(); return id.Name; },
        [@"Local\RetailFormaDesktop", @"Local\MyPosDesktop"]) { }
    internal TechnicianProvisioning(Func<bool> elevated, Func<string> installationOperator, string[] guards)
    { this.elevated = elevated; this.installationOperator = installationOperator; this.guards = guards; }

    private static string ValidatePath(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Select the full path to the intended retailforma.db.");
        path = Path.GetFullPath(path);
        if (!File.Exists(path)) throw new ArgumentException("Database not found. Launch the installed POS once to create it, then close the POS.");
        if (!Path.GetFileName(path).Equals("retailforma.db", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Select the working retailforma.db, not a backup archive or another file.");
        for (var entry = path; entry != null; entry = Path.GetDirectoryName(entry))
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Linked database paths are not supported. Select its real location.");
        return path;
    }
    private static string Connection(string path, SqliteOpenMode mode) => new SqliteConnectionStringBuilder
    { DataSource = path, Mode = mode, Pooling = false, DefaultTimeout = 1 }.ToString();

    public DatabasePreview Inspect(string path)
    {
        path = ValidatePath(path);
        using var connection = new SqliteConnection(Connection(path, SqliteOpenMode.ReadOnly)); connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = "PRAGMA quick_check;";
        if (!string.Equals(command.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Database integrity check failed. Recover it before provisioning.");
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Users','Settings','AuditLogs');";
        if (Convert.ToInt32(command.ExecuteScalar()) != 3) throw new InvalidDataException("This is not an initialized RetailForma database. No database is created or migrated by this tool.");
        command.CommandText = "SELECT COUNT(*) FROM Users WHERE Role=3;";
        var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
        command.CommandText = "SELECT Value FROM Settings WHERE Key='CompanyName' LIMIT 1;";
        var company = command.ExecuteScalar()?.ToString();
        return new(path, string.IsNullOrWhiteSpace(company) ? "Company name not configured" : company, exists);
    }

    public void Create(DatabasePreview expected, string username, string fullName, string password, string confirmation)
    {
        if (!elevated()) throw new UnauthorizedAccessException("Run Technician Setup as a Windows administrator.");
        if (password != confirmation) throw new ArgumentException("Passwords do not match.");
        var owned = new List<Mutex>();
        try
        {
            foreach (var name in guards)
            {
                var mutex = new Mutex(false, name); bool acquired;
                try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) { mutex.Dispose(); throw new InvalidOperationException("Close RetailForma and the old POS before provisioning."); }
                owned.Add(mutex);
            }
            var current = Inspect(expected.Path);
            if (current != expected) throw new InvalidOperationException("The selected database changed. Browse and review it again.");
            if (current.HasSystemAdministrator) throw new InvalidOperationException("A System Administrator already exists. This tool cannot reset or replace accounts.");
            using var db = new RetailFormaDbContext(new DbContextOptionsBuilder<RetailFormaDbContext>().UseSqlite(Connection(current.Path, SqliteOpenMode.ReadWrite)).Options);
            db.Database.OpenConnection(); db.Database.ExecuteSqlRaw("PRAGMA locking_mode=EXCLUSIVE;");
            SystemAdministratorSetup.CreateFirst(db, username, fullName, password, installationOperator());
        }
        finally { foreach (var mutex in owned.AsEnumerable().Reverse()) { mutex.ReleaseMutex(); mutex.Dispose(); } }
    }
}
