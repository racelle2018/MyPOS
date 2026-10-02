using MyPos.Core.Entities;

namespace MyPos.Desktop;

public static class AppSettings
{
    public static string Get(string key, string fallback = "")
        => App.Db.Settings.FirstOrDefault(s => s.Key == key)?.Value ?? fallback;

    public static void Set(string key, string value)
    {
        var setting = App.Db.Settings.FirstOrDefault(s => s.Key == key);
        if (setting == null) App.Db.Settings.Add(new Setting { Key = key, Value = value });
        else setting.Value = value;
    }
}
