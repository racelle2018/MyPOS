using System.IO;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.Setup;
using Xunit;

namespace RetailForma.Setup.Tests;

public sealed class ProvisioningTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "retailforma-technician-tests-" + Guid.NewGuid().ToString("N"));
    private readonly RetailFormaDbContext db;
    private readonly string path;
    private const string Password = "Test-only individual passphrase!";
    private readonly string guard = @"Local\RetailFormaTechnicianTest-" + Guid.NewGuid().ToString("N");
    public ProvisioningTests()
    {
        Directory.CreateDirectory(folder); path = Path.Combine(folder, "retailforma.db");
        db = new(new DbContextOptionsBuilder<RetailFormaDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);
        db.Database.EnsureCreated();
        db.Settings.Add(new Setting { Key = "CompanyName", Value = "ISOLATED TEST COMPANY" });
        db.Users.Add(new User { Username = "owner", Role = UserRole.Admin, FullName = "Test Owner", PasswordHash = "unchanged-owner-hash" }); db.SaveChanges();
    }
    private TechnicianProvisioning Service(bool allowed = true) => new(() => allowed, () => "isolated-test-operator", [guard]);
    public void Dispose() { db.Dispose(); Directory.Delete(folder, true); }

    [Fact]
    public void Inspect_is_read_only_and_create_is_first_only_with_audited_identity_and_no_secret()
    {
        var service = Service(); var before = File.ReadAllBytes(path); var preview = service.Inspect(path);
        Assert.Equal("ISOLATED TEST COMPANY", preview.Company); Assert.False(preview.HasSystemAdministrator);
        Assert.Equal(before, File.ReadAllBytes(path));
        service.Create(preview, "technician", "Individual Technician", Password, Password);
        var created = db.Users.AsNoTracking().Single(u => u.Role == UserRole.SystemAdministrator);
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, created.PasswordHash));
        Assert.Equal("unchanged-owner-hash", db.Users.AsNoTracking().Single(u => u.Username == "owner").PasswordHash);
        Assert.Contains(db.AuditLogs, a => a.Action == "SystemAdministratorProvisioned" && a.Details!.Contains("isolated-test-operator"));
        Assert.All(db.AuditLogs, a => Assert.DoesNotContain(Password, a.Details ?? ""));
        Assert.Throws<InvalidOperationException>(() => service.Create(preview, "othertech", "Other", Password, Password));
        Assert.Equal(2, db.Users.Count());
    }

    [Fact]
    public void Missing_database_and_non_elevated_or_mismatched_credentials_do_not_create_accounts()
    {
        var service = Service(); var preview = service.Inspect(path);
        var missing = Path.Combine(folder, "missing", "retailforma.db");
        Assert.Throws<ArgumentException>(() => service.Inspect(missing)); Assert.False(File.Exists(missing));
        Assert.Throws<UnauthorizedAccessException>(() => Service(false).Create(preview, "technician", "Tech", Password, Password));
        Assert.Throws<ArgumentException>(() => service.Create(preview, "technician", "Tech", Password, "wrong"));
        Assert.Single(db.Users); Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void Replaced_company_preview_and_existing_disabled_technical_account_are_rejected()
    {
        var service = Service(); var preview = service.Inspect(path);
        db.Settings.Single().Value = "DIFFERENT COMPANY"; db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => service.Create(preview, "technician", "Tech", Password, Password));
        db.Users.Add(new User { Username = "disabledtech", FullName = "Disabled", Role = UserRole.SystemAdministrator, IsActive = false, PasswordHash = "unchanged" }); db.SaveChanges();
        Assert.True(service.Inspect(path).HasSystemAdministrator);
        Assert.Throws<InvalidOperationException>(() => service.Create(service.Inspect(path), "newtech", "Tech", Password, Password));
        Assert.Equal(2, db.Users.Count());
    }

    [Fact]
    public async Task Running_instance_guard_blocks_provisioning()
    {
        using var ready = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var holding = Task.Run(() => { using var mutex = new Mutex(false, guard); mutex.WaitOne(); ready.Set(); release.Wait(); mutex.ReleaseMutex(); });
        ready.Wait();
        try { var service = Service(); Assert.Throws<InvalidOperationException>(() => service.Create(service.Inspect(path), "technician", "Tech", Password, Password)); Assert.Single(db.Users); }
        finally { release.Set(); await holding; }
    }

    [Fact]
    public void Failed_account_write_rolls_back_without_success_audit()
    {
        db.Database.ExecuteSqlRaw("CREATE TRIGGER RejectTechnical BEFORE INSERT ON Users WHEN NEW.Role=3 BEGIN SELECT RAISE(ABORT,'test failure'); END;");
        var service = Service(); Assert.Throws<DbUpdateException>(() => service.Create(service.Inspect(path), "technician", "Tech", Password, Password));
        Assert.Single(db.Users); Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void Another_database_writer_blocks_provisioning_without_partial_rows()
    {
        var service = Service(); var preview = service.Inspect(path);
        using (var transaction = db.Database.BeginTransaction())
        { Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => service.Create(preview, "technician", "Tech", Password, Password)); }
        Assert.Single(db.Users); Assert.Empty(db.AuditLogs);
    }
}
