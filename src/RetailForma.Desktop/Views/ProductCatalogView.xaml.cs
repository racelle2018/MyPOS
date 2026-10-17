using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Entities;
using RetailForma.Desktop.Dialogs;

namespace RetailForma.Desktop.Views;

public partial class ProductCatalogView : UserControl
{
    private readonly DispatcherTimer _productRefreshTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private List<Product> _lastProducts = new();

    public ProductCatalogView()
    {
        InitializeComponent();

        if (!Permissions.IsAdmin)
        {
            CostColumn.Visibility = Visibility.Collapsed;
            AddButton.Visibility = Visibility.Collapsed;
            AdjustStockButton.Visibility = Visibility.Collapsed;
            EditButton.Visibility = Visibility.Collapsed;
            DeactivateButton.Visibility = Visibility.Collapsed;
        }

        LoadProducts(selectFirstIfNone: true);
        _productRefreshTimer.Tick += (_, _) => LoadProducts(onlyIfChanged: true);
        Loaded += (_, _) =>
        {
            LoadProducts();
            _productRefreshTimer.Start();
            // The constructor runs before ScreenHost attaches this view. Defer
            // keyboard focus until the visible layout has finished loading.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (IsLoaded && IsVisible)
                    Keyboard.Focus(SearchBox);
            });
        };
        Unloaded += (_, _) => _productRefreshTimer.Stop();
    }

    private Product? Selected => (ProductsGrid.SelectedItem as ProductRowVM)?.Product;

    private void LoadProducts(bool onlyIfChanged = false, bool selectFirstIfNone = false)
    {
        var products = App.Service.Products().AsQueryable().OrderBy(p => p.Id).ToList();
        if (onlyIfChanged && ProductCatalogSnapshot.Same(_lastProducts, products)) return;
        _lastProducts = products;

        var term = SearchBox.Text.Trim();
        var selectedId = Selected?.Id;

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
        if (selectedId.HasValue)
            ProductsGrid.SelectedItem = rows.FirstOrDefault(r => r.Product.Id == selectedId);
        else if (selectFirstIfNone && rows.Count > 0)
            ProductsGrid.SelectedItem = rows[0];

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
        UpdateSelectionActions();
    }

    private void ToolbarGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Keep the search and actions together on desktop; stack the actions
        // below the search before either side becomes cramped.
        var stacked = e.NewSize.Width < 800;
        Grid.SetRow(ActionsPanel, stacked ? 1 : 0);
        Grid.SetColumn(ActionsPanel, stacked ? 0 : 1);
        Grid.SetColumnSpan(ActionsPanel, stacked ? 2 : 1);
        ActionsPanel.Margin = stacked ? new Thickness(0, 8, 0, 0) : new Thickness(0);
        SearchPanel.Margin = stacked ? new Thickness(0) : new Thickness(0, 0, 10, 0);
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

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadProducts();

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
        if (new ProductEditDialog(null).ShowDialog() == true)
            LoadProducts(selectFirstIfNone: true);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void ProductsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // A cashier's ordinary double-click is selection, not a denied admin action.
        if (!Permissions.IsAdmin || e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(ProductsGrid, source) is not DataGridRow { Item: ProductRowVM } row)
            return; // Headers, scrollbars and empty space must never edit the previous selection.

        ProductsGrid.SelectedItem = row.Item;
        e.Handled = true;
        EditSelected(); // Retains the central role check used by the Edit button.
    }

    private void EditSelected()
    {
        if (!Permissions.RequireAdmin("edit products")) return;

        var product = Selected;
        if (product == null)
        {
            RetailForma.Desktop.Dialogs.ActionDialog.Show("Select a product first.", "RetailForma",
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
            RetailForma.Desktop.Dialogs.ActionDialog.Show("Select a product first.", "RetailForma",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (new ReceiveStockDialog(product).ShowDialog() == true) LoadProducts();

        ReselectAndFocus(product.Id);
    }

    private void AdjustStockButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("approve stock adjustments") || Selected == null) return;
        if (new StockAdjustmentDialog(Selected) { Owner = Window.GetWindow(this) }.ShowDialog() == true) LoadProducts();
    }

    private void DeactivateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("change products")) return;

        var selected = Selected;
        if (selected == null)
        {
            RetailForma.Desktop.Dialogs.ActionDialog.Show("Select a product first.", "RetailForma",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var p = App.Service.Products().First(product => product.Id == selected.Id);

        var confirm = RetailForma.Desktop.Dialogs.ActionDialog.Show(p.IsActive
            ? $"Deactivate '{p.Name}'?\nIt disappears from the catalog, but sales history stays intact."
            : $"Reactivate '{p.Name}'?\nIt returns to the catalog and POS immediately.",
            "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        var activating = !p.IsActive;
        App.Service.ActivateProduct(p.Id, activating);
        LoadProducts();
        ActionDialog.Success(activating ? "Product activated." : "Product deactivated.", "Products", Window.GetWindow(this));
    }

    private void ProductsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateSelectionActions();

    private void UpdateSelectionActions()
    {
        var selected = Selected;
        ReceiveStockButton.IsEnabled = selected != null;
        AdjustStockButton.IsEnabled = selected != null && Permissions.IsAdmin;
        EditButton.IsEnabled = selected != null;
        DeactivateButton.IsEnabled = selected != null;
        DeactivateButton.Content = selected is { IsActive: false } ? "Activate" : "Deactivate";
    }
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
    public string StockLevelText => StockQty <= 0 ? "OUT" :
        StockQty <= CurrentSettings.LowStockThreshold ? "LOW" : "OK";
    public string ActiveText => Product.IsActive ? "ACTIVE" : "DISABLED";
    public Brush ActiveBrush => Product.IsActive ? Brushes.ForestGreen : Brushes.SlateGray;
}
