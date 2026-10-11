using System.Windows;
using System.Windows.Controls;
using MyPos.Core.Entities;
using MyPos.Core.Services;
namespace MyPos.Desktop.Views;

public partial class CashDrawerSettingsView : UserControl
{
    public CashDrawerSettingsView()
    {
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators may configure drawers.");
        InitializeComponent();
        AssignedCashierBox.ItemsSource = App.Db.Users.Where(u => u.IsActive && u.Role == UserRole.Cashier)
            .OrderBy(u => u.FullName).ToList();
        if (Guid.TryParse(AppSettings.Get("DrawerAssignedCashierId"), out var id)) AssignedCashierBox.SelectedValue = id;
        DrawerModeBox.SelectedIndex = AppSettings.Get("DrawerMode") == "Assigned" ? 1 : 0;
        RefreshCurrentPolicy();
        SavedFeedbackText.Text = "";
        AssignedCashierBox.SelectionChanged += (_, _) => SavedFeedbackText.Text = "Unsaved changes";
    }

    private void RefreshCurrentPolicy()
    {
        var shift = new ShiftService(App.Db).GetOpenShift();
        var cashier = shift?.AssignedCashierId is Guid id ? App.Db.Users.FirstOrDefault(u => u.Id == id)?.FullName : null;
        CurrentPolicyText.Text = shift == null ? "No shift is currently open." :
            shift.DrawerMode == DrawerMode.Shared ? "Open shift: Shared — all cashiers." :
            $"Open shift: Assigned — {cashier ?? "unknown cashier"}.";
    }

    private void DrawerModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AssignedCashierPanel == null) return;
        AssignedCashierPanel.Visibility = DrawerModeBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        SavedFeedbackText.Text = "Unsaved changes";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("configure cash drawers")) return;
        try
        {
            new ShiftService(App.Db).ConfigureDrawer(App.CurrentUser!.Id,
                DrawerModeBox.SelectedIndex == 1 ? DrawerMode.Assigned : DrawerMode.Shared,
                AssignedCashierBox.SelectedValue as Guid?);
            RefreshCurrentPolicy();
            SavedFeedbackText.Text = "Drawer policy saved for the next shift. Any open shift is unchanged.";
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Drawer Access Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
