using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Documents;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;
using RetailForma.Desktop.Dialogs;

namespace RetailForma.Desktop.Views;

public partial class DailySalesView : UserControl
{
    private readonly Guid _branchId;
    private DailySalesReport? _report;
    private bool _compactSalesHeader;
    private bool _loadingFilters = true;
    private string _filterScope = "All cashiers | All payments | Search: None";
    private string _printFilterScope = "";
    private bool _hasPrintFilters;

    public DailySalesView()
    {
        InitializeComponent();
        _branchId = App.Service.Branches().AsQueryable().OrderBy(b => b.CreatedAt).First().Id;
        CashierFilterBox.ItemsSource = new[] { new FilterChoice(null, "All cashiers") }.Concat(App.Service.Staff().OrderBy(u => u.FullName).Select(u => new FilterChoice(u.Id, u.FullName)).ToList()).ToList();
        PaymentFilterBox.ItemsSource = new[] { new FilterChoice(null, "All payments") }.Concat(Enum.GetValues<PaymentMethod>().Select(m => new FilterChoice((int)m, m.ToString()))).ToList();
        CashierFilterBox.SelectedIndex = PaymentFilterBox.SelectedIndex = 0;
        _loadingFilters = false;
        VoidButton.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        DatePick.DisplayDateEnd = RetailForma.Core.BusinessClock.Today;
        DatePick.SelectedDate = RetailForma.Core.BusinessClock.Today;
        Loaded += (_, _) =>
        {
            DatePick.DisplayDateEnd = RetailForma.Core.BusinessClock.Today;
            UpdateDateNavigation();
        };
    }

    private void LoadDate(DateTime date)
    {
        var cashier = (CashierFilterBox.SelectedItem as FilterChoice)?.Value as Guid?;
        var payment = (PaymentFilterBox.SelectedItem as FilterChoice)?.Value is int method ? (PaymentMethod?)method : null;
        var scope = new ReportScope(_branchId, App.CurrentUser?.Id ?? Guid.Empty, DateOnly.FromDateTime(date), DateOnly.FromDateTime(date), cashier, payment, SalesSearchBox.Text.Trim());
        try
        {
            var service = App.Service; service.Validate(scope);
            _filterScope = service.Describe(scope);
            var printScope = new List<string>();
            if (App.Service.Branches().AsQueryable().Count() > 1)
                printScope.Add("Branch: " + App.Service.Branches().AsQueryable().First(b => b.Id == _branchId).Name);
            if (cashier.HasValue)
                printScope.Add("Cashier: " + (CashierFilterBox.SelectedItem as FilterChoice)!.Label);
            if (payment.HasValue)
                printScope.Add("Payment: " + (PaymentFilterBox.SelectedItem as FilterChoice)!.Label);
            if (scope.Search.Length > 0) printScope.Add("Search: " + scope.Search);
            _printFilterScope = string.Join(" | ", printScope);
            _hasPrintFilters = printScope.Count > 0;
            var result = App.Service.DailySales(date, _branchId, cashier, payment, SalesSearchBox.Text);
            _report = new DailySalesReport { Date = result.Date, Sales = result.Sales, ScopeDescription = _filterScope };
        }
        catch (InvalidOperationException ex)
        {
            _report = null; SalesGrid.ItemsSource = null; TableStatusText.Text = ex.Message;
            UpdateActions(); return;
        }
        SalesGrid.ItemsSource = _report.Sales.Select(s => new SaleRowVM(s)).ToList();
        var paymentTotals = string.Join(" · ", Enum.GetValues<PaymentMethod>().Select(m => $"{m}: ₱{_report.ActiveSales.SelectMany(s => s.Payments).Where(p => p.Method == m).Sum(p => p.Amount):N2}"));
        TableStatusText.Text = $"{_report.Sales.Count} sale(s) · {_filterScope} · {paymentTotals}";
        ReportEmptyHint.Visibility = _report.Sales.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateSummary();
        UpdateActions();
    }

