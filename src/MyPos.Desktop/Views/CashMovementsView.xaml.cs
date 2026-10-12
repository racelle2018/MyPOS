using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using MyPos.Desktop.Controls;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop.Views;

public partial class CashMovementsView : UserControl
{
    private bool _loading = true;
    private CashMovementReport? _report;
    private readonly Guid _branchId;
    private readonly Guid? _shiftId;

    public CashMovementsView(Guid? shiftId = null)
    {
        InitializeComponent();
        _shiftId = shiftId;
        CategoryFilterBox.ItemsSource = new[] { "All classifications" }.Concat(Enum.GetValues<CashMovementCategory>().Select(CashMovementReport.ClassificationLabel)).ToList();
        CategoryFilterBox.SelectedIndex = 0;
        ApproveButton.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        _branchId = App.Db.Branches.OrderBy(b => b.CreatedAt).First().Id;
        FromPick.DisplayDateEnd = ToPick.DisplayDateEnd = DateTime.Today;
        TypeBox.SelectedIndex = 0;
        FromPick.SelectedDate = DateTime.Today.AddDays(-6);
        ToPick.SelectedDate = DateTime.Today;
        if (shiftId.HasValue)
        {
            var shift = App.Db.CashShifts.First(s => s.Id == shiftId.Value);
            FromPick.SelectedDate = shift.OpenedAt.Date; ToPick.SelectedDate = (shift.ClosedAt ?? DateTime.Today).Date;
        }
        _loading = false;
        LoadReport();
        Loaded += (_, _) => FromPick.DisplayDateEnd = ToPick.DisplayDateEnd = DateTime.Today;
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    { if (!_loading) LoadReport(); }

    private void SetRange(DateTime from, DateTime to)
    {
        _loading = true; FromPick.SelectedDate = from; ToPick.SelectedDate = to; _loading = false;
        LoadReport();
    }
    private void Today_Click(object sender, RoutedEventArgs e) => SetRange(DateTime.Today, DateTime.Today);
    private void Week_Click(object sender, RoutedEventArgs e) => SetRange(DateTime.Today.AddDays(-6), DateTime.Today);
    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadReport();

    private void LoadReport()
    {
        _report = null;
        MovementsGrid.ItemsSource = null;
        PrintButton.IsEnabled = ExportButton.IsEnabled = false;
        CashInText.Text = CashOutText.Text = NetText.Text = "—";
        try
        {
            if (FromPick.SelectedDate is not DateTime from || ToPick.SelectedDate is not DateTime to ||
                ReportDatePickerBehavior.GetHasInputError(FromPick) || ReportDatePickerBehavior.GetHasInputError(ToPick))
                throw new InvalidOperationException("Choose valid From and To dates.");
            var type = TypeBox.SelectedIndex switch
            { 1 => CashMovementType.CashIn, 2 => CashMovementType.CashOut, _ => (CashMovementType?)null };
            _report = new CashMovementReportService(App.Db).GetReport(_branchId,
                App.CurrentUser?.Id ?? Guid.Empty, DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), type,
                CategoryFilterBox.SelectedIndex > 0 ? Enum.GetValues<CashMovementCategory>()[CategoryFilterBox.SelectedIndex - 1] : null, SearchBox.Text.Trim(), _shiftId);
            MovementsGrid.ItemsSource = _report.Rows.Select(r => new MovementRow(r)).ToList();
            CashInText.Text = Money(_report.CashIn); CashOutText.Text = Money(_report.CashOut);
            NetText.Text = Money(_report.NetMovement);
            EmptyText.Text = _report.Rows.Count == 0 ? "No cash movements match this range and your drawer access." : "";
            StatusText.Text = $"{_report.Rows.Count} movement(s) · {_report.Branch} · Accessible drawers only. " +
                "Net movement excludes sales, refunds and opening float. " + _report.CashOutBreakdown;
            PrintButton.IsEnabled = ExportButton.IsEnabled = true;
        }
        catch (InvalidOperationException ex)
        {
            EmptyText.Text = ex.Message;
            StatusText.Text = "Report unavailable. Correct the filters or check your access.";
        }
    }

