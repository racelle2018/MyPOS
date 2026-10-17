using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using Xunit;

namespace RetailForma.Tests;

public class DbSeedTests
{
    private RetailFormaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<RetailFormaDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new RetailFormaDbContext(options);
        db.Database.OpenConnection();   // keeps the in-memory database alive
        return db;
    }

    [Fact]
    public void Seed_creates_system_accounts_and_admin()
    {
        using var db = CreateDb();
        DbInitializer.Initialize(db);

        Assert.Equal(14, db.Accounts.Count());
        Assert.Contains(db.Users, u => u.Username == "admin" && u.Role == UserRole.Admin);
        Assert.Equal("false", db.Settings.First(s => s.Key == "ReceiptIssuanceEnabled").Value);
    }

    [Fact]
    public void Seed_is_idempotent()
    {
        using var db = CreateDb();
        DbInitializer.Initialize(db);
        DbInitializer.Initialize(db);   // second run must not duplicate

        Assert.Equal(14, db.Accounts.Count());
        Assert.Single(db.Users, u => u.Username == "admin");
    }
}
