using System.Globalization;
using System.Windows;

namespace MyPos.Desktop.Dialogs;

public partial class QuantityEditDialog : Window
{
    private readonly decimal _stockAvailable;

    public decimal Quantity { get; private set; }

    public QuantityEditDialog(string productName, decimal currentQuantity, decimal stockAvailable)
    {
        InitializeComponent();
        _stockAvailable = stockAvailable;
        ProductText.Text = productName;
        StockText.Text = $"Available stock: {stockAvailable:0.##}";
        QuantityBox.Text = currentQuantity.ToString("0.############################", CultureInfo.CurrentCulture);
        Loaded += (_, _) =>
        {
            QuantityBox.Focus();
            QuantityBox.SelectAll();
        };
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryValidateQuantity(QuantityBox.Text, _stockAvailable,
                out var quantity, out var error))
        {
            ErrorText.Text = error;
            QuantityBox.Focus();
            return;
        }

        Quantity = quantity;
        DialogResult = true;
    }

    public static bool TryValidateQuantity(string text, decimal stockAvailable,
        out decimal quantity, out string error)
    {
        if (!decimal.TryParse(text.Trim(), NumberStyles.Number,
                CultureInfo.CurrentCulture, out quantity) || quantity <= 0)
        {
            quantity = 0;
            error = "Enter a quantity greater than zero.";
            return false;
        }

        if (quantity > stockAvailable)
        {
            quantity = 0;
            error = $"Only {stockAvailable:0.##} item(s) are available.";
            return false;
        }

        error = "";
        return true;
    }
}
