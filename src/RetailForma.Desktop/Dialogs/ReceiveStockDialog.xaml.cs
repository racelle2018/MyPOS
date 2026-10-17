using System.Windows;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;

namespace RetailForma.Desktop.Dialogs;

public partial class ReceiveStockDialog : Window
{
    private readonly Product _product;

    public ReceiveStockDialog(Product product)
    {
        InitializeComponent();
        _product = product;

        ProductNameText.Text = $"{product.Name} — current stock: {product.StockQty:0.##} {product.Unit}";
        CostBox.Text = product.CostPrice.ToString("0.####");   // last cost as a sensible default
        var kinds = new Dictionary<StockReceiptKind, string> { [StockReceiptKind.PaidPurchase] = "Paid purchase" };
        if (Permissions.IsAdmin)
        {
            kinds.Add(StockReceiptKind.OpeningBalance, "Opening inventory");
            kinds.Add(StockReceiptKind.OwnerContribution, "Owner stock contribution");
        }
        ReceiptKindBox.ItemsSource = kinds;
        ReceiptKindBox.DisplayMemberPath = "Value";
        ReceiptKindBox.SelectedValuePath = "Key";
        ReceiptKindBox.SelectedValue = StockReceiptKind.PaidPurchase;
        ZeroCostBox.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        QtyBox.Focus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";

        if (!decimal.TryParse(QtyBox.Text.Trim(), out var qty) || qty <= 0)
        { ErrorText.Text = "Enter a quantity greater than zero."; ActionDialog.Warning("Enter a quantity greater than zero.", Title, this); return; }
        if (!decimal.TryParse(CostBox.Text.Trim(), out var cost) || cost < 0)
        { ErrorText.Text = "Enter a valid unit cost."; ActionDialog.Warning("Enter a valid unit cost.", Title, this); return; }

        // One call = movement + weighted-average update + balanced journal entry, all atomic
        try { App.Service.Sales.ReceiveStock(
            _product.Id, qty, cost, App.CurrentUser!.Id, NotesBox.Text.Trim(),
            receiptKind: (StockReceiptKind)ReceiptKindBox.SelectedValue, zeroCostConfirmed: ZeroCostBox.IsChecked == true); }
        catch (InvalidOperationException ex) { ActionDialog.Warning(ex.Message, Title, this); return; }
        ActionDialog.Success("Stock received.", Title, this);

        DialogResult = true;
    }
}
