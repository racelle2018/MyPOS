using System.Windows;

namespace MyPos.Desktop.Dialogs;

public partial class SaleCompleteDialog : Window
{
    public SaleCompleteDialog(int saleNumber, decimal total, decimal tendered, decimal change)
    {
        InitializeComponent();
        SaleNumberText.Text = $"Sale #{saleNumber} — ₱{total:N2}";
        ChangeText.Text = $"Change ₱{change:N2}";
    }

    private void Done_Click(object sender, RoutedEventArgs e)
        => DialogResult = true;
}
