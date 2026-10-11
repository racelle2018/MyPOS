using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop.Views;

public partial class ShiftReconciliationView : UserControl
{
    private ShiftReconciliationReport? _report;
    private List<MetricRow> _summary = new();
    private List<MetricRow> _drawer = new();
    private List<MovementRow> _movements = new();
    private readonly Guid? _initialShift;
    public ShiftReconciliationView(Guid? shiftId = null) { _initialShift = shiftId; InitializeComponent(); LoadShifts(); }

    private void LoadShifts()
    {
        var selectedId = (ShiftBox.SelectedItem as ShiftChoice)?.Shift.Id ?? _initialShift;
        var service = new ShiftService(App.Db);
        var branchId = App.Db.Branches.OrderBy(b => b.CreatedAt).First().Id;
        var choices = App.Db.CashShifts.Where(s => s.BranchId == branchId).OrderByDescending(s => s.OpenedAt)
            .ToList().Where(s => service.CanUseDrawer(s, App.CurrentUser!.Id))
            .Select(s => new ShiftChoice(s, $"{s.OpenedAt:yyyy-MM-dd h:mm tt} · {(s.ClosedAt == null ? "Open" : "Closed")} · {s.DrawerMode}")).ToList();
        ShiftBox.ItemsSource = choices;
        ShiftBox.SelectedItem = choices.FirstOrDefault(c => c.Shift.Id == selectedId) ?? choices.FirstOrDefault();
        if (choices.Count == 0) { StatusText.Text = "No accessible shifts recorded."; PrintButton.IsEnabled = ExportButton.IsEnabled = false; }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadShifts();
    private void ShiftBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => LoadSelectedShift();

    private void LoadSelectedShift()
    {
        if (PrintButton == null) return;
        _report = null;
        SummaryGrid.ItemsSource = DrawerGrid.ItemsSource = MovementsGrid.ItemsSource = null;
        PrintButton.IsEnabled = ExportButton.IsEnabled = false;
        if (ShiftBox.SelectedItem is not ShiftChoice choice) return;
        try
        {
            _report = new ShiftReportService(App.Db).GetReport(choice.Shift.Id, App.CurrentUser!.Id);
            var shift = _report.Shift;
            DenominationGrid.ItemsSource = shift.DenominationCounts.Select(c => new { c.FaceValue, c.Count, Amount = c.FaceValue * c.Count }).ToList();
            DenominationGrid.Visibility = shift.DenominationCounts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            DenominationHint.Text = shift.DenominationCounts.Count > 0 ? "Recorded denomination count (original close)." : "No denomination breakdown was recorded.";
            var users = App.Db.Users.ToDictionary(u => u.Id, u => u.FullName);
            var assigned = shift.DrawerMode == DrawerMode.Shared ? "Shared" :
                $"Assigned to {users.GetValueOrDefault(shift.AssignedCashierId ?? Guid.Empty, "Unknown cashier")}";
            StatusText.Text = $"{assigned} · opened by {users.GetValueOrDefault(shift.UserId, "Unknown user")} · " +
                $"closed by {users.GetValueOrDefault(shift.ClosedById ?? Guid.Empty, "Not recorded / open")} · {_report.LaterVoidCount} void(s) after closing · {_report.LegacySalesCount} legacy sale(s) linked by time";
            _summary = new() { new("Total sales", Money(_report.CurrentSales)), new("Net sales (not profit)", Money(_report.CurrentNet)),
                new("VAT", Money(_report.CurrentVat)), new("Transactions", _report.TransactionCount.ToString()),
                new("Voids", $"{_report.VoidCount} ({Money(_report.VoidTotal)})") };
            _summary.AddRange(Enum.GetValues<PaymentMethod>().Select(m => new MetricRow(m.ToString(), Money(_report.PaymentTotal(m)))));
            var totals = _report.DrawerTotals;
            _drawer = new() { new("Opening float", Money(shift.OpeningFloat)),
                new("Cash sales less refunds (at closing for closed shifts)", Money(totals.CashSales)),
                new("Cash in", Money(totals.CashIn)), new("Cash out", Money(totals.CashOut)),
                new("Expected drawer", Money(_report.ExpectedDrawer)),
                new("Counted drawer", shift.CountedDrawer.HasValue ? Money(shift.CountedDrawer.Value) : "Not counted"),
                new("Closing variance", shift.ClosedAt.HasValue ? Money(shift.Variance) : "Shift still open") };
            _movements = _report.Movements.Select(m => new MovementRow(m.MovementDate.ToString("yyyy-MM-dd h:mm tt"),
                m.Type.ToString(), $"{m.Category}: {m.Reason}; {m.ExpenseCategory}; {m.Destination}; Ref {m.Reference}; " +
                    (m.ApprovedAt.HasValue ? $"Approved by {users.GetValueOrDefault(m.ApprovedById ?? Guid.Empty, "Unknown")}, {m.ApprovedAt:g}" : "Unapproved"),
                users.GetValueOrDefault(m.UserId, "Unknown user"), Money(m.Amount))).ToList();
            SummaryGrid.ItemsSource = _summary; DrawerGrid.ItemsSource = _drawer; MovementsGrid.ItemsSource = _movements;
            PrintButton.IsEnabled = ExportButton.IsEnabled = true; // Zero-sales shifts still require reconciliation.
        }
        catch (InvalidOperationException ex) { StatusText.Text = ex.Message; }
    }

    private static string Money(decimal value) => $"₱{value:N2}";

    private void MetricGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not DataGrid grid || grid.Columns.Count != 2 || e.NewSize.Width < 552) return;
        var available = e.NewSize.Width - 2;
        var descriptionWidth = Math.Max(400, available * 0.7);
        grid.Columns[0].Width = new DataGridLength(descriptionWidth);
        grid.Columns[1].Width = new DataGridLength(available - descriptionWidth);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        LoadSelectedShift();
        if (_report == null) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV file (*.csv)|*.csv",
            FileName = $"Shift-Reconciliation-{_report.Shift.OpenedAt:yyyy-MM-dd}-{_report.Shift.Id.ToString("N")[..8]}.csv" };
        if (dialog.ShowDialog() != true) return;
        var csv = new System.Text.StringBuilder("Section,Description,Value,User,DateTime\r\n");
        void Row(string section, string description, string value, string user = "", string date = "")
            => csv.AppendLine(string.Join(",", new[] { section, description, value, user, date }.Select(ReportCsv.Text)));
        Row("Scope", "Shift ID", _report.Shift.Id.ToString());
        Row("Scope", "Branch", App.Db.Branches.First(b => b.Id == _report.Shift.BranchId).Name);
        Row("Scope", "Access", StatusText.Text);
        Row("Scope", "Opened", _report.Shift.OpenedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        Row("Scope", "Closed", _report.Shift.ClosedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Open");
        foreach (var row in _summary) Row("Current sales", row.Label, row.Value);
        foreach (var row in _drawer) Row("Drawer accountability", row.Label, row.Value);
        foreach (var row in _movements) Row("Cash movements", row.Type + ": " + row.Reason, row.Amount, row.User, row.Date);
        foreach (var count in _report.Shift.DenominationCounts) Row("Denomination", Money(count.FaceValue), count.Count.ToString() + " × " + Money(count.FaceValue * count.Count));
        Row("Policy", "Later voids", "Current sales update; original closing cash and variance are preserved.");
        try { System.IO.File.WriteAllText(dialog.FileName, csv.ToString(), System.Text.Encoding.UTF8); }
        catch (System.IO.IOException ex) { MessageBox.Show(ex.Message, "Export CSV", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        // Refresh permission and totals immediately before printing.
        LoadSelectedShift();
        if (_report == null) return;
        new ReportPrintPreviewWindow(BuildDocument(), "Shift Reconciliation",
            $"Shift-Reconciliation-{_report.Shift.OpenedAt:yyyy-MM-dd}-{_report.Shift.Id.ToString("N")[..8]}.pdf")
            { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    internal FlowDocument BuildDocument()
    {
        var report = _report!;
        var document = new FlowDocument { FontFamily = (FontFamily)FindResource("ReportInterFontFamily"),
            FontSize = 12, ColumnWidth = double.PositiveInfinity };
        void Paragraph(string text, double size = 10, bool bold = false, bool center = false, double bottomMargin = 4)
            => document.Blocks.Add(new Paragraph(new Run(text)) { FontSize = size,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                TextAlignment = center ? TextAlignment.Center : TextAlignment.Left, Margin = new Thickness(0, 0, 0, bottomMargin) });
        ReportDocumentChrome.AddHeader(document, "SHIFT / CASHIER RECONCILIATION");
        if (App.Db.Branches.Count() > 1)
            Paragraph("Filters: Branch: " + App.Db.Branches.First(b => b.Id == report.Shift.BranchId).Name, 9);
        Paragraph($"Shift {report.Shift.Id} · {report.Shift.OpenedAt:yyyy-MM-dd h:mm tt} — " +
            (report.Shift.ClosedAt?.ToString("yyyy-MM-dd h:mm tt") ?? "Open"));
        Paragraph(StatusText.Text, 9);
        void Table(string title, IEnumerable<string[]> rows, params string[] headers)
        {
            Paragraph(title, 12, true);
            var table = new Table { CellSpacing = 0, FontSize = 10, Margin = new Thickness(0, 0, 0, 10) };
            for (var i = 0; i < headers.Length; i++) table.Columns.Add(new TableColumn { Width = new GridLength(i == 2 ? 240 : 130) });
            var group = new TableRowGroup(); table.RowGroups.Add(group);
            foreach (var values in new[] { headers }.Concat(rows))
            {
                var row = new TableRow(); group.Rows.Add(row);
                for (var i = 0; i < values.Length; i++) row.Cells.Add(new TableCell(new Paragraph(new Run(values[i]))
                    { Margin = new Thickness(0), KeepTogether = true, TextAlignment = i == values.Length - 1 ? TextAlignment.Right : TextAlignment.Left })
                    { Padding = new Thickness(4, 3, 4, 3), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 2, 2) });
            }
            document.Blocks.Add(table);
        }
        Table("Current sales — approved voids excluded", _summary.Select(r => new[] { r.Label, r.Value }), "Description", "Value");
        Table("Drawer accountability", _drawer.Select(r => new[] { r.Label, r.Value }), "Description", "Amount");
        if (report.Shift.DenominationCounts.Count > 0)
            Table("Recorded bills / coins", report.Shift.DenominationCounts.OrderByDescending(c => c.FaceValue)
                .Select(c => new[] { Money(c.FaceValue), c.Count.ToString(), Money(c.FaceValue * c.Count) }), "Face value", "Count", "Amount");
        else Paragraph("No denomination breakdown recorded.", 9);
        Paragraph("Closing count/variance are preserved. Later voids update current sales, not the original cash count. Legacy sales are linked by timestamps.", 9);
        Table("Cash movements — cash out is not necessarily an expense", _movements.Select(r => new[] { r.Date, r.Type, r.Reason, r.User, r.Amount }),
            "Date / time", "Type", "Reason", "User", "Amount");
        ReportDocumentChrome.AddFooter(document, includeReviewers: true);
        ReportPagination.SetIdentity(document, $"Shift / Cashier Reconciliation · {report.Shift.OpenedAt:yyyy-MM-dd}");
        return document;
    }

    private sealed record ShiftChoice(CashShift Shift, string Label);
    private sealed record MetricRow(string Label, string Value);
    private sealed record MovementRow(string Date, string Type, string Reason, string User, string Amount);
}
