using System.Windows;
using System.Windows.Controls;
using MyPos.Core.Services;
using MyPos.Desktop.Controls;

namespace MyPos.Desktop.Views;

public partial class AppearanceSettingsView : UserControl
{
    public AppearanceSettingsView()
    {
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators may change appearance.");
        InitializeComponent();
        FontBox.ItemsSource = AppearanceResources.AvailableFonts.Where(f => f != "Inter").Select(f => new AppearanceChoice(f,
            f == "Default" ? "Default (Inter)" : f)).ToArray();
        SizeBox.ItemsSource = new[] { new AppearanceChoice("Standard", "Default (12 DIP tables)"),
            new AppearanceChoice("Comfortable", "Comfortable (13 DIP tables)"), new AppearanceChoice("Large", "Large (14 DIP tables)") };
        SpacingBox.ItemsSource = new[] { new AppearanceChoice("Compact", "Default (Compact)"), new AppearanceChoice("Comfortable", "Comfortable (+4 DIP)") };
        var saved = new AppearanceSettingsService(App.Db).Load();
        var available = AppearanceResources.AvailableFonts.Contains(saved.Font);
        Select(available ? saved : saved with { Font = "Default" });
        SampleGrid.ItemsSource = new[] { new { Product = "SAMPLE PRODUCT", Amount = "₱1,250.00" } };
        Preview();
        if (!available) SavedFeedbackText.Text = "The saved font is not installed. Default is shown; Save to keep this choice.";
        foreach (var box in new[] { FontBox, SizeBox, SpacingBox })
            box.SelectionChanged += (_, _) => { Preview(); SavedFeedbackText.Text = "Unsaved changes"; };
    }
    public sealed record AppearanceChoice(string Value, string Label);
    private AppearancePreferences Selected => new(FontBox.SelectedValue as string ?? "Default",
        SizeBox.SelectedValue as string ?? "Standard", SpacingBox.SelectedValue as string ?? "Compact");
    private void Select(AppearancePreferences value)
    { FontBox.SelectedValue = value.Font == "Inter" ? "Default" : value.Font; SizeBox.SelectedValue = value.TextSize; SpacingBox.SelectedValue = value.TableSpacing; }
    private void Preview()
    {
        var resources = AppearanceResources.Build(Selected);
        PreviewPanel.Resources = resources;
        var family = (System.Windows.Media.FontFamily)resources["ReportInterFontFamily"];
        var face = new System.Windows.Media.Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var name = face.TryGetGlyphTypeface(out var glyph) ? glyph.FamilyNames.Values.First() : family.Source;
        PreviewSummaryText.Text = $"Sample font: {name} · {resources["UiFontSize12"]} DIP text · {resources["UiTableRowHeight28"]} DIP rows";
    }
    private void Preview_Click(object sender, RoutedEventArgs e)
    { Preview(); SavedFeedbackText.Text = "Unsaved changes"; MyPos.Desktop.Dialogs.ActionDialog.Information("Preview updated. Save to apply these choices.", "Appearance preview", Window.GetWindow(this)); }
    private void Reset_Click(object sender, RoutedEventArgs e)
    { Select(new()); Preview(); SavedFeedbackText.Text = "Unsaved changes"; MyPos.Desktop.Dialogs.ActionDialog.Information("Defaults selected. Save to apply them.", "Appearance reset", Window.GetWindow(this)); }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("change appearance")) return;
        try
        {
            new AppearanceSettingsService(App.Db).Save(App.CurrentUser!.Id, Selected);
            AppearanceResources.Apply(Selected); Preview();
            SavedFeedbackText.Text = "";
            MyPos.Desktop.Dialogs.ActionDialog.Success("Appearance settings saved and applied.", "Appearance", Window.GetWindow(this));
        }
        catch (InvalidOperationException ex)
        { MyPos.Desktop.Dialogs.ActionDialog.Show(ex.Message, "Appearance", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
