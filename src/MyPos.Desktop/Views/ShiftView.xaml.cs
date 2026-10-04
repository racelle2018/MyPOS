using System.Windows;
using System.Windows.Controls;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop.Views;

public partial class ShiftView : UserControl
{
    public ShiftView() { InitializeComponent(); Load(); }

    private void Load()
    {
        var service = new ShiftService(App.Db);
        var open = service.GetOpenShift();
        OpenShiftButton.IsEnabled = open == null;
        CashIOButton.IsEnabled = open != null;
        CloseShiftButton.IsEnabled = open != null;
        if (open == null)
        {
            StatusHeader.Text = "NO OPEN SHIFT";
            StatusDetail.Text = "Open a shift to declare the opening float before selling.";
            CashSalesText.Text = NonCashText.Text = CashInText.Text = CashOutText.Text = ExpectedText.Text = "₱0.00";
            TxnText.Text = "0";
        }
        else
        {
            var totals = service.GetShiftTotals(open.Id);
            StatusHeader.Text = "SHIFT OPEN";
            StatusDetail.Text = $"Opened {open.OpenedAt:ddd, MMM d h:mm tt} · opening float ₱{open.OpeningFloat:N2}";
            CashSalesText.Text = $"₱{totals.CashSales:N2}"; NonCashText.Text = $"₱{totals.NonCashSales:N2}";
            CashInText.Text = $"₱{totals.CashIn:N2}"; CashOutText.Text = $"₱{totals.CashOut:N2}";
            TxnText.Text = totals.TransactionCount.ToString();
            ExpectedText.Text = $"₱{open.OpeningFloat + totals.CashSales + totals.CashIn - totals.CashOut:N2}";
        }
        var users = App.Db.Users.ToDictionary(u => u.Id, u => u.Username);
        ShiftsGrid.ItemsSource = App.Db.CashShifts.OrderByDescending(s => s.OpenedAt).Take(15).ToList().Select(s => new ShiftRowVM(s, users.GetValueOrDefault(s.UserId) ?? "—")).ToList();
    }

    private void OpenShiftButton_Click(object sender, RoutedEventArgs e) { var d = new ShiftOpenDialog(); if (d.ShowDialog() == true) { new ShiftService(App.Db).OpenShift(App.Db.Branches.OrderBy(b => b.CreatedAt).First().Id, App.CurrentUser!.Id, d.OpeningFloat); Load(); } }
    private void CashIOButton_Click(object sender, RoutedEventArgs e) { if (new CashMovementDialog().ShowDialog() == true) Load(); }
    private void CloseShiftButton_Click(object sender, RoutedEventArgs e) { if (new ShiftCloseDialog().ShowDialog() == true) Load(); }
}

public sealed class ShiftRowVM
{
    private readonly CashShift _shift; public ShiftRowVM(CashShift shift, string username) { _shift = shift; UserText = username; }
    public string OpenedText => _shift.OpenedAt.ToString("MMM d, h:mm tt"); public string ClosedText => _shift.ClosedAt?.ToString("MMM d, h:mm tt") ?? "—"; public string UserText { get; }
    public decimal OpeningFloat => _shift.OpeningFloat; public decimal ExpectedDrawer => _shift.ExpectedDrawer; public decimal? CountedDrawer => _shift.CountedDrawer;
    public string VarianceText => _shift.ClosedAt == null ? "—" : _shift.Variance.ToString("+0.00;-0.00;0.00");
}
