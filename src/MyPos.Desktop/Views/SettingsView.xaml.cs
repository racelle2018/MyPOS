using System.Windows;
using System.Windows.Controls;
using MyPos.Core.Entities;

namespace MyPos.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators may change settings.");
        CompanyNameBox.Text = AppSettings.Get("CompanyName", "MY STORE");
        CompanyAddressBox.Text = AppSettings.Get("CompanyAddress");
        CompanyTinBox.Text = AppSettings.Get("CompanyTin");
        FooterMessageBox.Text = AppSettings.Get("FooterMessage", "THANK YOU! PLEASE COME AGAIN.");
        ReceiptsEnabledBox.IsChecked = AppSettings.Get("ReceiptIssuanceEnabled", "false") == "true";
        WidthBox.SelectedIndex = AppSettings.Get("ReceiptWidth", "32") == "48" ? 1 : 0;
        PrinterNameBox.Text = AppSettings.Get("ReceiptPrinterName");
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        var path = BackupService.BackupNow();
        MessageBox.Show(path == null ? "Backup failed — see the log file." : $"Backup saved:\n{path}", "MyPos");
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.Set("CompanyName", CompanyNameBox.Text.Trim().ToUpperInvariant());
        AppSettings.Set("CompanyAddress", CompanyAddressBox.Text.Trim().ToUpperInvariant());
        AppSettings.Set("CompanyTin", CompanyTinBox.Text.Trim().ToUpperInvariant());
        AppSettings.Set("FooterMessage", FooterMessageBox.Text.Trim().ToUpperInvariant());
        AppSettings.Set("ReceiptIssuanceEnabled", ReceiptsEnabledBox.IsChecked == true ? "true" : "false");
        AppSettings.Set("ReceiptWidth", WidthBox.SelectedIndex == 1 ? "48" : "32");
        AppSettings.Set("ReceiptPrinterName", PrinterNameBox.Text.Trim().ToUpperInvariant());
        App.Db.AuditLogs.Add(new AuditLog
        {
            Date = DateTime.Now, UserId = App.CurrentUser?.Id,
            Action = "SettingChanged", EntityName = "Setting",
            Details = "Receipt and store settings updated"
        });
        App.Db.SaveChanges();
        MessageBox.Show("Settings saved.", "MyPos");
    }
}
