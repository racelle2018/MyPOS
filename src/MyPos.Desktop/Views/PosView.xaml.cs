using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using MyPos.Desktop.Controls;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop.Views;

public partial class PosView : UserControl
{
    private readonly ObservableCollection<CartLineVM> _cart = new();
    private readonly Guid _branchId;
    private readonly decimal _vatRate;
    private bool ReceiptsEnabled => AppSettings.Get("ReceiptIssuanceEnabled", "false") == "true";
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _sanitizingInvoice;
    private Dictionary<string, string> _customerAddresses = new(StringComparer.OrdinalIgnoreCase);
    private List<Product> _filtered = new();

    public bool HasItems => _cart.Count > 0;
    public int CartCount => _cart.Count;
    private bool IsSeniorSale => DiscountKindBox.SelectedIndex == 2;

    public PosView()
    {
        InitializeComponent();

        _branchId = App.Db.Branches.OrderBy(b => b.CreatedAt).First().Id;
        _vatRate = decimal.Parse(
            App.Db.Settings.First(s => s.Key == "VatRate").Value,
            System.Globalization.CultureInfo.InvariantCulture);
        VatLabel.Text = $"VAT ({_vatRate * 100:0.#}%)";
        UpdateInvoiceBoxState();
        Loaded += (_, _) => UpdateInvoiceBoxState();

        PayModeBox.ItemsSource = new[] { "Cash", "Card", "GCash", "Maya", "Bank" };
        PayModeBox.SelectedIndex = 0;
        TypeBox.ItemsSource = new[] { "Walk-in", "Pick-up", "Delivery" };
        TypeBox.SelectedIndex = 0;
        DiscountKindBox.SelectedIndex = 0;

        _clock.Tick += (_, _) => UpdateClock();
        UpdateClock();
        _clock.Start();
        Unloaded += (_, _) => _clock.Stop();

        CartGrid.ItemsSource = _cart;
        RefreshProducts();
        RefreshTotals();
        SearchBox.Focus();
    }

    // ---------- header (date / invoice / customer) ----------

    private void UpdateClock()
    {
        DateTimeText.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy · h:mm:ss tt");
    }

