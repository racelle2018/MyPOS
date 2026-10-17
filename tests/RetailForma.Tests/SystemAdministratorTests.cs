using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;

namespace RetailForma.Tests;

public sealed class SystemAdministratorTests
{
    [Fact]
    public void Owner_cannot_manage_technical_accounts_and_inactive_users_have_no_admin_access()
    {
        var owner = new User { Role = UserRole.Admin };
        var technical = new User { Role = UserRole.SystemAdministrator };
        var cashier = new User { Role = UserRole.Cashier };
        Assert.False(UserAccess.CanManageUser(owner, technical));
        Assert.True(UserAccess.CanManageUser(owner, cashier));
        Assert.True(UserAccess.CanManageUser(technical, owner));
        Assert.True(UserAccess.CanManageUser(technical, technical));
        technical.IsActive = false;
        Assert.False(UserAccess.IsAdministrator(technical));
        Assert.False(UserAccess.CanManageUser(cashier, owner));
        Assert.Equal(1, (int)UserRole.Admin);
        Assert.Equal(2, (int)UserRole.Cashier);
        Assert.Equal("Owner", UserAccess.RoleName(UserRole.Admin));
    }

    [Fact]
    public void Setup_creates_once_audits_and_never_promotes_or_resets_existing_accounts()
    {
        using var db = new RetailFormaDbContext(new DbContextOptionsBuilder<RetailFormaDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection(); DbInitializer.Initialize(db);
        var owner = db.Users.First(u => u.Role == UserRole.Admin);
        Assert.Throws<ArgumentException>(() => SystemAdministratorSetup.CreateFirst(db, owner.Username, "Technician", "Test-only long passphrase!", "test operator"));
        var user = SystemAdministratorSetup.CreateFirst(db, "technician", "Individual technician", "Test-only long passphrase!", "test operator");
        Assert.True(BCrypt.Net.BCrypt.Verify("Test-only long passphrase!", user.PasswordHash));
        Assert.Equal(UserRole.Admin, owner.Role);
        Assert.Contains(db.AuditLogs, log => log.Action == "SystemAdministratorProvisioned" && log.UserId == user.Id);
        Assert.Throws<InvalidOperationException>(() => SystemAdministratorSetup.CreateFirst(db, "othertech", "Other", "Other test-only long phrase!", "operator"));
        // Full Owner functionality is available to the technical role, including translated EF guards.
        new ShiftService(db).ConfigureDrawer(user.Id, DrawerMode.Shared);
        new ExpandedReportService(db).Validate(new ReportScope(db.Branches.First().Id, user.Id,
            DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today)), adminOnly: true);
    }
}
