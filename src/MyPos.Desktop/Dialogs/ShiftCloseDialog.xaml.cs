using System.Windows;
using MyPos.Core.Services;
namespace MyPos.Desktop.Dialogs;
public partial class ShiftCloseDialog : Window
{
    private readonly ShiftService _service = new(App.Db); private readonly MyPos.Core.Entities.CashShift? _shift; private ShiftService.ShiftTotals? _totals; private decimal _expected;
    public ShiftCloseDialog() { InitializeComponent(); _shift = _service.GetOpenShift(); if (_shift == null) { Close(); return; } _totals = _service.GetShiftTotals(_shift.Id); _expected = _shift.OpeningFloat + _totals.CashSales + _totals.CashIn - _totals.CashOut - _totals.VoidedCashSales; FloatText.Text = $"Opening float: ₱{_shift.OpeningFloat:N2}"; CashSalesText.Text = $"Cash sales: ₱{_totals.CashSales:N2}"; CashIOText.Text = $"Cash in/out: +₱{_totals.CashIn:N2} / -₱{_totals.CashOut:N2}"; VoidText.Text = $"Voided cash: -₱{_totals.VoidedCashSales:N2}"; ExpectedText.Text = $"EXPECTED DRAWER: ₱{_expected:N2}"; }
    private void CountedBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) { if (decimal.TryParse(CountedBox.Text, out var value)) VariancePreview.Text = $"Variance: ₱{value - _expected:+0.00;-0.00;0.00}"; }
    private void SaveButton_Click(object sender, RoutedEventArgs e) { if (_shift == null || !decimal.TryParse(CountedBox.Text, out var counted) || counted < 0) { ErrorText.Text = "Enter a valid counted amount."; return; } try { _service.CloseShift(App.CurrentUser!.Id, counted, NotesBox.Text); DialogResult = true; } catch (Exception ex) { ErrorText.Text = ex.Message; } }
}
