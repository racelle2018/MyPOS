using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Documents;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop.Views;

public partial class DailySalesView : UserControl
{
    private readonly Guid _branchId;
    private DailySalesReport? _report;

    public DailySalesView()
    {
        InitializeComponent();
        _branchId = App.Db.Branches.OrderBy(b => b.CreatedAt).First().Id;
        VoidButton.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        DatePick.SelectedDate = DateTime.Today;
    }

    private void LoadDate(DateTime date)
    {
        _report = new ReportService(App.Db).GetDailySales(date, _branchId);
        SalesGrid.ItemsSource = _report.Sales.Select(s => new SaleRowVM(s)).ToList();
        ReportEmptyHint.Visibility = _report.Sales.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateSummary();
        UpdateActions();
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
        VoidButton.IsEnabled = Permissions.IsAdmin &&
            SalesGrid.SelectedItem is SaleRowVM { Sale.IsVoided: false };
    }

    private void TodayButton_Click(object sender, RoutedEventArgs e)
        => DatePick.SelectedDate = DateTime.Today;

    private void PrevDayButton_Click(object sender, RoutedEventArgs e)
        => DatePick.SelectedDate = (DatePick.SelectedDate ?? DateTime.Today).AddDays(-1);

    private void NextDayButton_Click(object sender, RoutedEventArgs e)
        => DatePick.SelectedDate = (DatePick.SelectedDate ?? DateTime.Today).AddDays(1);

