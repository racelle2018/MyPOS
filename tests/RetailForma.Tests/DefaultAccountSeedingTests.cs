using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;

namespace RetailForma.Tests;

public sealed class DefaultAccountSeedingTests
{
    [Fact]
    public void RenamingStarterAccountNeverRecreatesDefaultOwnerOnRestart()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var db = new RetailFormaDbContext(new DbContextOptionsBuilder<RetailFormaDbContext>().UseSqlite(connection).Options);
        DbInitializer.Initialize(db); var starter = db.Users.Single(); starter.Username = "individualowner"; db.SaveChanges();
        DbInitializer.Initialize(db);
        Assert.Single(db.Users); Assert.False(db.Users.Any(u => u.Username == "admin"));
    }
}