    private void UpdateInvoiceBoxState()
    {
        if (ReceiptsEnabled)
        {
            var branch = App.Db.Branches.First(b => b.Id == _branchId);
            InvoiceBox.Text = $"AUTO {branch.NextReceiptNumber:D6}";
            InvoiceBox.IsEnabled = false;
            InvoiceBox.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xF7));
            InvoiceBox.ToolTip = "System receipts are enabled. This sale will receive " +
                                $"receipt R{branch.NextReceiptNumber:D6}. Turn off Print System Receipts " +
                                "in Settings to enter a manual OR number.";
        }
        else
        {
            InvoiceBox.Text = "";
            InvoiceBox.IsEnabled = true;
            InvoiceBox.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF9, 0xC3));
            InvoiceBox.ToolTip = "Enter the manual receipt / OR number issued for this sale.";
        }
    }

    private void LoadCustomers()
    {
        var customers = App.Db.Customers.OrderBy(c => c.Name).ToList();
        _customerAddresses = customers.ToDictionary(c => c.Name, c => c.Address, StringComparer.OrdinalIgnoreCase);
        // Customer is intentionally a plain text field; Core still stores snapshots.
    }

    private void CustomerNameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var address = "";
        if (CustomerNameBox == null
            )
        {
            AddressBox.Text = address;   // pick a known customer → address fills itself
        }
    }

    private PaymentMethod SelectedMethod => (PayModeBox.SelectedItem as string) switch
    {
        "Card" => PaymentMethod.Card,
        "GCash" => PaymentMethod.Gcash,
        "Maya" => PaymentMethod.Maya,
        "Bank" => PaymentMethod.Bank,
        _ => PaymentMethod.Cash
    };

    private OrderType SelectedOrderType => (TypeBox.SelectedItem as string) switch
    {
        "Pick-up" => OrderType.PickUp,
        "Delivery" => OrderType.Delivery,
        _ => OrderType.WalkIn
    };

    // ---------- cart ----------

    private void AddToCart(Product p)
    {
        var line = _cart.FirstOrDefault(l => l.ProductId == p.Id);
        if (line == null)
        {
            if (p.StockQty <= 0) { Status($"{p.Name} — out of stock"); return; }
            var newLine = new CartLineVM(p);
            newLine.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(CartLineVM.Qty) or nameof(CartLineVM.LineTotal))
                    RefreshTotals();
            };
            _cart.Add(newLine);
        }
        else
        {
            if (line.Qty + 1 > line.StockAvailable)
            { Status($"{line.Name} — stock limit reached ({line.StockAvailable:0.##})"); return; }
            line.Qty += 1;
        }
        Status($"Added: {p.Name}");
        RefreshTotals();
    }

    private void QtyPlus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CartLineVM line })
        {
            if (line.Qty + 1 > line.StockAvailable)
            { Status($"{line.Name} — stock limit reached ({line.StockAvailable:0.##})"); return; }
            line.Qty += 1;
            RefreshTotals();
        }
    }

    private void QtyMinus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CartLineVM line })
        {
            if (line.Qty <= 1) _cart.Remove(line);
            else line.Qty -= 1;
            RefreshTotals();
        }
    }

    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CartLineVM line })
        {
            _cart.Remove(line);
            RefreshTotals();
        }
    }

    private void ClearCart()
    {
        if (_cart.Count == 0 && InvoiceBox.Text.Length == 0 && CustomerNameBox.Text.Length == 0) return;
        if (MessageBox.Show("Clear this sale (items, discount, invoice, customer)?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ResetSaleDraft();
        Status("Sale cleared");
    }

    private void ResetSaleDraft()
    {
        _cart.Clear();
        DiscountBox.Text = "";
        UpdateInvoiceBoxState();
        CustomerNameBox.Text = "";
        AddressBox.Text = "";
        SeniorIdBox.Text = "";
        DiscountKindBox.SelectedIndex = 0;
        UpdateClock();
        LoadCustomers();     // a new customer was just created — suggest it from now on
        RefreshProducts();   // stock display refresh
        RefreshTotals();
    }

    private void HoldButton_Click(object sender, RoutedEventArgs e) => HoldCart();
    private void RecallButton_Click(object sender, RoutedEventArgs e) => RecallHeld();

    private void HoldCart()
    {
        if (_cart.Count == 0) { Status("Nothing to hold - cart is empty"); return; }
        var count = _cart.Count;
        var held = new HeldSaleService(App.Db).Hold(_branchId, App.CurrentUser!.Id,
            _cart.Select(l => new HeldCartLine(l.ProductId, l.Name, null, l.Price, l.UnitCost, l.IsVatExempt, l.Qty, l.StockAvailable)).ToList(),
            CustomerNameBox.Text, InvoiceBox.Text, null);
        ResetSaleDraft();
        Status($"Held - {(string.IsNullOrWhiteSpace(held.CustomerName) ? "WALK-IN" : held.CustomerName)} ({count} item(s))");
    }

    private void RecallHeld()
    {
        if (_cart.Count > 0 && MessageBox.Show($"Recalling will discard the current {_cart.Count} item(s) in the cart. Continue?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var dialog = new HeldSalesDialog(_branchId);
        if (dialog.ShowDialog() != true || dialog.RecalledCart == null) return;
        ResetSaleDraft();
        var products = App.Db.Products.Where(p => dialog.RecalledCart.Select(l => l.ProductId).Contains(p.Id)).ToDictionary(p => p.Id);
        var skipped = 0;
        foreach (var line in dialog.RecalledCart)
        {
            if (!products.TryGetValue(line.ProductId, out var product) || !product.IsActive) { skipped++; continue; }
            var vm = new CartLineVM(product) { Qty = Math.Min(line.Qty, product.StockQty > 0 ? product.StockQty : 0) };
            if (vm.Qty <= 0) { skipped++; continue; }
            vm.PropertyChanged += (_, args) => { if (args.PropertyName is nameof(CartLineVM.Qty) or nameof(CartLineVM.LineTotal)) RefreshTotals(); };
            _cart.Add(vm);
        }
        CustomerNameBox.Text = dialog.RecalledCustomer ?? "";
        InvoiceBox.Text = dialog.RecalledInvoice ?? "";
        RefreshTotals();
        Status(skipped > 0 ? $"Recalled - {skipped} item(s) skipped (unavailable or out of stock)" : "Recalled");
        SearchBox.Focus();
    }

    // ---------- totals ----------

    private void RefreshTotals()
    {
        var lines = _cart.Select(l => new SaleCalculator.Line(l.Price, l.Qty, l.UnitCost, l.IsVatExempt)).ToList();
        var kind = IsSeniorSale ? DiscountKind.SeniorPwd : DiscountKind.None;
        var r = SaleCalculator.Compute(lines, ParseDiscount(), _vatRate, kind);

        GrossText.Text = $"₱{r.Gross:N2}";
        VatText.Text = $"₱{r.Vat:N2}";
        TotalText.Text = $"₱{r.Total:N2}";
        PayButton.IsEnabled = _cart.Count > 0;
    }

    private decimal ParseDiscount()
        => decimal.TryParse(DiscountBox.Text.Trim(), out var d) && d > 0 ? d : 0;

    private void DiscountBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshTotals();

    private void DiscountKindBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DiscountBox == null || SeniorIdBox == null) return;
        DiscountBox.IsEnabled = DiscountKindBox.SelectedIndex == 1;
        SeniorIdBox.IsEnabled = IsSeniorSale;
        SeniorIdBox.Background = IsSeniorSale ? new SolidColorBrush(Color.FromRgb(0xFE, 0xF9, 0xC3)) : Brushes.White;
        if (IsSeniorSale) DiscountBox.Text = "";
        RefreshTotals();
    }

    private void InvoiceBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_sanitizingInvoice || !InvoiceBox.IsEnabled) return;

        var clean = string.Concat(InvoiceBox.Text.Where(c => char.IsLetterOrDigit(c) || c == '-'));
        if (clean != InvoiceBox.Text)
        {
            _sanitizingInvoice = true;
            var caret = Math.Max(0, InvoiceBox.CaretIndex - (InvoiceBox.Text.Length - clean.Length));
            InvoiceBox.Text = clean;
            InvoiceBox.CaretIndex = Math.Min(caret, clean.Length);
            _sanitizingInvoice = false;
        }

        var duplicate = clean.Length > 0 && App.Db.Sales.ToList().Any(s =>
            s.BranchId == _branchId && s.ReceiptNumber == clean && !s.IsVoided);

        InvoiceBox.BorderBrush = duplicate
            ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
            : new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
        InvoiceBox.BorderThickness = duplicate ? new Thickness(2) : new Thickness(1);
        InvoiceBox.ToolTip = duplicate
            ? $"Invoice '{clean}' was already used. Check the next number in your booklet."
            : "Enter the manual receipt / OR number issued for this sale.";
    }

    private void DiscountBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Pay();
        }
    }

    // ---------- search & products ----------

    private void RefreshProducts()
    {
        var term = SearchBox.Text.Trim();
        var products = App.Db.Products.Where(p => p.IsActive).ToList();

        if (term.Length > 0)
        {
            products = products.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (p.Barcode ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        _filtered = products.OrderBy(p => p.Name).ToList();
        ProductsGrid.ItemsSource = _filtered;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshProducts();

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var term = SearchBox.Text.Trim();
        if (term.Length == 0) return;

        // Exact barcode → instant add (this is the scanner path).
        // Otherwise: exactly one match → add it; several → hand over to the list.
        var exact = _filtered.FirstOrDefault(p => p.Barcode == term)
                 ?? (_filtered.Count == 1 ? _filtered[0] : null);

        if (exact != null)
        {
            AddAndReset(exact);
        }
        else if (_filtered.Count > 0)
        {
            ProductsGrid.SelectedIndex = 0;
            ProductsGrid.Focus();
        }
        else
        {
            Status("No matching product");
        }
    }

    private void ProductsGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ProductsGrid.SelectedItem is Product p)
        {
            AddAndReset(p);
            e.Handled = true;
        }
    }

    private void ProductsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProductsGrid.SelectedItem is Product p)
        {
            AddAndReset(p);
        }
    }

    private void AddAndReset(Product product)
    {
        AddToCart(product);
        SearchBox.Text = "";
        DataGridBehaviors.SetKeepSelection(CartGrid, true);
        SearchBox.Focus();
        Dispatcher.BeginInvoke(() => DataGridBehaviors.SetKeepSelection(CartGrid, false));
    }

    // ---------- pay ----------

    private void Pay()
    {
        var shiftService = new ShiftService(App.Db);
        if (!shiftService.HasOpenShift)
        {
            Status("No open shift - open one before selling (Shift button)");
            return;
        }
        InvoiceBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
        InvoiceBox.BorderThickness = new Thickness(1);

        if (_cart.Count == 0) { Status("Cart is empty"); return; }

        if (!ReceiptsEnabled && string.IsNullOrWhiteSpace(InvoiceBox.Text))
        {
            Status("Invoice / OR number is required");
            InvoiceBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            InvoiceBox.BorderThickness = new Thickness(2);
            InvoiceBox.Focus();
            InvoiceBox.SelectAll();
            MessageBox.Show(
                "Invoice / OR No. is required before payment.\n\n" +
                "Enter the number of the manual receipt issued for this sale.",
                "Invoice Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var kind = IsSeniorSale ? DiscountKind.SeniorPwd : DiscountKind.None;
        if (kind == DiscountKind.SeniorPwd && string.IsNullOrWhiteSpace(SeniorIdBox.Text))
        {
            Status("Senior/PWD ID number is required");
            SeniorIdBox.Focus();
            return;
        }

        var lines = _cart.Select(l => new SaleCalculator.Line(l.Price, l.Qty, l.UnitCost, l.IsVatExempt)).ToList();
        var discount = ParseDiscount();
        var calc = SaleCalculator.Compute(lines, discount, _vatRate, kind);

        if (discount > calc.Gross) { Status("Discount exceeds the subtotal"); return; }

        var method = SelectedMethod;
        var dlg = new PaymentDialog(calc.Total, method);
        if (dlg.ShowDialog() != true) { SearchBox.Focus(); return; }

        // Receipt type: system receipts print their own number; otherwise the
        // typed invoice number marks it Manual; blank = None
        ReceiptType receiptType;
        string? manualNo = null;
        if (ReceiptsEnabled) receiptType = ReceiptType.System;
        else if (!string.IsNullOrWhiteSpace(InvoiceBox.Text))
        { receiptType = ReceiptType.Manual; manualNo = InvoiceBox.Text.Trim(); }
        else receiptType = ReceiptType.None;

        try
        {
            var sale = new SaleService(App.Db).PostSale(
                _branchId,
                App.CurrentUser!.Id,
                _cart.Select(l => new CartLine(l.ProductId, l.Qty)).ToList(),
                calc.Discount,
                dlg.Tendered,
                method,
                null,
                receiptType,
                manualNo,
                CustomerNameBox.Text,
                AddressBox.Text,
                SelectedOrderType,
                kind,
                IsSeniorSale ? SeniorIdBox.Text : null);

            new SaleCompleteDialog(sale.SaleNumber, sale.TotalAmount, sale.TenderedAmount, sale.ChangeAmount)
                .ShowDialog();

            ResetSaleDraft();
            Status($"Sale #{sale.SaleNumber} completed" +
                   (sale.ReceiptNumber != null ? $" — {sale.ReceiptNumber}" : ""));
        }
        catch (InvalidOperationException ex)
        {
            // Final guard — e.g. stock changed underneath us; the transaction rolled back cleanly
            MessageBox.Show(ex.Message, "Sale not completed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SearchBox.Focus();
        }
    }

    // ---------- hotkeys ----------

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2) { e.Handled = true; Pay(); }
        else if (e.Key == Key.F3) { e.Handled = true; HoldCart(); }
        else if (e.Key == Key.F4) { e.Handled = true; ClearCart(); }
        else if (e.Key == Key.F6) { e.Handled = true; RecallHeld(); }
        else if (e.Key == Key.Escape && SearchBox.Text.Length > 0)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
            e.Handled = true;
        }
        // Delete removes a cart row — but NOT while typing in the discount box
        else if (e.Key == Key.Delete && CartGrid.IsKeyboardFocusWithin
                 && CartGrid.SelectedItem is CartLineVM line)
        {
            _cart.Remove(line);
            RefreshTotals();
            e.Handled = true;
        }
    }

    private void PayButton_Click(object sender, RoutedEventArgs e) => Pay();
    private void ClearButton_Click(object sender, RoutedEventArgs e) => ClearCart();

    private void Status(string message) => StatusText.Text = message;
}

/// <summary>One row in the cart. Qty changes notify the UI in place (selection preserved).</summary>
public class CartLineVM : INotifyPropertyChanged
{
    public Guid ProductId { get; }
    public string Name { get; }
    public decimal Price { get; }
    public decimal UnitCost { get; }
    public bool IsVatExempt { get; }
    public decimal StockAvailable { get; }

    private decimal _qty;
    public decimal Qty
    {
        get => _qty;
        set
        {
            _qty = Math.Clamp(value, 0, StockAvailable);
            OnPropertyChanged();
            OnPropertyChanged(nameof(LineTotal));
        }
    }

    public decimal LineTotal => Math.Round(Price * Qty, 2, MidpointRounding.AwayFromZero);

    public CartLineVM(Product p)
    {
        ProductId = p.Id; Name = p.Name; Price = p.Price; UnitCost = p.CostPrice;
        IsVatExempt = p.IsVatExempt; StockAvailable = p.StockQty;
        _qty = 1;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
