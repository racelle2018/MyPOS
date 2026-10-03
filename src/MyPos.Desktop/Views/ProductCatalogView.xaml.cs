using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    private Product? Selected => (ProductsGrid.SelectedItem as ProductRowVM)?.Product;

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

        var list = products.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ToList();
        var rows = list.Select(p => new ProductRowVM(p)).ToList();
        ProductsGrid.ItemsSource = rows;

        CountText.Text = rows.Count == 0
            ? "No products match."
            : $"{rows.Count} product(s)";
        EmptyHint.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (rows.Count == 0)
        {
            EmptyHint.Text = term.Length > 0
                ? "No products match your search"
                : "No products yet — add your first product";
        }
        DeactivateButton.Content = Selected != null && !Selected.IsActive ? "Activate" : "Deactivate";
    }

    private void ReselectAndFocus(Guid productId)
    {
        var row = ProductsGrid.ItemsSource.OfType<ProductRowVM>()
            .FirstOrDefault(p => p.Product.Id == productId);

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
                 && ProductsGrid.ItemsSource is List<ProductRowVM> { Count: 1 })
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
        if (!Permissions.RequireAdmin("change products")) return;

        var p = Selected;
        if (p == null)
        {
            MessageBox.Show("Select a product first.", "MyPos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(p.IsActive
            ? $"Deactivate '{p.Name}'?\nIt disappears from the catalog, but sales history stays intact."
            : $"Reactivate '{p.Name}'?\nIt returns to the catalog and POS immediately.",
            "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        var activating = !p.IsActive;
        p.IsActive = activating;
        App.Db.AuditLogs.Add(new AuditLog
        {
            Date = DateTime.Now, UserId = App.CurrentUser?.Id,
            Action = activating ? "ProductActivate" : "ProductDeactivate",
            EntityName = "Product", EntityId = p.Id, Details = p.Name
        });
        App.Db.SaveChanges();
        LoadProducts();
    }

    private void ProductsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => DeactivateButton.Content = Selected != null && !Selected.IsActive ? "Activate" : "Deactivate";
}

public sealed class ProductRowVM
{
    public Product Product { get; }
    public ProductRowVM(Product product) => Product = product;
    public string Name => Product.Name;
    public string? Barcode => Product.Barcode;
    public string Category => Product.Category;
    public string Unit => Product.Unit;
    public decimal CostPrice => Product.CostPrice;
    public decimal Price => Product.Price;
    public decimal StockQty => Product.StockQty;
    public string ActiveText => Product.IsActive ? "ACTIVE" : "OFF";
    public Brush ActiveBrush => Product.IsActive ? Brushes.ForestGreen : Brushes.SlateGray;
}
