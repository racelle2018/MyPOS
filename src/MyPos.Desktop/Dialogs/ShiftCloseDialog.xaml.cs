using System.Windows;
using MyPos.Core.Services;

namespace MyPos.Desktop.Dialogs;

public partial class ShiftCloseDialog : Window
{
    private readonly ShiftService _service = new(App.Db);
    private readonly MyPos.Core.Entities.CashShift? _shift;
    private readonly decimal _expected;

    public ShiftCloseDialog()
    {
        InitializeComponent();
        _shift = _service.GetOpenShift();
        if (_shift == null)
        {
            Close();
            return;
        }

        var totals = _service.GetShiftTotals(_shift.Id);
        _expected = _shift.OpeningFloat + totals.CashSales + totals.CashIn - totals.CashOut;
        FloatText.Text = $"Opening float: ₱{_shift.OpeningFloat:N2}";
        CashSalesText.Text = $"Net cash sales: ₱{totals.CashSales:N2}";
        CashIOText.Text = $"Cash in/out: +₱{totals.CashIn:N2} / -₱{totals.CashOut:N2}";
        VoidText.Text = $"Voided cash (already excluded): ₱{totals.VoidedCashSales:N2}";
        ExpectedText.Text = $"EXPECTED DRAWER: ₱{_expected:N2}";
    }

    private void CountedBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (decimal.TryParse(CountedBox.Text, out var value))
            VariancePreview.Text = $"Variance: ₱{value - _expected:+0.00;-0.00;0.00}";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_shift == null || !decimal.TryParse(CountedBox.Text, out var counted) || counted < 0)
        {
            ErrorText.Text = "Enter a valid counted amount.";
            return;
        }

        try
        {
            _service.CloseShift(App.CurrentUser!.Id, counted, NotesBox.Text);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }
}
