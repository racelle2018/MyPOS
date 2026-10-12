using System.Windows;
using System.Windows.Controls;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop.Views;

public partial class ShiftView : UserControl
{
    public ShiftView() { InitializeComponent(); Load(); }

    private void ShiftLayout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 950;
        ShiftTotals.Columns = compact ? 3 : 6;
        Grid.SetRow(ShiftActions, compact ? 1 : 0);
        Grid.SetColumn(ShiftActions, compact ? 0 : 1);
        Grid.SetColumnSpan(ShiftActions, compact ? 2 : 1);
        ShiftActions.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }

    private void Load()
    {
        var service = new ShiftService(App.Db);
        var open = service.GetOpenShift();
        OpenShiftButton.IsEnabled = open == null;
        CashIOButton.IsEnabled = open != null && service.CanUseDrawer(open, App.CurrentUser!.Id);
        CloseShiftButton.IsEnabled = CashIOButton.IsEnabled;
        if (open == null)
        {
            StatusHeader.Text = "NO OPEN SHIFT";
            StatusDetail.Text = "Open a shift to declare the opening float before selling.";
            CashSalesText.Text = NonCashText.Text = CashInText.Text = CashOutText.Text = ExpectedText.Text = "₱0.00";
            TxnText.Text = "0";
        }
        else if (!CashIOButton.IsEnabled)
        {
            StatusHeader.Text = "DRAWER ASSIGNED TO ANOTHER CASHIER";
            StatusDetail.Text = "This drawer's totals are restricted to its assigned cashier and administrators.";
            CashSalesText.Text = NonCashText.Text = CashInText.Text = CashOutText.Text = ExpectedText.Text = "—";
            TxnText.Text = "—";
        }
        else
        {
            var totals = service.GetShiftTotals(open.Id);
            StatusHeader.Text = "SHIFT OPEN";
            var cashier = App.Db.Users.Find(open.UserId)?.Username ?? "Cashier";
            var assignment = open.DrawerMode == DrawerMode.Shared ? "Shared drawer" :
                $"Assigned: {App.Db.Users.Find(open.AssignedCashierId)?.FullName ?? "Unknown cashier"}";
            StatusDetail.Text = $"{assignment} · {cashier} · opened {open.OpenedAt:ddd, MMM d h:mm tt} · opening float ₱{open.OpeningFloat:N2}";
            CashSalesText.Text = $"₱{totals.CashSales:N2}"; NonCashText.Text = $"₱{totals.NonCashSales:N2}";
            CashInText.Text = $"₱{totals.CashIn:N2}"; CashOutText.Text = $"₱{totals.CashOut:N2}";
            TxnText.Text = totals.TransactionCount.ToString();
            ExpectedText.Text = $"₱{open.OpeningFloat + totals.CashSales + totals.CashIn - totals.CashOut:N2}";
        }
        var users = App.Db.Users.ToDictionary(u => u.Id, u => u.Username);
        ShiftsGrid.ItemsSource = App.Db.CashShifts.OrderByDescending(s => s.OpenedAt).ToList()
            .Where(s => service.CanUseDrawer(s, App.CurrentUser!.Id)).Select(s => new ShiftRowVM(s, users.GetValueOrDefault(s.UserId) ?? "—")).ToList();
        var count = ShiftsGrid.Items.Count;
        CountText.Text = $"{count} accessible shift{(count == 1 ? "" : "s")}";
        EmptyHint.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenShiftButton_Click(object sender, RoutedEventArgs e)
    {
        var d = new ShiftOpenDialog { Owner = Window.GetWindow(this) };
        if (d.ShowDialog() != true) return;
        try
        {
            new ShiftService(App.Db).OpenShift(App.Db.Branches.OrderBy(b => b.CreatedAt).First().Id,
                App.CurrentUser!.Id, d.OpeningFloat);
            Load();
            MyPos.Desktop.Dialogs.ActionDialog.Success("Shift opened.", "Open Shift", Window.GetWindow(this));
        }
        catch (InvalidOperationException ex) { MyPos.Desktop.Dialogs.ActionDialog.Show(ex.Message, "Open Shift", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void CashIOButton_Click(object sender, RoutedEventArgs e) { if (new CashMovementDialog().ShowDialog() == true) Load(); }
    private void CloseShiftButton_Click(object sender, RoutedEventArgs e) { if (new ShiftCloseDialog().ShowDialog() == true) Load(); }
    private void Reconciliation_Click(object sender, RoutedEventArgs e)
    {
        if (ShiftsGrid.SelectedItem is not ShiftRowVM row)
        { MyPos.Desktop.Dialogs.ActionDialog.Show("Select a shift first.", "Shift history", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        try
        {
            CreateReconciliationDialog(row.ShiftId).ShowDialog();
        }
        catch (InvalidOperationException ex) { MyPos.Desktop.Dialogs.ActionDialog.Show(ex.Message, "Shift history", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    internal ReportHostWindow CreateReconciliationDialog(Guid shiftId)
    {
        new ShiftReportService(App.Db).GetReport(shiftId, App.CurrentUser!.Id);
        return new ReportHostWindow("Shift Reconciliation", new ShiftReconciliationView(shiftId))
            { Owner = Window.GetWindow(this) };
    }
}

public sealed class ShiftRowVM
{
    private readonly CashShift _shift; public ShiftRowVM(CashShift shift, string username) { _shift = shift; UserText = username; }
    public Guid ShiftId => _shift.Id;
    public string OpenedText => _shift.OpenedAt.ToString("MMM d, h:mm tt"); public string ClosedText => _shift.ClosedAt?.ToString("MMM d, h:mm tt") ?? "—"; public string UserText { get; }
    public decimal OpeningFloat => _shift.OpeningFloat; public decimal ExpectedDrawer => _shift.ExpectedDrawer; public decimal? CountedDrawer => _shift.CountedDrawer;
    public string VarianceText => _shift.ClosedAt == null ? "—" : _shift.Variance.ToString("+0.00;-0.00;0.00");
}
