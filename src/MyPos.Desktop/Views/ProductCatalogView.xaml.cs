using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyPos.Core.Entities;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop.Views;

public partial class ProductCatalogView : UserControl
{
    public ProductCatalogView()
    {
        InitializeComponent();

        if (!Permissions.IsAdmin)
        {
            CostColumn.Visibility = Visibility.Collapsed;
            AddButton.Visibility = Visibility.Collapsed;
            EditButton.Visibility = Visibility.Collapsed;
            DeactivateButton.Visibility = Visibility.Collapsed;
        }

        LoadProducts();
        SearchBox.Focus();
    }

    private Product? Selected => ProductsGrid.SelectedItem as Product;

    private void LoadProducts()
    {
        var term = SearchBox.Text.Trim();
        var products = App.Db.Products.ToList();

        if (term.Length > 0)
        {
            products = products.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (p.Barcode ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (p.Category ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var list = products.OrderBy(p => p.Name).ToList();
        ProductsGrid.ItemsSource = list;

        CountText.Text = list.Count == 0
            ? "No products match."
            : $"{list.Count} product(s)";
    }

    private void ReselectAndFocus(Guid productId)
    {
        var row = ProductsGrid.ItemsSource.OfType<Product>()
            .FirstOrDefault(p => p.Id == productId);

        if (row != null)
        {
            ProductsGrid.SelectedItem = row;
            ProductsGrid.ScrollIntoView(row);
        }

        ProductsGrid.Focus();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => LoadProducts();

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) SearchBox.Text = "";
        else if (e.Key == Key.Enter
                 && ProductsGrid.ItemsSource is List<Product> { Count: 1 })
        {
            ProductsGrid.SelectedIndex = 0;
            ProductsGrid.Focus();
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("add products")) return;
        if (new ProductEditDialog(null).ShowDialog() == true) LoadProducts();
    }

    private void EditButton_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (!Permissions.RequireAdmin("edit products")) return;

        var product = Selected;
        if (product == null)
        {
            MessageBox.Show("Select a product first.", "MyPos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var saved = new ProductEditDialog(product).ShowDialog() == true;
        if (saved) LoadProducts();

        ReselectAndFocus(product.Id);
    }

    private void ReceiveStockButton_Click(object sender, RoutedEventArgs e)
    {
        var product = Selected;
        if (product == null)
        {
            MessageBox.Show("Select a product first.", "MyPos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (new ReceiveStockDialog(product).ShowDialog() == true) LoadProducts();

        ReselectAndFocus(product.Id);
    }

    private void DeactivateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("deactivate products")) return;

        var p = Selected;
        if (p == null)
        {
            MessageBox.Show("Select a product first.", "MyPos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Deactivate '{p.Name}'?\nIt disappears from the catalog, but sales history stays intact.",
            "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        p.IsActive = false;
        App.Db.AuditLogs.Add(new AuditLog
        {
            Date = DateTime.Now, UserId = App.CurrentUser?.Id,
            Action = "ProductDeactivate", EntityName = "Product", EntityId = p.Id, Details = p.Name
        });
        App.Db.SaveChanges();
        LoadProducts();
    }
}
