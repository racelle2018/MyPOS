namespace RetailForma.Desktop;

/// <summary>The build metadata is the single source for displayed application versions.</summary>
public static class AppVersion
{
    public static string Current => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "Unknown";
}
