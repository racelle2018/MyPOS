using RetailForma.Core.Entities;

namespace RetailForma.Desktop;

public static class AppSettings
{
    public static string Get(string key, string fallback = "")
        => App.Service.Settings(App.CurrentUser == null).GetValueOrDefault(key, fallback);

    public static void Set(string key, string value)
    {
        var db = App.Service.TestDatabase ?? throw new InvalidOperationException("Save settings through the typed settings action.");
        var setting = db.Settings.FirstOrDefault(s => s.Key == key);
        if (setting == null) db.Settings.Add(new Setting { Key = key, Value = value });
        else setting.Value = value;
    }
}
