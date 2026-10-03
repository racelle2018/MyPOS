using System.Windows;
using MyPos.Core.Entities;

namespace MyPos.Desktop.Dialogs;

public partial class SaleDetailDialog : Window
{
    public SaleDetailDialog(Sale sale)
    {
        InitializeComponent();
        HeaderText.Text = $"SALE #{sale.SaleNumber}" +
                          (sale.ReceiptNumber == null ? "" : $" — {sale.ReceiptNumber}");
        SubHeaderText.Text = $"{sale.SaleDate:ddd, MMM d, yyyy h:mm:ss tt}" +
                             (sale.CustomerName == null ? " — WALK-IN" : $" — {sale.CustomerName}");
        if (sale.IsVoided)
        {
            VoidedText.Visibility = Visibility.Visible;
            VoidedText.Text = $"VOIDED — {sale.VoidReason}";
        }

        ItemsGrid.ItemsSource = sale.Items.ToList();
        GrossText.Text = $"Gross: ₱{sale.GrossAmount:N2}";
        DiscountText.Text = $"Discount: ₱{sale.DiscountAmount:N2}";
        if (sale.DiscountKind == DiscountKind.SeniorPwd)
            DiscountText.Text += $" (SENIOR/PWD: {sale.SeniorIdNumber})";
        VatText.Text = $"VAT: ₱{sale.VatAmount:N2}";
        NetText.Text = $"Net: ₱{sale.NetAmount:N2}";
        TotalText.Text = $"TOTAL: ₱{sale.TotalAmount:N2}";
        ChangeText.Text = $"Change: ₱{sale.ChangeAmount:N2}";
    }
}
