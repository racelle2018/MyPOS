using System.Windows;
using System.Windows.Media;
using MyPos.Core.Entities;
namespace MyPos.Desktop.Dialogs;
public partial class SaleCompleteDialog : Window
{
    private readonly Sale _sale;
    public SaleCompleteDialog(Sale sale, bool receiptsExpected, bool printed)
    {
        InitializeComponent(); _sale = sale;
        SaleNumberText.Text = $"SALE #{sale.SaleNumber} - ₱{sale.TotalAmount:N2}" + (sale.ReceiptNumber == null ? "" : $" - {sale.ReceiptNumber}");
        ChangeLabel.Text = sale.ChangeAmount > 0 ? "CHANGE" : "FULLY PAID";
        ChangeText.Text = sale.ChangeAmount > 0 ? $"₱{sale.ChangeAmount:N2}" : $"₱{sale.TotalAmount:N2}";
        if (!receiptsExpected) PrintStatusText.Text = "MANUAL RECEIPT MODE - WRITE THE OR BY HAND";
        else if (printed) { PrintStatusText.Text = "RECEIPT PRINTED"; PrintStatusText.Foreground = Brushes.Green; }
        else { PrintStatusText.Text = "RECEIPT NOT PRINTED - PRESS VIEW RECEIPT TO SEE AND RETRY"; PrintStatusText.Foreground = Brushes.Red; }
    }
    private void ReceiptButton_Click(object sender, RoutedEventArgs e) => new ReceiptPreviewDialog(_sale).ShowDialog();
    private void Done_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
