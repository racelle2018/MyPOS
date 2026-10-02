using System.Windows;
using MyPos.Core.Entities;
using MyPos.Core.Services;

namespace MyPos.Desktop.Dialogs;

public partial class VoidSaleDialog : Window
{
    private readonly Sale _sale;

    public VoidSaleDialog(Sale sale)
    {
        InitializeComponent();
        _sale = sale;
        SaleInfoText.Text = $"Sale #{sale.SaleNumber} — {sale.SaleDate:ddd, MMM d, yyyy h:mm tt}\n" +
                            $"Total: ₱{sale.TotalAmount:N2}" +
                            (string.IsNullOrWhiteSpace(sale.CustomerName) ? "" : $"\nCustomer: {sale.CustomerName}");
    }

    private void VoidButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (ConfirmBox.IsChecked != true)
        {
            ErrorText.Text = "Tick the confirmation box to proceed.";
            return;
        }

        var reason = ReasonBox.Text.Trim();
        if (reason.Length < 5)
        {
            ErrorText.Text = "Enter a reason (at least a few words).";
            return;
        }

        try
        {
            new SaleService(App.Db).VoidSale(_sale.Id, App.CurrentUser!.Id, reason.ToUpperInvariant());
            DialogResult = true;
        }
        catch (InvalidOperationException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }
}
