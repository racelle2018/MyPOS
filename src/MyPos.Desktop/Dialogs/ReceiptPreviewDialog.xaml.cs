using System.Windows;
using MyPos.Core.Entities;
using MyPos.Desktop.Printing;
namespace MyPos.Desktop.Dialogs;
public partial class ReceiptPreviewDialog : Window
{
    private readonly Sale _sale; private readonly bool _reprint;
    public ReceiptPreviewDialog(Sale sale, bool reprint = false, string? warning = null) { InitializeComponent(); _sale = sale; _reprint = reprint; ReceiptText.Text = ReceiptPrinting.BuildPreviewText(sale, reprint); if (warning != null) { WarningText.Text = warning; WarningText.Visibility = Visibility.Visible; } }
    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        if (ReceiptPrinting.TryPrint(_sale, _reprint))
        {
            PrintButton.IsEnabled = false;
            PrintButton.Content = "PRINTED";
        }
        else
        {
            WarningText.Text = "PRINTING FAILED - check that the printer is powered on, connected, and selected in Settings. Retry when ready.";
            WarningText.Visibility = Visibility.Visible;
        }
    }
}
