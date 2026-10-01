using System.Windows;
using MyPos.Core.Entities;
using MyPos.Core.Services;

namespace MyPos.Desktop.Dialogs;

public partial class ReceiveStockDialog : Window
{
    private readonly Product _product;

    public ReceiveStockDialog(Product product)
    {
        InitializeComponent();
        _product = product;

        ProductNameText.Text = $"{product.Name} — current stock: {product.StockQty:0.##} {product.Unit}";
        CostBox.Text = product.CostPrice.ToString("0.####");   // last cost as a sensible default
        QtyBox.Focus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";

        if (!decimal.TryParse(QtyBox.Text.Trim(), out var qty) || qty <= 0)
        { ErrorText.Text = "Enter a quantity greater than zero."; return; }
        if (!decimal.TryParse(CostBox.Text.Trim(), out var cost) || cost < 0)
        { ErrorText.Text = "Enter a valid unit cost."; return; }

        // One call = movement + weighted-average update + balanced journal entry, all atomic
        new SaleService(App.Db).ReceiveStock(
            _product.Id, qty, cost, App.CurrentUser!.Id, NotesBox.Text.Trim());

        DialogResult = true;
    }
}