    private void DatePick_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DatePick.SelectedDate is DateTime date)
        {
            LoadDate(date);
        }
    }

    private void VoidButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("void sales")) return;
        if (SalesGrid.SelectedItem is not SaleRowVM row)
        {
            MessageBox.Show("Select a sale in the table first.", "MyPos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (row.Sale.IsVoided)
        {
            MessageBox.Show("This sale is already voided.", "MyPos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (new VoidSaleDialog(row.Sale).ShowDialog() == true)
            LoadDate(DatePick.SelectedDate ?? DateTime.Today);
    }

    private void SalesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SalesGrid.SelectedItem is SaleRowVM row)
            new SaleDetailDialog(row.Sale).ShowDialog();
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_report == null || _report.Sales.Count == 0)
        {
            MessageBox.Show("No sales to export for this date.", "MyPos");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"DAILY-SALES-{_report.Date:yyyy-MM-dd}.csv"
        };
        if (dialog.ShowDialog() != true) return;

        var csv = new StringBuilder();
        csv.AppendLine("Sale #,Time,Invoice/OR,Customer,Pay Mode,Senior/PWD ID,Gross,Discount,Total,VAT,Net,Voided");
        foreach (var sale in _report.Sales)
        {
            csv.AppendLine(string.Join(",", sale.SaleNumber,
                sale.SaleDate.ToString("HH:mm:ss"), Quote(sale.ReceiptNumber ?? ""),
                Quote(sale.CustomerName ?? "WALK-IN"),
                (sale.Payments.FirstOrDefault()?.Method.ToString().ToUpperInvariant() ?? "CASH") +
                    (sale.DiscountKind == MyPos.Core.Entities.DiscountKind.SeniorPwd ? " (SC)" : ""),
                Quote(sale.SeniorIdNumber ?? ""),
                sale.GrossAmount.ToString("N2"), sale.DiscountAmount.ToString("N2"),
                sale.TotalAmount.ToString("N2"), sale.VatAmount.ToString("N2"),
                sale.NetAmount.ToString("N2"), sale.IsVoided ? "YES" : ""));
        }
        File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
        MessageBox.Show($"Exported {_report.Sales.Count} rows to:\n{dialog.FileName}", "Export Complete");
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        if (_report == null || _report.Sales.Count == 0)
        {
            MessageBox.Show("No sales to print for this date.", "MyPos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preview = new ReportPrintPreviewWindow(BuildReportDocument()) { Owner = Window.GetWindow(this) };
        preview.ShowDialog();
    }

    private FlowDocument BuildReportDocument()
    {
        var report = _report!;
        var border = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
        var headerBackground = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
        var muted = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
        var document = new FlowDocument
        {
            PageWidth = 1123,
            PageHeight = 794,
            PagePadding = new Thickness(40, 30, 40, 30),
            FontFamily = (System.Windows.Media.FontFamily)FindResource("UiFontFamily"),
            FontSize = 9,
            ColumnWidth = double.PositiveInfinity
        };

        document.Blocks.Add(new Paragraph(new Run(AppSettings.Get("CompanyName", "MY STORE")))
        { FontSize = 16, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center });
        document.Blocks.Add(new Paragraph(new Run($"DAILY SALES REPORT — {report.Date:dddd, MMMM d, yyyy}".ToUpperInvariant()))
        { FontSize = 11, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 4) });
        document.Blocks.Add(new Paragraph(new Run(
            $"GENERATED {DateTime.Now:MM/dd/yyyy h:mm:ss tt} BY {App.CurrentUser?.FullName ?? "—"}"))
        { FontSize = 8, Foreground = muted, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 14) });

        TableCell SummaryCell(string label, string value)
        {
            var cell = new TableCell { Padding = new Thickness(6, 4, 6, 4), BorderBrush = border,
                BorderThickness = new Thickness(0, 0, 1, 1) };
            cell.Blocks.Add(new Paragraph(new Run(label)) { FontSize = 7.5, Foreground = muted, Margin = new Thickness(0) });
            cell.Blocks.Add(new Paragraph(new Run(value)) { FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 1, 0, 0) });
            return cell;
        }

        var summary = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14) };
        for (var i = 0; i < 5; i++) summary.Columns.Add(new TableColumn { Width = new GridLength(190) });
        var summaryRows = new TableRowGroup();
        summary.RowGroups.Add(summaryRows);
        summaryRows.Rows.Add(new TableRow { Cells =
        {
            SummaryCell("TOTAL SALES", $"₱{report.TotalSales:N2}"), SummaryCell("NET SALES", $"₱{report.NetSales:N2}"),
            SummaryCell("VAT", $"₱{report.Vat:N2}"), SummaryCell("TRANSACTIONS", report.TransactionCount.ToString()),
            SummaryCell("AVG. SALE", $"₱{report.AverageSale:N2}")
        }});
        summaryRows.Rows.Add(new TableRow { Cells =
        {
            SummaryCell("SYSTEM RECEIPTS", report.SystemReceipts.ToString()), SummaryCell("MANUAL OR", report.ManualReceipts.ToString()),
            SummaryCell("MISSING RECEIPTS", report.MissingReceipts.ToString()), SummaryCell("VOIDS", $"{report.VoidCount} (₱{report.VoidTotal:N2})"),
            SummaryCell("DISCOUNTS", $"₱{report.Discounts:N2}")
        }});
        document.Blocks.Add(summary);

        var headings = new[] { "SALE #", "TIME", "INVOICE/OR", "CUSTOMER", "PAY", "GROSS", "DISC", "TOTAL", "VAT", "NET", "VOID" };
        var widths = new[] { 45.0, 60, 90, 170, 55, 80, 70, 85, 75, 80, 45 };
        var alignments = new[] { TextAlignment.Left, TextAlignment.Left, TextAlignment.Left, TextAlignment.Left, TextAlignment.Left,
            TextAlignment.Right, TextAlignment.Right, TextAlignment.Right, TextAlignment.Right, TextAlignment.Right, TextAlignment.Center };

        TableCell Cell(string text, int column, bool bold = false, Brush? foreground = null, Brush? background = null)
        {
            var run = new Run(text) { FontWeight = bold ? FontWeights.Bold : FontWeights.Normal };
            if (foreground != null) run.Foreground = foreground;
            var cell = new TableCell(new Paragraph(run) { TextAlignment = alignments[column], Margin = new Thickness(0) })
            { Padding = new Thickness(5, 3, 5, 3), BorderBrush = border, BorderThickness = new Thickness(0, 0, 1, 1) };
            if (background != null) cell.Background = background;
            return cell;
        }

        var table = new Table { CellSpacing = 0 };
        foreach (var width in widths) table.Columns.Add(new TableColumn { Width = new GridLength(width) });
        var rows = new TableRowGroup();
        table.RowGroups.Add(rows);
        var heading = new TableRow();
        rows.Rows.Add(heading);
        for (var i = 0; i < headings.Length; i++) heading.Cells.Add(Cell(headings[i], i, true, background: headerBackground));

        foreach (var sale in report.Sales)
        {
            var foreground = sale.IsVoided ? muted : null;
            var row = new TableRow();
            rows.Rows.Add(row);
            row.Cells.Add(Cell(sale.SaleNumber.ToString(), 0, foreground: foreground));
            row.Cells.Add(Cell(sale.SaleDate.ToString("h:mm tt"), 1, foreground: foreground));
            row.Cells.Add(Cell(sale.ReceiptNumber ?? "—", 2, foreground: foreground));
            row.Cells.Add(Cell(sale.CustomerName ?? "WALK-IN", 3, foreground: foreground));
            row.Cells.Add(Cell(sale.Payments.FirstOrDefault()?.Method.ToString().ToUpperInvariant() ?? "CASH", 4, foreground: foreground));
            row.Cells.Add(Cell(sale.GrossAmount.ToString("N2"), 5, foreground: foreground));
            row.Cells.Add(Cell(sale.DiscountAmount.ToString("N2"), 6, foreground: foreground));
            row.Cells.Add(Cell(sale.TotalAmount.ToString("N2"), 7, !sale.IsVoided, foreground));
            row.Cells.Add(Cell(sale.VatAmount.ToString("N2"), 8, foreground: foreground));
            row.Cells.Add(Cell(sale.NetAmount.ToString("N2"), 9, foreground: foreground));
            row.Cells.Add(Cell(sale.IsVoided ? "VOID" : "", 10, foreground: foreground));
        }
        document.Blocks.Add(table);
        document.Blocks.Add(new Paragraph(new Run(
            $"{report.TransactionCount} TRANSACTION(S) — TOTAL ₱{report.TotalSales:N2} — NET ₱{report.NetSales:N2} — VAT ₱{report.Vat:N2}"))
        { FontSize = 9, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
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
        + (Sale.DiscountKind == MyPos.Core.Entities.DiscountKind.SeniorPwd ? " (SC)" : "");
    public decimal GrossAmount => Sale.GrossAmount;
    public decimal DiscountAmount => Sale.DiscountAmount;
    public decimal TotalAmount => Sale.TotalAmount;
    public decimal VatAmount => Sale.VatAmount;
    public decimal NetAmount => Sale.NetAmount;
    public string VoidedText => Sale.IsVoided ? "YES" : "";
}
