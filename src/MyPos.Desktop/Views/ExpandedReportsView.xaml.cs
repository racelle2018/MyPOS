using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using MyPos.Desktop.Controls;
using MyPos.Desktop.Dialogs;
namespace MyPos.Desktop.Views;

public enum ExpandedReportKind { ProductSales, SalesSummary, VoidEvents }
public partial class ExpandedReportsView : UserControl
{
    private readonly ExpandedReportKind _kind;
    private bool _loading = true;
    private ReportTableData? _data;
    public ExpandedReportsView(ExpandedReportKind kind)
    {
        _kind = kind;
        InitializeComponent();
        var branches = App.Db.Branches.OrderBy(b => b.CreatedAt).ToList();
        BranchBox.ItemsSource = branches.Select(b => new Choice(b.Id, b.Name)).ToList(); BranchBox.SelectedIndex = 0;
        if (kind == ExpandedReportKind.ProductSales && branches.Count == 1) BranchBox.Visibility = Visibility.Collapsed;
        CashierBox.ItemsSource = new[] { new Choice(null, "All cashiers") }.Concat(App.Db.Users.OrderBy(u => u.FullName).Select(u => new Choice(u.Id, u.FullName)).ToList()).ToList();
        CashierBox.SelectedIndex = 0;
        PaymentBox.ItemsSource = new[] { new Choice(null, "All payments") }.Concat(Enum.GetValues<PaymentMethod>().Select(m => new Choice((int)m, m.ToString()))).ToList();
        PaymentBox.SelectedIndex = 0;
        CategoryBox.ItemsSource = new[] { new Choice("", "All categories") }.Concat(App.Db.SaleItems.Select(i => i.CategorySnapshot).Distinct().ToList()
            .OrderBy(c => c).Select(c => new Choice(c == "" ? "__UNCATEGORIZED" : c ?? "Historical category unavailable",
                c == "" ? "Uncategorized" : c ?? "Historical category unavailable"))).ToList();
        CategoryBox.SelectedIndex = 0;
        CategoryBox.Visibility = kind == ExpandedReportKind.ProductSales ? Visibility.Visible : Visibility.Collapsed;
        if (kind == ExpandedReportKind.ProductSales) { ModeBox.ItemsSource = new[] { "Product detail", "Category summary" }; SearchBox.Tag = "Search product or barcode"; }
        else if (kind == ExpandedReportKind.SalesSummary) ModeBox.ItemsSource = new[] { "Daily summary", "Monthly summary" };
        else { ModeBox.Visibility = PaymentBox.Visibility = Visibility.Collapsed; }
        ModeBox.SelectedIndex = 0;
        FromPick.DisplayDateEnd = ToPick.DisplayDateEnd = DateTime.Today;
        FromPick.SelectedDate = DateTime.Today.AddDays(-6); ToPick.SelectedDate = DateTime.Today;
        _loading = false; LoadReport();
    }
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (!_loading) LoadReport(); }
    private void Search_Changed(object sender, TextChangedEventArgs e) { if (!_loading) LoadReport(); }
    private void SetRange(DateTime from, DateTime to)
    { _loading = true; FromPick.SelectedDate = from; ToPick.SelectedDate = to; _loading = false; LoadReport(); }
    private void Today_Click(object sender, RoutedEventArgs e) => SetRange(DateTime.Today, DateTime.Today);
    private void Week_Click(object sender, RoutedEventArgs e) => SetRange(DateTime.Today.AddDays(-6), DateTime.Today);
    private void Month_Click(object sender, RoutedEventArgs e) => SetRange(DateTime.Today.AddDays(-29), DateTime.Today);
    private void ThisMonth_Click(object sender, RoutedEventArgs e) => SetRange(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);
    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadReport();
    private ReportScope GetScope()
    {
        if (FromPick.SelectedDate is not DateTime from || ToPick.SelectedDate is not DateTime to ||
            ReportDatePickerBehavior.GetHasInputError(FromPick) || ReportDatePickerBehavior.GetHasInputError(ToPick))
            throw new InvalidOperationException("Choose valid From and To dates.");
        return new((Guid)((Choice)BranchBox.SelectedItem).Value!, App.CurrentUser?.Id ?? Guid.Empty,
            DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), ((Choice)CashierBox.SelectedItem).Value as Guid?,
            ((Choice)PaymentBox.SelectedItem).Value is int method ? (PaymentMethod)method : null,
            SearchBox.Text.Trim(), (string)((Choice)CategoryBox.SelectedItem).Value!);
    }
    private void LoadReport()
    {
        _data = null; PrintButton.IsEnabled = ExportButton.IsEnabled = false; ReportGrid.ItemsSource = null; SummaryText.Text = "";
        ReportWarningText.Text = ""; ReportWarningText.Visibility = Visibility.Collapsed;
        try
        {
            var scope = GetScope(); var service = new ExpandedReportService(App.Db);
            _data = _kind switch { ExpandedReportKind.ProductSales => service.ProductSales(scope, ModeBox.SelectedIndex == 1),
                ExpandedReportKind.SalesSummary => service.SalesSummary(scope, ModeBox.SelectedIndex == 1), _ => service.VoidEvents(scope) };
            ReportGrid.Columns.Clear();
            var table = new DataTable();
            for (var i = 0; i < _data.Headers.Length; i++)
            {
                table.Columns.Add("C" + i, typeof(string));
                var numeric = _data.NumericColumns.Contains(i);
                ReportGrid.Columns.Add(new DataGridTextColumn { Header = _data.Headers[i], Binding = new Binding("[C" + i + "]"),
                    Width = numeric ? new DataGridLength(95) : new DataGridLength(140), MinWidth = numeric ? 85 : 100,
                    ElementStyle = numeric ? (Style)FindResource("RightCell") : null });
            }
            foreach (var row in _data.Rows) table.Rows.Add(row.Cells.Cast<object>().ToArray());
            FitColumns(ReportGrid.ActualWidth);
            ReportGrid.ItemsSource = table.DefaultView;
            SummaryText.Text = _data.PresentationSummary ?? _data.Summary;
            ReportWarningText.Text = _data.Warning.Length > 0 ? "Warning: " + _data.Warning : "";
            ReportWarningText.Visibility = _data.Warning.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = (_data.PresentationScope ?? _data.Scope) + "\n" + _data.Note;
            EmptyText.Text = _data.Rows.Count == 0 ? "No records match these filters." : "";
            PrintButton.IsEnabled = ExportButton.IsEnabled = true;
        }
        catch (InvalidOperationException ex) { EmptyText.Text = ex.Message; StatusText.Text = "Report unavailable."; }
    }
    internal System.Windows.Documents.FlowDocument BuildDocument()
        => ReportTableDocument.Build(_data ?? throw new InvalidOperationException("Load a valid report first."));
    private void ReportGrid_SizeChanged(object sender, SizeChangedEventArgs e) => FitColumns(e.NewSize.Width);
    private void FitColumns(double width)
    {
        if (_data == null || width <= 0) return;
        var weights = _data.Headers.Select((h, i) => _data.NumericColumns.Contains(i) ? 95d :
            h is "Product" or "Reason" or "Customer" or "Quantities by unit" ? 220d : 140d).ToArray();
        var scale = Math.Max(1, (width - 2 - SystemParameters.VerticalScrollBarWidth) / weights.Sum());
        for (var i = 0; i < weights.Length; i++) ReportGrid.Columns[i].Width = new DataGridLength(weights[i] * scale);
    }
    private string FileStem => $"{_kind}-{FromPick.SelectedDate:yyyy-MM-dd}-to-{ToPick.SelectedDate:yyyy-MM-dd}-{ModeBox.SelectedIndex}";
    private void Print_Click(object sender, RoutedEventArgs e)
    {
        LoadReport(); if (_data == null) return;
        new ReportPrintPreviewWindow(BuildDocument(), _data.Title, FileStem + ".pdf") { Owner = Window.GetWindow(this) }.ShowDialog();
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        LoadReport(); if (_data == null) return;
        var picker = new Microsoft.Win32.SaveFileDialog { FileName = FileStem + ".csv", Filter = "CSV file (*.csv)|*.csv" };
        if (picker.ShowDialog() != true) return;
        try { File.WriteAllText(picker.FileName, _data.ToCsv(), System.Text.Encoding.UTF8); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show(ex.Message, "Export CSV", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private sealed record Choice(object? Value, string Label);
}
