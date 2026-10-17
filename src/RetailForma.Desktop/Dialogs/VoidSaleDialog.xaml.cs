using System.Windows;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;

namespace RetailForma.Desktop.Dialogs;

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
        if (!Permissions.RequireAdmin("approve a sale void")) return;
        ErrorText.Text = "";
        if (ConfirmBox.IsChecked != true)
        {
            ErrorText.Text = "Tick the confirmation box to proceed."; ActionDialog.Warning("Tick the confirmation box to proceed.", Title, this);
            return;
        }

        var reason = ReasonBox.Text.Trim();
        if (reason.Length < 5)
        {
            ErrorText.Text = "Enter a reason (at least a few words)."; ActionDialog.Warning("Enter a reason (at least a few words).", Title, this);
            return;
        }

        try
        {
            App.Service.Sales.VoidSale(_sale.Id, App.CurrentUser!.Id, reason);
            ActionDialog.Success("Sale voided. Reports and stock reflect the approved void.", Title, this);
            DialogResult = true;
        }
        catch (InvalidOperationException ex)
        {
            ErrorText.Text = ex.Message; ActionDialog.Warning(ex.Message, Title, this);
        }
    }
}
