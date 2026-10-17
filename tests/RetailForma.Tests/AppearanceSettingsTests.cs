using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;
using Xunit;

namespace RetailForma.Tests;

public class AppearanceSettingsTests
{
    private static RetailFormaDbContext Db()
    {
        var db = new RetailFormaDbContext(new DbContextOptionsBuilder<RetailFormaDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection(); DbInitializer.Initialize(db); return db;
    }
    [Fact]
    public void Settings_are_admin_only_persistent_audited_and_resettable()
    {
        using var db = Db(); var admin = db.Users.Single(); var service = new AppearanceSettingsService(db);
        Assert.Equal(new AppearancePreferences(), service.Load());
        var cashier = new User { Username = "appearance-cashier", FullName = "Cashier", Role = UserRole.Cashier };
        db.Users.Add(cashier); db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => service.Save(cashier.Id, new("Inter", "Large", "Comfortable")));
        Assert.Empty(db.Settings.Where(s => s.Key.StartsWith("Appearance.")));
        var selected = new AppearancePreferences("Wix Madefor Text", "Large", "Comfortable");
        service.Save(admin.Id, selected);
        Assert.Equal(selected, new AppearanceSettingsService(db).Load());
        Assert.Single(db.AuditLogs.Where(a => a.Action == "AppearanceSettings"));
        service.Save(admin.Id, new());
        Assert.Equal(new AppearancePreferences(), service.Load());
        admin.IsActive = false; db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => service.Save(admin.Id, selected));
    }
    [Theory]
    [InlineData("Segoe UI")]
    [InlineData("Arial")]
    [InlineData("Calibri")]
    [InlineData("Tahoma")]
    public void Approved_common_fonts_are_persisted_without_arbitrary_font_names(string font)
    {
        using var db = Db(); var service = new AppearanceSettingsService(db);
        var preferences = new AppearancePreferences(font);
        service.Save(db.Users.Single().Id, preferences);
        Assert.Equal(preferences, service.Load());
    }

    [Theory]
    [InlineData("Comic Sans MS", "Standard", "Compact")]
    [InlineData("Inter", "99", "Compact")]
    [InlineData("Inter", "Large", "Custom")]
    public void Unsupported_values_are_rejected_and_corrupt_saved_values_fall_back(string font, string size, string spacing)
    {
        using var db = Db(); var service = new AppearanceSettingsService(db);
        var invalid = new AppearancePreferences(font, size, spacing);
        Assert.Throws<InvalidOperationException>(() => service.Save(db.Users.Single().Id, invalid));
        db.Settings.AddRange(new Setting { Key = "Appearance.Font", Value = font },
            new Setting { Key = "Appearance.TextSize", Value = size }, new Setting { Key = "Appearance.TableSpacing", Value = spacing });
        db.SaveChanges();
        Assert.Equal(invalid.Safe(), service.Load());
    }
}
