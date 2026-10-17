using System.Windows;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;
namespace RetailForma.Desktop.Dialogs;
public partial class StockAdjustmentDialog : Window
{
    private readonly Guid productId;
    public StockAdjustmentDialog(Product product)
    {
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Stock adjustments require Owner approval.");
        InitializeComponent(); productId = product.Id;
        ProductText.Text = $"{product.Name}\nStock: {product.StockQty:0.####}; carrying unit cost: ₱{product.CostPrice:0.####}";
        CounterpartBox.ItemsSource = new Dictionary<string, string> { ["3990"] = "Opening clearing correction — reconcile", ["3000"] = "Proprietor stock contribution" };
        QuantityBox.Focus();
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("approve stock adjustments")) return;
        if (!decimal.TryParse(QuantityBox.Text, out var delta) || delta == 0)
        { ActionDialog.Warning("Enter a nonzero signed quantity change.", Title, this); return; }
        try
        {
            App.Service.Sales.AdjustStock(productId, delta, App.CurrentUser!.Id, ReasonBox.Text,
                CounterpartBox.SelectedValue as string, ZeroCostBox.IsChecked == true);
            ActionDialog.Success("Stock adjustment and carrying-value posting saved.", Title, this); DialogResult = true;
        }
        catch (Exception ex) { ActionDialog.Warning(ex.Message, Title, this); }
    }
}
