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
using MyPos.Desktop.Printing;

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
    private string? _lastAutoAddress;
    private Sale? _saleAwaitingPrint;
    private bool _hasOpenShift;

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
        Loaded += (_, _) =>
        {
            UpdateInvoiceBoxState();
            UpdateShiftStatus();
            UpdateHeldCount();
            _clock.Start();
        };

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
        LoadCustomers();
        RefreshProducts();
        RefreshTotals();
        UpdateShiftStatus();
        SearchBox.Focus();
    }

    private void PosView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Keep the transaction fields and at least one product row visible on short desktops.
        SaleDetailsTitle.Visibility = e.NewSize.Height < 470
            ? Visibility.Collapsed
            : Visibility.Visible;
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
            InvoiceBox.ToolTip = "System receipts are enabled. This sale will receive " +
                                $"receipt R{branch.NextReceiptNumber:D6}. Turn off Print System Receipts " +
                                "in Settings to enter a manual OR number.";
        }
        else
        {
            InvoiceBox.Text = "";
            InvoiceBox.IsEnabled = true;
            InvoiceBox.ToolTip = "Enter OR number issued for this sale.";
        }
    }

    private void LoadCustomers()
    {
        var customers = App.Db.Customers.OrderBy(c => c.Name).ToList();
        _customerAddresses = customers.ToDictionary(c => c.Name, c => c.Address, StringComparer.OrdinalIgnoreCase);
        // Customer is intentionally a plain text field; Core still stores snapshots.
    }

    private void CustomerNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (AddressBox == null || _customerAddresses.Count == 0) return;
        if (!string.IsNullOrWhiteSpace(AddressBox.Text) && AddressBox.Text != _lastAutoAddress) return;
        var address = _customerAddresses.GetValueOrDefault(CustomerNameBox.Text.Trim());
        if (address == null) return;
        AddressBox.Text = address;
        _lastAutoAddress = address;
    }

    private void UpdateShiftStatus()
    {
        var open = new ShiftService(App.Db).GetOpenShift();
        _hasOpenShift = open != null;
        PayButton.IsEnabled = _hasOpenShift && _cart.Count > 0;
        ShiftPrompt.Visibility = open == null ? Visibility.Visible : Visibility.Collapsed;
        PayButton.ToolTip = open == null ? "Open a shift before taking payment."
            : _cart.Count == 0 ? "Add a product to the current sale."
            : null;
    }

    private void OpenShiftButton_Click(object sender, RoutedEventArgs e)
    {
        if (new ShiftService(App.Db).HasOpenShift)
        {
            UpdateShiftStatus();
            return;
        }

        var dialog = new ShiftOpenDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
        {
            SearchBox.Focus();
            return;
        }

        try
        {
            new ShiftService(App.Db).OpenShift(_branchId, App.CurrentUser!.Id, dialog.OpeningFloat);
            UpdateShiftStatus();
            Status("Shift opened. Ready to take payment.");
        }
        catch (InvalidOperationException ex)
        {
            UpdateShiftStatus();
            MessageBox.Show(ex.Message, "Shift not opened", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        SearchBox.Focus();
    }

    private void UpdateHeldCount()
    {
        var count = new HeldSaleService(App.Db).ListActive(_branchId).Count;
        RecallButton.Content = count == 0 ? "Recall" : $"Recall {Math.Min(count, 9)}{(count > 9 ? "+" : "")}";
        RecallButton.ToolTip = count == 0
            ? "Recall a held sale (F6)"
            : $"Recall a held sale (F6) · {count} waiting";
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

    private void EditSelectedQuantity()
    {
        if (!CartGrid.IsKeyboardFocusWithin || CartGrid.SelectedItem is not CartLineVM line)
        {
            Status("Select an item in Current sale first.");
            return;
        }

        var dialog = new QuantityEditDialog(line.Name, line.Qty, line.StockAvailable)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true)
        {
            line.Qty = dialog.Quantity;
            RefreshTotals();
            Status($"Quantity updated: {line.Name} × {line.Qty:0.##}");
        }
        CartGrid.Focus();
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
        _lastAutoAddress = null;
        DiscountErrorText.Text = "";
        DiscountKindBox.SelectedIndex = 0;
        PayModeBox.SelectedIndex = 0;
        TypeBox.SelectedIndex = 0;
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
            CustomerNameBox.Text,
            InvoiceBox.IsEnabled ? InvoiceBox.Text : null,
            null,
            AddressBox.Text,
            SelectedMethod,
            SelectedOrderType,
            IsSeniorSale ? DiscountKind.SeniorPwd : DiscountKindBox.SelectedIndex == 1 ? DiscountKind.Regular : DiscountKind.None,
            ParseDiscount(),
            IsSeniorSale ? DiscountBox.Text.Trim() : null);
        ResetSaleDraft();
        UpdateHeldCount();
        Status($"Held - {(string.IsNullOrWhiteSpace(held.CustomerName) ? "WALK-IN" : held.CustomerName)} ({count} item(s))");
    }

    private void RecallHeld()
    {
        if (_cart.Count > 0 && MessageBox.Show($"Recalling will discard the current {_cart.Count} item(s) in the cart. Continue?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var dialog = new HeldSalesDialog(_branchId);
        var recalled = dialog.ShowDialog() == true;
        UpdateHeldCount();
        if (!recalled || dialog.RecalledCart == null || dialog.SelectedDraft == null) return;
        var ids = dialog.RecalledCart.Select(line => line.ProductId).ToList();
        var products = App.Db.Products.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id);
        var restored = new List<CartLineVM>();
        var skipped = 0;
        var reduced = 0;
        foreach (var line in dialog.RecalledCart)
        {
            if (!products.TryGetValue(line.ProductId, out var product) || !product.IsActive) { skipped++; continue; }
            var vm = new CartLineVM(product) { Qty = Math.Min(line.Qty, product.StockQty) };
            if (vm.Qty <= 0) { skipped++; continue; }
            if (vm.Qty < line.Qty) reduced++;
            vm.PropertyChanged += (_, args) => { if (args.PropertyName is nameof(CartLineVM.Qty) or nameof(CartLineVM.LineTotal)) RefreshTotals(); };
            restored.Add(vm);
        }
        if (restored.Count == 0)
        {
            MessageBox.Show("No items from this held sale are currently available. The held sale is still saved.",
                "Recall unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (skipped > 0 || reduced > 0)
        {
            var warning = $"Current stock has changed: {skipped} unavailable item(s) will be skipped and " +
                          $"{reduced} item quantity/quantities will be reduced. Recall the available items?";
            if (MessageBox.Show(warning, "Review held sale", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
        }

        var draft = dialog.SelectedDraft;
        ResetSaleDraft();
        foreach (var line in restored) _cart.Add(line);
        CustomerNameBox.Text = draft.CustomerName;
        AddressBox.Text = draft.CustomerAddress ?? "";
        if (InvoiceBox.IsEnabled) InvoiceBox.Text = draft.InvoiceNumber ?? "";
        PayModeBox.SelectedIndex = Math.Max(0, (int)draft.PaymentMethod - 1);
        TypeBox.SelectedIndex = Math.Max(0, (int)draft.OrderType - 1);
        DiscountKindBox.SelectedIndex = (int)draft.DiscountKind;
        DiscountBox.Text = IsSeniorSale ? draft.SeniorIdNumber ?? ""
            : draft.DiscountAmount > 0 ? draft.DiscountAmount.ToString("0.##") : "";
        RefreshTotals();
        new HeldSaleService(App.Db).CompleteRecall(draft.Id, App.CurrentUser!.Id);
        UpdateHeldCount();
        Status($"Recalled. {skipped} unavailable item(s) skipped; {reduced} quantity/quantities reduced to current stock.");
        SearchBox.Focus();
    }

    // ---------- totals ----------

    private void RefreshTotals()
    {
        var lines = _cart.Select(l => new SaleCalculator.Line(l.Price, l.Qty, l.UnitCost, l.IsVatExempt)).ToList();
        var kind = IsSeniorSale ? DiscountKind.SeniorPwd : DiscountKind.None;
        var r = SaleCalculator.Compute(lines, ParseDiscount(), _vatRate, kind);

        GrossText.Text = $"₱{r.Gross:N2}";
        DiscountSummaryText.Text = $"−₱{r.Discount:N2}";
        DiscountBreakdown.Visibility = r.Discount > 0 ? Visibility.Visible : Visibility.Collapsed;
        NetText.Text = $"₱{r.Net:N2}";
        VatText.Text = $"₱{r.Vat:N2}";
        TotalText.Text = $"₱{r.Total:N2}";
        ItemCountText.Text = _cart.Sum(l => l.Qty).ToString("0.##");
        CartSummaryText.Text = $"{_cart.Count} {(_cart.Count == 1 ? "line" : "lines")} · {ItemCountText.Text} items";
        CartEmptyHint.Visibility = _cart.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PayButton.IsEnabled = _hasOpenShift && _cart.Count > 0;
        PayButton.ToolTip = !_hasOpenShift ? "Open a shift before taking payment."
            : _cart.Count == 0 ? "Add a product to the current sale."
            : null;
    }

    private decimal ParseDiscount()
        => DiscountKindBox.SelectedIndex == 1 &&
           decimal.TryParse(DiscountBox.Text.Trim(), out var d) && d > 0 ? d : 0;

    private void DiscountBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DiscountErrorText != null) DiscountErrorText.Text = "";
        if (DiscountKindBox != null && GrossText != null) RefreshTotals();
    }

    private void DiscountKindBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DiscountBox == null || DiscountDetailLabel == null || DiscountErrorText == null) return;
        DiscountBox.Text = "";
        DiscountBox.IsEnabled = DiscountKindBox.SelectedIndex is 1 or 2;
        DiscountBox.TextAlignment = DiscountKindBox.SelectedIndex == 1
            ? TextAlignment.Right : TextAlignment.Left;
        DiscountDetailLabel.Text = DiscountKindBox.SelectedIndex switch
        {
            1 => "DISCOUNT AMOUNT",
            2 => "SC/PWD ID *",
            _ => "DISCOUNT DETAIL"
        };
        DiscountBox.Tag = DiscountKindBox.SelectedIndex switch
        {
            1 => "Amount in pesos",
            2 => "Required Senior/PWD ID",
            _ => "Choose a discount type first"
        };
        DiscountErrorText.Text = "";
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
            : (Brush)FindResource("UiBorderBrush");
        InvoiceBox.BorderThickness = duplicate ? new Thickness(2) : new Thickness(1);
        InvoiceBox.ToolTip = duplicate
            ? $"Invoice '{clean}' was already used."
            : "Enter OR number issued for this sale.";
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
        ProductsEmptyHint.Visibility = _filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
        // A double-click on a header, scrollbar or empty space must not add the
        // previously selected product. Only an actual product row is actionable.
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(ProductsGrid, source) is DataGridRow { Item: Product p })
            AddAndReset(p);
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
            UpdateShiftStatus();
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
        if (kind == DiscountKind.SeniorPwd && string.IsNullOrWhiteSpace(DiscountBox.Text))
        {
            Status("Senior/PWD ID number is required");
            DiscountErrorText.Text = "Required for SC/PWD sale";
            DiscountBox.Focus();
            return;
        }
        if (DiscountKindBox.SelectedIndex == 1 && ParseDiscount() <= 0)
        {
            Status("Enter a valid discount amount");
            DiscountErrorText.Text = "Enter an amount greater than zero";
            DiscountBox.Focus();
            return;
        }
        DiscountErrorText.Text = "";

        var lines = _cart.Select(l => new SaleCalculator.Line(l.Price, l.Qty, l.UnitCost, l.IsVatExempt)).ToList();
        var discount = ParseDiscount();
        var calc = SaleCalculator.Compute(lines, discount, _vatRate, kind);

        if (discount > calc.Gross) { Status("Discount exceeds the subtotal"); return; }

        var method = SelectedMethod;
        var dlg = new PaymentDialog(calc.Total, method) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) { SearchBox.Focus(); return; }

        // Receipt type: system receipts print their own number; otherwise the
        // typed invoice number marks it Manual; blank = None
        ReceiptType receiptType;
        string? manualNo = null;
        if (ReceiptsEnabled) receiptType = ReceiptType.System;
        else if (!string.IsNullOrWhiteSpace(InvoiceBox.Text))
        { receiptType = ReceiptType.Manual; manualNo = InvoiceBox.Text.Trim(); }
        else receiptType = ReceiptType.None;

        Sale sale;
        try
        {
            sale = new SaleService(App.Db).PostSale(
                _branchId,
                App.CurrentUser!.Id,
                _cart.Select(l => new CartLine(l.ProductId, l.Qty)).ToList(),
                discount,
                dlg.Tendered,
                method,
                dlg.Reference,
                receiptType,
                manualNo,
                CustomerNameBox.Text,
                AddressBox.Text,
                SelectedOrderType,
                kind,
                IsSeniorSale ? DiscountBox.Text.Trim() : null);

        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Sale not completed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            SearchBox.Focus();
            return;
        }

        // The sale is committed. Clear the draft before any optional printer work
        // so retrying a receipt can never accidentally post the sale a second time.
        ResetSaleDraft();
        Status($"Sale #{sale.SaleNumber} saved" +
               (sale.ReceiptNumber != null ? $" - {sale.ReceiptNumber}" : ""));

        var printed = !ReceiptsEnabled || ReceiptPrinting.TryPrint(sale, reprint: false);
        var completion = new SaleCompleteDialog(sale, ReceiptsEnabled, printed);
        completion.ShowDialog();
        if (ReceiptsEnabled && !completion.ReceiptPrinted)
        {
            _saleAwaitingPrint = sale;
            PrintFailureText.Text = $"Sale #{sale.SaleNumber} was saved, but its receipt was not printed. Check the printer and retry.";
            PrintFailureBanner.Visibility = Visibility.Visible;
            Status($"Sale #{sale.SaleNumber} saved; receipt not printed.");
        }
        else if (_saleAwaitingPrint?.Id == sale.Id)
        {
            _saleAwaitingPrint = null;
            PrintFailureBanner.Visibility = Visibility.Collapsed;
        }
        SearchBox.Focus();
    }

    private void RetryReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        if (_saleAwaitingPrint == null) return;
        var preview = new ReceiptPreviewDialog(_saleAwaitingPrint, false,
            "Sale already saved. This action prints its receipt only; it will not charge the customer again.");
        preview.ShowDialog();
        if (!preview.PrintedSuccessfully) return;
        Status($"Receipt printed for sale #{_saleAwaitingPrint.SaleNumber}");
        _saleAwaitingPrint = null;
        PrintFailureBanner.Visibility = Visibility.Collapsed;
    }

    // ---------- hotkeys ----------

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1) { e.Handled = true; EditSelectedQuantity(); }
        else if (e.Key == Key.F2) { e.Handled = true; Pay(); }
        else if (e.Key == Key.F3) { e.Handled = true; HoldCart(); }
        else if (e.Key == Key.F4) { e.Handled = true; ClearCart(); }
        else if (e.Key == Key.F6) { e.Handled = true; RecallHeld(); }
        else if (e.Key == Key.Escape && SearchBox.Text.Length > 0)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
            e.Handled = true;
        }
        // Delete removes a selected cart row, never text in another input field.
        else if (e.Key == Key.Delete && CartGrid.IsKeyboardFocusWithin
                 && CartGrid.SelectedItem is CartLineVM line)
        {
            _cart.Remove(line);
            RefreshTotals();
            Status($"Removed: {line.Name}");
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
    public string ItemCode { get; }
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
        ProductId = p.Id; ItemCode = string.IsNullOrWhiteSpace(p.Barcode) ? "—" : p.Barcode;
        Name = p.Name; Price = p.Price; UnitCost = p.CostPrice;
        IsVatExempt = p.IsVatExempt; StockAvailable = p.StockQty;
        _qty = 1;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
