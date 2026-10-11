using System.Windows;
using MyPos.Core.Entities;
using MyPos.Core.Services;
namespace MyPos.Desktop.Dialogs;
public partial class CashMovementDialog : Window
{
    public CashMovementDialog()
    {
        InitializeComponent(); TypeBox.SelectedIndex = 0;
        CategoryBox.ItemsSource = Enum.GetValues<CashMovementCategory>(); CategoryBox.SelectedIndex = 0;
        ApproveNowBox.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Category_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CategoryBox.SelectedItem is CashMovementCategory category && category is CashMovementCategory.Expense or CashMovementCategory.BankDeposit or CashMovementCategory.Remittance or CashMovementCategory.OwnerWithdrawal)
            TypeBox.SelectedIndex = 1;
    }
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0 || string.IsNullOrWhiteSpace(ReasonBox.Text)) { ErrorText.Text = "Enter a positive amount and a reason."; return; }
        try { new ShiftService(App.Db).RecordCashMovement(App.CurrentUser!.Id, TypeBox.SelectedIndex == 0 ? CashMovementType.CashIn : CashMovementType.CashOut, amount, ReasonBox.Text,
            (CashMovementCategory)CategoryBox.SelectedItem, ReferenceBox.Text, DestinationBox.Text, ExpenseCategoryBox.Text, ApproveNowBox.IsChecked == true); DialogResult = true; } catch (Exception ex) { ErrorText.Text = ex.Message; }
    }
}
