using System.Windows;
using System.Windows.Controls;
using MyPos.Core.Services;

namespace MyPos.Desktop.Views;

public partial class CashCountingSettingsView : UserControl
{
    public CashCountingSettingsView()
    {
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators may configure cash counting.");
        InitializeComponent();
        MinimumBox.ItemsSource = ShiftService.Denominations.Reverse().Select(d => new DenominationChoice(d, $"₱{d:N2}")).ToList();
        try { MinimumBox.SelectedValue = new ShiftService(App.Db).GetMinimumCountingDenomination(); SavedFeedbackText.Text = ""; }
        catch (InvalidOperationException ex) { MinimumBox.SelectedValue = 0.25m; SavedFeedbackText.Text = ex.Message; }
    }
    private void Minimum_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (MinimumBox.SelectedValue is not decimal minimum) return;
        AvailableText.Text = "Counting denominations: " + string.Join(" · ", ShiftService.Denominations.Where(d => d >= minimum).Select(d => $"₱{d:N2}"));
        SavedFeedbackText.Text = "Unsaved changes";
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("configure cash counting")) return;
        try
        {
            if (MinimumBox.SelectedValue is not decimal minimum) throw new InvalidOperationException("Select a minimum denomination.");
            new ShiftService(App.Db).ConfigureCountingMinimum(App.CurrentUser!.Id, minimum);
            SavedFeedbackText.Text = "";
            MyPos.Desktop.Dialogs.ActionDialog.Success("Cash counting settings saved. New counting forms use this minimum; saved counts are unchanged.", "Cash Counting Settings", Window.GetWindow(this));
        }
        catch (InvalidOperationException ex)
        { MyPos.Desktop.Dialogs.ActionDialog.Show(ex.Message, "Cash Counting Settings", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private sealed record DenominationChoice(decimal Value, string Label);
}
