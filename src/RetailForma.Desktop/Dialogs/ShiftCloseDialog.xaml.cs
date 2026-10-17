using System.Windows;
using RetailForma.Core.Services;
using System.ComponentModel;
using MaterialDesignThemes.Wpf;

namespace RetailForma.Desktop.Dialogs;

public partial class ShiftCloseDialog : Window
{
    private readonly DesktopShiftCommands _service = App.Service.Drawer;
    private readonly RetailForma.Core.Entities.CashShift? _shift;
    private decimal _expected;
    private ShiftService.ShiftTotals? _reviewedTotals;
    private bool _saving;
    private readonly List<CountRow> _counts = new();

    public ShiftCloseDialog()
    {
        InitializeComponent();
        // Bound the window before centering; expanded counting scrolls only the body.
        MaxHeight = Math.Min(MaxHeight, Math.Max(160, SystemParameters.WorkArea.Height - 32));
        Height = Math.Min(Height, MaxHeight);
        _counts.AddRange(_service.GetCountingDenominations().Select(value => new CountRow(value, UpdateDenominations)));
        DenominationGrid.ItemsSource = _counts;
        _shift = _service.GetOpenShift();
        if (_shift == null)
        {
            Close();
            return;
        }

        string UserName(Guid? id) => id.HasValue
            ? App.Service.Staff().Where(u => u.Id == id.Value).Select(u => u.FullName).FirstOrDefault() ?? "Unknown user"
            : "Not assigned";
        ShiftIdentityText.Text = $"Opened {_shift.OpenedAt:g}\n{_shift.DrawerMode} drawer · Opened by {UserName(_shift.UserId)}";
        if (_shift.DrawerMode == RetailForma.Core.Entities.DrawerMode.Assigned)
            ShiftIdentityText.Text += $"\nAssigned cashier: {UserName(_shift.AssignedCashierId)}";
        ClosingOperatorText.Text = $"Closing operator: {App.CurrentUser?.FullName}";
        RefreshTotals();
    }

    private void RefreshTotals()
    {
        if (_shift == null) return;
        var totals = _service.GetShiftTotals(_shift.Id);
        _reviewedTotals = totals;
        _expected = _shift.OpeningFloat + totals.CashSales + totals.CashIn - totals.CashOut;
        FloatText.Text = $"₱{_shift.OpeningFloat:N2}";
        CashSalesText.Text = $"₱{totals.CashSales:N2}";
        CashInText.Text = $"+₱{totals.CashIn:N2}";
        CashOutText.Text = $"−₱{totals.CashOut:N2}";
        VoidText.Text = $"Voided cash already accounted for: ₱{totals.VoidedCashSales:N2}";
        ExpectedText.Text = $"₱{_expected:N2}";
        UpdateVariance();
    }

    private void CountedBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        HintAssist.SetHint(CountedBox, CountedBox.Text.Length == 0 ? "Counted cash in drawer" : "");
        ErrorText.Text = "";
        UpdateVariance();
    }

    internal static string DescribeVariance(decimal variance) => variance == 0
        ? "Balanced" : variance < 0 ? $"Short by ₱{-variance:N2}" : $"Over by ₱{variance:N2}";

    private void UpdateVariance()
    {
        var valid = decimal.TryParse(CountedBox.Text, out var value) && value >= 0 && Math.Round(value, 2) == value;
        var variance = Math.Round(value - _expected, 2, MidpointRounding.AwayFromZero);
        VariancePreview.Text = valid ? DescribeVariance(variance) : "Cash not yet counted";
        VariancePreview.Foreground = (System.Windows.Media.Brush)FindResource(
            !valid || variance == 0 ? "UiTextBrush" : variance < 0 ? "UiDangerBrush" : "UiWarningBrush");
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_saving) return;
        DenominationGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        DenominationGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        if (_shift == null || !decimal.TryParse(CountedBox.Text, out var counted) || counted < 0 || Math.Round(counted, 2) != counted)
        {
            ErrorText.Text = "Enter a nonnegative counted amount with at most two decimal places."; ActionDialog.Warning("Enter a nonnegative counted amount with at most two decimal places.", Title, this);
            return;
        }

        _saving = true;
        CloseShiftButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        try
        {
            if (UseDenominationsBox.IsChecked == true && _counts.Any(r => !r.Valid))
            { ErrorText.Text = "Enter whole nonnegative denomination counts."; ActionDialog.Warning("Enter whole nonnegative denomination counts.", Title, this); return; }
            var reviewed = _reviewedTotals;
            RefreshTotals();
            if (reviewed != _reviewedTotals)
            {
                ErrorText.Text = "Shift activity changed. Review the updated totals, then click Close Shift again."; ActionDialog.Warning("Shift activity changed. Review the updated totals, then click Close Shift again.", Title, this);
                return;
            }
            var result = RetailForma.Desktop.Dialogs.ActionDialog.Show(this,
                $"Expected cash: ₱{_expected:N2}\nCounted cash: ₱{counted:N2}\n{DescribeVariance(Math.Round(counted - _expected, 2, MidpointRounding.AwayFromZero))}\n\nClosing operator: {App.CurrentUser?.FullName}\n\nClose this shift?",
                "Confirm shift closing", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes) return;
            _service.CloseShift(App.CurrentUser!.Id, counted, NotesBox.Text,
                UseDenominationsBox.IsChecked == true ? _counts.ToDictionary(r => r.FaceValue, r => r.Count) : null,
                shiftId: _shift.Id, reviewedTotals: _reviewedTotals);
            ActionDialog.Success("Shift closed. Counted cash and variance have been saved.", Title, this);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            RefreshTotals();
            ErrorText.Text = ex.Message; ActionDialog.Error(ex.Message, Title, this);
        }
        finally
        {
            _saving = false;
            CloseShiftButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    private void UseDenominations_Changed(object sender, RoutedEventArgs e) => UpdateDenominations();
    private void UpdateDenominations()
    {
        if (DenominationGrid == null || CountedBox == null) return;
        var enabled = UseDenominationsBox.IsChecked == true;
        DenominationGrid.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        CountedBox.IsReadOnly = enabled;
        if (!enabled) return;
        CountedBox.Text = _counts.All(r => r.Valid) ? _counts.Sum(r => r.Amount).ToString("0.00") : "";
    }
    public sealed class CountRow(decimal faceValue, Action changed) : INotifyPropertyChanged
    {
        private string _count = "0";
        public decimal FaceValue => faceValue;
        public string CountText { get => _count; set { _count = string.IsNullOrWhiteSpace(value) ? "0" : value; PropertyChanged?.Invoke(this, new(nameof(CountText))); PropertyChanged?.Invoke(this, new(nameof(Amount))); changed(); } }
        public bool Valid => string.IsNullOrWhiteSpace(_count) || (int.TryParse(_count, out var value) && value >= 0);
        public int Count => int.TryParse(_count, out var value) && value >= 0 ? value : 0;
        public decimal Amount => FaceValue * Count;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
