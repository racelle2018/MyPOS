using System.Windows;
using System.Windows.Media;
using RetailForma.Core.Services;

namespace RetailForma.Desktop.Controls;

public static class AppearanceResources
{
    public static string[] AvailableFonts => AppearancePreferences.Fonts.Where(name =>
        name is "Default" or "Inter" or "Wix Madefor Text" || IsInstalled(name)).ToArray();
    private static bool IsInstalled(string name) => Fonts.SystemFontFamilies.Any(f =>
        f.FamilyNames.Values.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)));
    public static FontFamily ResolveFont(string name)
    {
        if (name is "Inter" or "Wix Madefor Text")
            return new FontFamily(new Uri("pack://application:,,,/"), $"/RetailForma.Desktop;component/Assets/Fonts/#{name}");
        return AppearancePreferences.Fonts.Contains(name) && IsInstalled(name) ? new FontFamily(name) : ResolveFont("Inter");
    }
    public static ResourceDictionary Build(AppearancePreferences preferences)
    {
        var safe = preferences.Safe();
        if (!AvailableFonts.Contains(safe.Font)) safe = safe with { Font = "Default" };
        var delta = safe.TextSize == "Large" ? 2d : safe.TextSize == "Comfortable" ? 1d : 0d;
        var spacing = safe.TableSpacing == "Comfortable" ? 4d : 0d;
        var resources = new ResourceDictionary
        {
            ["UiFontFamily"] = ResolveFont(safe.Font == "Default" ? "Inter" : safe.Font),
            ["ReportInterFontFamily"] = ResolveFont(safe.Font == "Default" ? "Inter" : safe.Font),
            // Output follows the font family only; print sizes stay independent of UI presets.
            ["PrintInterFontFamily"] = ResolveFont(safe.Font == "Default" ? "Inter" : safe.Font)
        };
        foreach (var size in new[] { 11, 12, 13, 14 }) resources[$"UiFontSize{size}"] = size + delta;
        foreach (var height in new[] { 16, 24, 26, 27, 28, 30, 32, 34, 36, 40, 48 })
            resources[$"UiControlHeight{height}"] = height + delta * 2;
        foreach (var height in new[] { 28, 30 }) resources[$"UiTableRowHeight{height}"] = height + delta * 2 + spacing;
        foreach (var height in new[] { 26, 32 }) resources[$"UiTableHeaderHeight{height}"] = height + delta * 2 + spacing;
        return resources;
    }
    public static void Apply(AppearancePreferences preferences)
    {
        // Replace only presentation tokens. Brushes, borders, print sizes and data remain untouched.
        foreach (System.Collections.DictionaryEntry item in Build(preferences))
            Application.Current.Resources[item.Key] = item.Value;
    }
}