    private sealed record FilterChoice(object? Value, string Label);
    private void Filters_Changed(object sender, SelectionChangedEventArgs e) { if (!_loadingFilters) LoadDate(DatePick.SelectedDate ?? RetailForma.Core.BusinessClock.Today); }
    private void Search_Changed(object sender, TextChangedEventArgs e) { if (!_loadingFilters) LoadDate(DatePick.SelectedDate ?? RetailForma.Core.BusinessClock.Today); }

    private void SalesHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 900;
        if (compact == _compactSalesHeader) return;

        _compactSalesHeader = compact;
        Grid.SetRow(SalesActions, compact ? 1 : 0);
        Grid.SetColumn(SalesActions, compact ? 0 : 1);
        Grid.SetColumnSpan(SalesActions, compact ? 2 : 1);
        SalesActions.HorizontalAlignment = compact
            ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }

    private void UpdateSummary()
    {
        if (_report == null) return;
        TotalSalesText.Text = $"₱{_report.TotalSales:N2}";
        NetSalesText.Text = $"₱{_report.NetSales:N2}";
        VatText.Text = $"₱{_report.Vat:N2}";
        CountText.Text = _report.TransactionCount.ToString();
        AvgText.Text = $"₱{_report.AverageSale:N2}";
        VoidsText.Text = _report.VoidCount == 0
            ? "0" : $"{_report.VoidCount} (₱{_report.VoidTotal:N2})";
        SystemReceiptsText.Text = _report.SystemReceipts.ToString();
        ManualReceiptsText.Text = _report.ManualReceipts.ToString();
        MissingReceiptsText.Text = _report.MissingReceipts.ToString();
        MissingReceiptsText.Foreground = _report.MissingReceipts > 0
            ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
            : new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
        MissingHint.Visibility = _report.MissingReceipts > 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SalesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateActions();

    private void UpdateActions()
    {
        var hasSales = _report?.Sales.Count > 0;
        ExportButton.IsEnabled = hasSales;
        PrintButton.IsEnabled = hasSales;
        ReprintReceiptButton.IsEnabled =
            SalesGrid.SelectedItem is SaleRowVM row && CanReprintReceipt(row.Sale);
        VoidButton.IsEnabled = Permissions.IsAdmin &&
            SalesGrid.SelectedItem is SaleRowVM { Sale.IsVoided: false };
    }

    private void ReprintReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        if (SalesGrid.SelectedItem is not SaleRowVM row || !CanReprintReceipt(row.Sale))
            return;

        var preview = new ReceiptPreviewDialog(row.Sale, reprint: true,
            warning: "This is a copy of an existing system receipt. It will not create a new sale or payment.")
        { Owner = Window.GetWindow(this) };
        preview.ShowDialog();
        if (!preview.PrintedSuccessfully) return;

        App.Service.ClientEvent("ReceiptReprint", $"Sale #{row.Sale.SaleNumber} - {row.Sale.ReceiptNumber}", row.Sale.Id);
    }

    private static bool CanReprintReceipt(Sale sale) =>
        !sale.IsVoided && sale.ReceiptType == ReceiptType.System &&
        !string.IsNullOrWhiteSpace(sale.ReceiptNumber);

    private void TodayButton_Click(object sender, RoutedEventArgs e)
    {
        DatePick.DisplayDateEnd = RetailForma.Core.BusinessClock.Today;
        if (DatePick.SelectedDate?.Date == RetailForma.Core.BusinessClock.Today)
            LoadDate(RetailForma.Core.BusinessClock.Today); // Also acts as a refresh for today's sales.
        else
            DatePick.SelectedDate = RetailForma.Core.BusinessClock.Today;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
        => LoadDate(DatePick.SelectedDate ?? RetailForma.Core.BusinessClock.Today);

    private void PrevDayButton_Click(object sender, RoutedEventArgs e)
    {
        var date = (DatePick.SelectedDate ?? RetailForma.Core.BusinessClock.Today).Date;
        if (date > DateTime.MinValue.Date)
            DatePick.SelectedDate = date.AddDays(-1);
    }

    private void NextDayButton_Click(object sender, RoutedEventArgs e)
    {
        var date = (DatePick.SelectedDate ?? RetailForma.Core.BusinessClock.Today).Date;
        if (date < RetailForma.Core.BusinessClock.Today)
            DatePick.SelectedDate = date.AddDays(1);
    }

    private void DatePick_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NextDayButton == null) return;
        DatePick.DisplayDateEnd = RetailForma.Core.BusinessClock.Today;
        if (DatePick.SelectedDate?.Date > RetailForma.Core.BusinessClock.Today)
        {
            DatePick.SelectedDate = RetailForma.Core.BusinessClock.Today;
            return;
        }
        UpdateDateNavigation();
        if (DatePick.SelectedDate is DateTime date)
        {
            LoadDate(date);
        }
    }

    private void UpdateDateNavigation()
    {
        var date = DatePick.SelectedDate?.Date ?? RetailForma.Core.BusinessClock.Today;
        PrevDayButton.IsEnabled = date > DateTime.MinValue.Date;
        NextDayButton.IsEnabled = date < RetailForma.Core.BusinessClock.Today;
    }

    private void VoidButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("void sales")) return;
        if (SalesGrid.SelectedItem is not SaleRowVM row)
        {
            RetailForma.Desktop.Dialogs.ActionDialog.Show("Select a sale in the table first.", "RetailForma",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (row.Sale.IsVoided)
        {
            RetailForma.Desktop.Dialogs.ActionDialog.Show("This sale is already voided.", "RetailForma",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (new VoidSaleDialog(row.Sale).ShowDialog() == true)
            LoadDate(DatePick.SelectedDate ?? RetailForma.Core.BusinessClock.Today);
    }

    private void SalesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SalesGrid.SelectedItem is SaleRowVM row)
            new SaleDetailDialog(row.Sale).ShowDialog();
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        LoadDate(DatePick.SelectedDate ?? RetailForma.Core.BusinessClock.Today);
        if (_report == null || _report.Sales.Count == 0)
        {
            RetailForma.Desktop.Dialogs.ActionDialog.Show("No sales to export for this date.", "RetailForma");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"DAILY-SALES-{_report.Date:yyyy-MM-dd}.csv"
        };
        if (dialog.ShowDialog() != true) return;

        var branch = App.Service.Branches().AsQueryable().First(b => b.Id == _branchId).Name;
        try
        {
            File.WriteAllText(dialog.FileName, ReportCsv.DailySales(_report, branch), Encoding.UTF8);
            RetailForma.Desktop.Dialogs.ActionDialog.Success($"Exported {_report.Sales.Count} rows to:\n{dialog.FileName}", "Export Complete", Window.GetWindow(this));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { RetailForma.Desktop.Dialogs.ActionDialog.Error(ex.Message, "Export CSV", Window.GetWindow(this)); }
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        LoadDate(DatePick.SelectedDate ?? RetailForma.Core.BusinessClock.Today);
        if (_report == null || _report.Sales.Count == 0)
        {
            RetailForma.Desktop.Dialogs.ActionDialog.Show("No sales to print for this date.", "RetailForma",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preview = new ReportPrintPreviewWindow(BuildReportDocument(), "Daily Sales",
            $"Daily-Sales-{_report.Date:yyyy-MM-dd}.pdf") { Owner = Window.GetWindow(this) };
        preview.ShowDialog();
    }

    private FlowDocument BuildReportDocument()
    {
        var report = _report!;
        var border = Brushes.Black;
        var muted = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
        var document = new FlowDocument
        {
            PageWidth = 794,
            PageHeight = 1123,
            PagePadding = new Thickness(40, 30, 40, 30),
            FontFamily = (System.Windows.Media.FontFamily)FindResource("PrintInterFontFamily"),
            FontSize = 12,
            ColumnWidth = double.PositiveInfinity
        };

        ReportDocumentChrome.AddHeader(document, "DAILY SALES REPORT", $"{report.Date:dddd, MMMM d, yyyy}");
        if (_hasPrintFilters)
            document.Blocks.Add(new Paragraph(new Run("Filters: " + _printFilterScope)) { FontSize = 9, Margin = new Thickness(0, 0, 0, 5) });

        TableCell SummaryCell(string text, bool amount = false, bool emphasize = false, bool indent = false)
        {
            var cell = new TableCell
            {
                Padding = new Thickness(amount || indent ? 8 : 0, 3, amount ? 3 : 8, 3),
                BorderThickness = new Thickness(0)
            };
            cell.Blocks.Add(new Paragraph(new Run(text))
            {
                FontSize = 10, FontWeight = emphasize ? FontWeights.SemiBold : FontWeights.Normal,
                TextAlignment = amount ? TextAlignment.Right : TextAlignment.Left,
                Margin = new Thickness(0), KeepTogether = true
            });
            return cell;
        }

        var summary = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14) };
        foreach (var width in new[] { 190d, 290, 40, 190, 290 })
            summary.Columns.Add(new TableColumn { Width = new GridLength(width) });
        var summaryRows = new TableRowGroup();
        summary.RowGroups.Add(summaryRows);
        TableCell SummaryHeading(string title)
        {
            var cell = SummaryCell(title, emphasize: true);
            cell.ColumnSpan = 2;
            cell.BorderBrush = border;
            cell.BorderThickness = new Thickness(1);
            cell.Padding = new Thickness(3, 3, 0, 3);
            return cell;
        }
        summaryRows.Rows.Add(new TableRow { Cells =
        {
            SummaryHeading("Sales summary"), SummaryCell(""),
            SummaryHeading("Invoice reconciliation")
        }});
        void SummaryRow(string label, string value, string check, string count, bool primary = false, bool last = false)
        {
            var row = new TableRow { Cells =
            {
                SummaryCell(label, indent: true), SummaryCell(value, true, primary), SummaryCell(""),
                SummaryCell(check, indent: true), SummaryCell(count, true)
            }};
            foreach (var index in new[] { 0, 1, 3, 4 })
            {
                row.Cells[index].BorderBrush = border;
                row.Cells[index].BorderThickness = new Thickness(
                    index is 0 or 3 ? 1 : 0, 0, index is 1 or 4 ? 1 : 0, last ? 1 : 0);
            }
            summaryRows.Rows.Add(row);
        }
        SummaryRow("Total sales", $"₱{report.TotalSales:N2}", "System invoices", report.SystemReceipts.ToString(), primary: true);
        SummaryRow("Discounts", $"₱{report.Discounts:N2}", "Manual invoices", report.ManualReceipts.ToString());
        SummaryRow("Net sales (excl. VAT)", $"₱{report.NetSales:N2}", "Missing invoices", report.MissingReceipts.ToString());
        SummaryRow("VAT", $"₱{report.Vat:N2}", "Voided invoices", report.VoidCount.ToString());
        SummaryRow("Transactions", report.TransactionCount.ToString(), "Voided amount", $"₱{report.VoidTotal:N2}", last: true);
        document.Blocks.Add(summary);

        var headings = new[] { "INVOICE", "TIME", "CUSTOMER", "PAYMENT", "GROSS", "DISC", "TOTAL", "VAT", "NET", "VOID" };
        var widths = new[] { 110.0, 60, 180, 75, 80, 70, 85, 75, 80, 45 };
        var alignments = new[] { TextAlignment.Left, TextAlignment.Left, TextAlignment.Left, TextAlignment.Left,
            TextAlignment.Right, TextAlignment.Right, TextAlignment.Right, TextAlignment.Right, TextAlignment.Right, TextAlignment.Center };

        TableCell Cell(string text, int column, bool bold = false, Brush? foreground = null)
        {
            var run = new Run(text) { FontWeight = bold ? FontWeights.Bold : FontWeights.Normal };
            if (foreground != null) run.Foreground = foreground;
            var cell = new TableCell(new Paragraph(run) { TextAlignment = alignments[column], Margin = new Thickness(0), KeepTogether = true })
            { Padding = new Thickness(3, 4, 3, 4), BorderBrush = border, BorderThickness = new Thickness(0) };
            return cell;
        }

        var table = new Table { CellSpacing = 0 };
        foreach (var width in widths) table.Columns.Add(new TableColumn { Width = new GridLength(width) });
        var rows = new TableRowGroup();
        table.RowGroups.Add(rows);
        var heading = new TableRow();
        rows.Rows.Add(heading);
        for (var i = 0; i < headings.Length; i++)
        {
            var cell = Cell(headings[i], i, true);
            cell.BorderThickness = new Thickness(0, 1, 0, 1);
            heading.Cells.Add(cell);
        }

        foreach (var sale in report.Sales)
        {
            var foreground = sale.IsVoided ? muted : null;
            var row = new TableRow();
            rows.Rows.Add(row);
            row.Cells.Add(Cell(sale.ReceiptNumber ?? "—", 0, foreground: foreground));
            row.Cells.Add(Cell(sale.SaleDate.ToString("h:mm tt"), 1, foreground: foreground));
            row.Cells.Add(Cell(sale.CustomerName ?? "WALK-IN", 2, foreground: foreground));
            row.Cells.Add(Cell(sale.Payments.FirstOrDefault()?.Method.ToString().ToUpperInvariant() ?? "CASH", 3, foreground: foreground));
            row.Cells.Add(Cell(sale.GrossAmount.ToString("N2"), 4, foreground: foreground));
            row.Cells.Add(Cell(sale.DiscountAmount.ToString("N2"), 5, foreground: foreground));
            row.Cells.Add(Cell(sale.TotalAmount.ToString("N2"), 6, !sale.IsVoided, foreground));
            row.Cells.Add(Cell(sale.VatAmount.ToString("N2"), 7, foreground: foreground));
            row.Cells.Add(Cell(sale.NetAmount.ToString("N2"), 8, foreground: foreground));
            row.Cells.Add(Cell(sale.IsVoided ? "VOID" : "", 9, foreground: foreground));
        }
        if (rows.Rows.Count > 1)
            foreach (var cell in rows.Rows.Last().Cells)
                cell.BorderThickness = new Thickness(0, 0, 0, 1);
        document.Blocks.Add(table);
        document.Blocks.Add(new Paragraph(new Run("Sales totals exclude voided invoices. Net sales = Total sales − VAT; costs and expenses are not deducted."))
        { FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
            TextAlignment = TextAlignment.Left, Margin = new Thickness(0, 6, 0, 0) });
        ReportDocumentChrome.AddFooter(document);
        ReportPagination.SetIdentity(document, $"Daily Sales · {report.Date:yyyy-MM-dd}");
        ReportPagination.SetPlainTableHeaders(document, true);
        return document;
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

public sealed class SaleRowVM
{
    public Sale Sale { get; }
    public SaleRowVM(Sale sale) => Sale = sale;
    public int SaleNumber => Sale.SaleNumber;
    public string TimeText => Sale.SaleDate.ToString("h:mm:ss tt");
    public string ReceiptNumber => Sale.ReceiptNumber ?? "—";
    public string CustomerName => Sale.CustomerName ?? "WALK-IN";
    public string PayModeText => (Sale.Payments.FirstOrDefault()?.Method.ToString().ToUpperInvariant() ?? "CASH")
        + (Sale.DiscountKind == RetailForma.Core.Entities.DiscountKind.SeniorPwd ? " (SC)" : "");
    public decimal GrossAmount => Sale.GrossAmount;
    public decimal DiscountAmount => Sale.DiscountAmount;
    public decimal TotalAmount => Sale.TotalAmount;
    public decimal VatAmount => Sale.VatAmount;
    public decimal NetAmount => Sale.NetAmount;
    public string VoidedText => Sale.IsVoided ? "YES" : "";
}