    private static string Money(decimal value) => $"₱{value:N2}";
    private void MovementsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 0 || MovementsGrid.Columns.Count != 9) return;
        var compact = e.NewSize.Width < 1000;
        var widths = new[] { compact ? 145d : 155d, 80d, 120d, 0d, compact ? 110d : 125d,
            compact ? 130d : 140d, compact ? 95d : 110d, 170d, 160d };
        widths[3] = Math.Max(140, e.NewSize.Width - 2 - SystemParameters.VerticalScrollBarWidth - widths.Sum());
        for (var i = 0; i < widths.Length; i++) MovementsGrid.Columns[i].Width = new DataGridLength(widths[i]);
    }
    private string FileStem => $"Cash-Movements-{_report!.From:yyyy-MM-dd}-to-{_report.To:yyyy-MM-dd}-" +
        (_report.Type?.ToString() ?? "All");

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        LoadReport(); // Recheck access and refresh data before exporting.
        if (_report == null) { MyPos.Desktop.Dialogs.ActionDialog.Warning("Load a valid cash movements report first.", "Export CSV", Window.GetWindow(this)); return; }
        var picker = new Microsoft.Win32.SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = FileStem + ".csv" };
        if (picker.ShowDialog() != true) return;
        try { File.WriteAllText(picker.FileName, _report.ToCsv(), System.Text.Encoding.UTF8); MyPos.Desktop.Dialogs.ActionDialog.Success($"CSV saved to:\n{picker.FileName}", "Export CSV", Window.GetWindow(this)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MyPos.Desktop.Dialogs.ActionDialog.Show(ex.Message, "Export CSV", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        LoadReport();
        if (_report == null) { MyPos.Desktop.Dialogs.ActionDialog.Warning("Load a valid cash movements report first.", "Print report", Window.GetWindow(this)); return; }
        new ReportPrintPreviewWindow(BuildDocument(), "Cash Movements", FileStem + ".pdf")
            { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    internal FlowDocument BuildDocument()
    {
        var report = _report ?? throw new InvalidOperationException("Load a valid report first.");
        static string Details(CashMovementReportRow row) => string.Join("\n", new[]
        {
            row.Reason,
            string.IsNullOrWhiteSpace(row.ExpenseCategory) ? null : "Expense category: " + row.ExpenseCategory,
            string.IsNullOrWhiteSpace(row.Destination) ? null : "Destination: " + row.Destination,
            string.IsNullOrWhiteSpace(row.Reference) ? null : "Reference: " + row.Reference
        }.Where(v => !string.IsNullOrWhiteSpace(v)));
        static string Approval(CashMovementReportRow row) => "Recorded by: " + row.User + "\n" +
            (row.ApprovedAt.HasValue ? $"Approved by: {row.Approver}\nApproved: {row.ApprovedAt:MM/dd/yyyy h:mm tt}" : "Approval: Unapproved");
        var totals = $"Cash in: {Money(report.CashIn)} | Cash out: {Money(report.CashOut)} | Net movement: {Money(report.NetMovement)}";
        if (report.CashOutBreakdown.Length > 0) totals += "\n" + report.CashOutBreakdown;
        return ReportTableDocument.Build(new ReportTableData("CASH MOVEMENTS REPORT",
            $"{report.Branch} | {report.From:yyyy-MM-dd} to {report.To:yyyy-MM-dd} | {report.FilterLabel}",
            "Accessible drawers only. Names use current directory. Net movement excludes sales, refunds and float. Classification is operational, not an expense ledger.",
            totals,
            new[] { "Date / time", "Type / classification", "Reason / reference", "User / approval", "Amount" }, new[] { 4 },
            report.Rows.Select(r => new ReportTableRow(new[] { r.Date.ToString("MM/dd/yyyy HH:mm:ss"),
                $"{(r.Type == CashMovementType.CashIn ? "Cash in" : "Cash out")}\n{CashMovementReport.ClassificationLabel(r.Category)}",
                Details(r), Approval(r), r.Amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) },
                r.Id.ToString())).ToList())
        {
            PresentationScope = report.DescribePresentation(App.Db.Branches.Count() > 1),
            SummaryBeforeTable = false,
            NoteAfterTable = true,
            PresentationNote = "Accessible drawers only. Net movement = cash in − cash out; excludes sales, refunds and opening float."
        });
    }
    private void Search_Changed(object sender, TextChangedEventArgs e) { if (!_loading) LoadReport(); }
    private void Approve_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("approve cash movements")) return;
        if (MovementsGrid.SelectedItem is not MovementRow row)
        { MyPos.Desktop.Dialogs.ActionDialog.Show("Select a movement first.", "Cash movements", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        try { new ShiftService(App.Db).ApproveCashMovement(row.Id, App.CurrentUser!.Id); LoadReport(); MyPos.Desktop.Dialogs.ActionDialog.Success("Cash movement approved.", "Cash movements", Window.GetWindow(this)); }
        catch (InvalidOperationException ex) { MyPos.Desktop.Dialogs.ActionDialog.Show(ex.Message, "Cash movements", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private sealed class MovementRow(CashMovementReportRow entry)
    {
        public Guid Id => entry.Id;
        public string Category => CashMovementReport.ClassificationLabel(entry.Category);
        public string DestinationReference => string.Join(" / ", new[] { entry.Destination, entry.Reference }.Where(v => !string.IsNullOrWhiteSpace(v)));
        public string Approval => entry.ApprovedAt.HasValue ? $"{entry.Approver} · {entry.ApprovedAt:g}" : "Unapproved";
        public DateTime Date => entry.Date;
        public DateTime ShiftOpenedAt => entry.ShiftOpenedAt;
        public string TypeLabel => entry.Type == CashMovementType.CashIn ? "Cash in" : "Cash out";
        public string Reason => entry.Reason;
        public string User => entry.User;
        public decimal Amount => entry.Amount;
    }
}
