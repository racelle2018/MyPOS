using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;

namespace MyPos.Core.Services;

public sealed record AppearancePreferences(string Font = "Default", string TextSize = "Standard", string TableSpacing = "Compact")
{
    public static readonly string[] Fonts = { "Default", "Inter", "Wix Madefor Text", "Segoe UI", "Arial", "Calibri", "Tahoma" };
    public static readonly string[] Sizes = { "Standard", "Comfortable", "Large" };
    public static readonly string[] Spacings = { "Compact", "Comfortable" };
    public AppearancePreferences Safe() => new(Fonts.Contains(Font) ? Font : "Default",
        Sizes.Contains(TextSize) ? TextSize : "Standard", Spacings.Contains(TableSpacing) ? TableSpacing : "Compact");
}

public sealed class AppearanceSettingsService(MyPosDbContext db)
{
    public AppearancePreferences Load()
    {
        var values = db.Settings.AsNoTracking().Where(s => s.Key.StartsWith("Appearance.")).ToDictionary(s => s.Key, s => s.Value);
        return new AppearancePreferences(values.GetValueOrDefault("Appearance.Font", "Default"),
            values.GetValueOrDefault("Appearance.TextSize", "Standard"), values.GetValueOrDefault("Appearance.TableSpacing", "Compact")).Safe();
    }
    public void Save(Guid adminId, AppearancePreferences preferences)
    {
        if (!db.Users.AsNoTracking().Any(u => u.Id == adminId && u.IsActive && u.Role == UserRole.Admin))
            throw new InvalidOperationException("Only active administrators can change appearance settings.");
        if (preferences != preferences.Safe()) throw new InvalidOperationException("Choose an approved appearance preset.");
        foreach (var (key, value) in new[] { ("Appearance.Font", preferences.Font), ("Appearance.TextSize", preferences.TextSize), ("Appearance.TableSpacing", preferences.TableSpacing) })
        {
            var setting = db.Settings.FirstOrDefault(s => s.Key == key);
            if (setting == null) db.Settings.Add(new Setting { Key = key, Value = value });
            else setting.Value = value;
        }
        db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, UserId = adminId, Action = "AppearanceSettings", EntityName = "Setting",
            Details = $"Font: {preferences.Font}; text: {preferences.TextSize}; tables: {preferences.TableSpacing}" });
        db.SaveChanges();
    }
}
