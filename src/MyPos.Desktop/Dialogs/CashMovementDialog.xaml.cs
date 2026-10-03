using System.Windows;
using MyPos.Core.Entities;
using MyPos.Core.Services;
namespace MyPos.Desktop.Dialogs;
public partial class CashMovementDialog : Window
{
    public CashMovementDialog() { InitializeComponent(); TypeBox.SelectedIndex = 0; }
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0 || string.IsNullOrWhiteSpace(ReasonBox.Text)) { ErrorText.Text = "Enter a positive amount and a reason."; return; }
        try { new ShiftService(App.Db).RecordCashMovement(App.CurrentUser!.Id, TypeBox.SelectedIndex == 0 ? CashMovementType.CashIn : CashMovementType.CashOut, amount, ReasonBox.Text); DialogResult = true; } catch (Exception ex) { ErrorText.Text = ex.Message; }
    }
}
