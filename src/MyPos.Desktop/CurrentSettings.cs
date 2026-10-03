namespace MyPos.Desktop;

/// <summary>Static bridge for XAML bindings to live application settings.</summary>
public static class CurrentSettings
{
    public static int LowStockThreshold =>
        int.TryParse(AppSettings.Get("LowStockThreshold", "5"), out var value) && value > 0
            ? value : 5;
}
