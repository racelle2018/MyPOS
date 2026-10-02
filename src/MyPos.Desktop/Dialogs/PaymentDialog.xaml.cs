using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyPos.Core.Entities;

namespace MyPos.Desktop.Dialogs;

public partial class PaymentDialog : Window
{
    private readonly decimal _total;
    private readonly bool _isCash;

    public decimal Tendered { get; private set; }
    public string? Reference { get; private set; }

    public PaymentDialog(decimal total, PaymentMethod method)
    {
        InitializeComponent();
        _total = total;
        _isCash = method == PaymentMethod.Cash;
        TotalText.Text = $"₱{total:N2}";
        TenderedBox.Text = total.ToString("0.##");

        if (_isCash)
        {
            Title = "Cash Payment";
            TenderedBox.Focus();
            TenderedBox.SelectAll();
        }
        else
        {
            Title = $"{method} Payment";
            TenderedLabel.Text = "Amount";
            TenderedBox.IsReadOnly = true;
            QuickWrap.Visibility = Visibility.Collapsed;
            ChangePanel.Visibility = Visibility.Collapsed;
            ReferencePanel.Visibility = Visibility.Visible;
            ReferenceBox.Focus();
        }
    }

    private void TenderedBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ErrorText.Text = "";
        if (!decimal.TryParse(TenderedBox.Text.Trim(), out var tendered))
        {
            ChangeText.Text = "";
            return;
        }

        var change = tendered - _total;
        ChangeText.Text = change >= 0
            ? $"₱{change:N2}"
            : $"₱{-change:N2} short";
        ChangeText.Foreground = new SolidColorBrush(change >= 0
            ? Color.FromRgb(0x16, 0xA3, 0x4A)
            : Color.FromRgb(0xDC, 0x26, 0x26));
    }

    private void QuickAmount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && decimal.TryParse(tag, out var amount))
            TenderedBox.Text = amount.ToString("0.##");
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (_isCash)
        {
            if (!decimal.TryParse(TenderedBox.Text.Trim(), out var tendered))
            {
                ErrorText.Text = "Enter the cash amount received.";
                return;
            }
            if (tendered < _total)
            {
                ErrorText.Text = "Cash tendered is less than the amount due.";
                return;
            }
            Tendered = tendered;
        }
        else
        {
            Tendered = _total;
        }

        Reference = string.IsNullOrWhiteSpace(ReferenceBox.Text)
            ? null
            : ReferenceBox.Text.Trim();
        DialogResult = true;
    }
}
