using System.Windows;
namespace MyPos.Desktop.Dialogs;
public partial class ShiftOpenDialog : Window
{
    public decimal OpeningFloat { get; private set; }

    public ShiftOpenDialog()
    {
        InitializeComponent();
        var assigned = AppSettings.Get("DrawerMode") == "Assigned";
        var name = Guid.TryParse(AppSettings.Get("DrawerAssignedCashierId"), out var id)
            ? App.Db.Users.FirstOrDefault(u => u.Id == id && u.IsActive)?.FullName : null;
        DrawerPolicyText.Text = assigned ? $"Assigned — {name ?? "administrator must select an active cashier"}" : "Shared — all cashiers";
        FloatBox.Focus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(FloatBox.Text, out var value) || value < 0)
        { ErrorText.Text = "Enter a valid amount."; ActionDialog.Warning("Enter a valid amount.", Title, this); return; }
        OpeningFloat = value;
        DialogResult = true;
    }
}
