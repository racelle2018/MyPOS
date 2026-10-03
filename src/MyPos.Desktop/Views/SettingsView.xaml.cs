using System.Printing;
using System.Windows;
using System.Windows.Controls;
using MyPos.Core.Entities;

namespace MyPos.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        if (!Permissions.IsAdmin)
            throw new UnauthorizedAccessException("Only administrators may change settings.");

        CompanyNameBox.Text = AppSettings.Get("CompanyName", "MY STORE");
        CompanyAddressBox.Text = AppSettings.Get("CompanyAddress", "");
        CompanyTinBox.Text = AppSettings.Get("CompanyTin", "");
        ReceiptsEnabledBox.IsChecked = AppSettings.Get("ReceiptIssuanceEnabled", "false") == "true";
        WidthBox.SelectedIndex = AppSettings.Get("ReceiptWidth", "32") == "48" ? 1 : 0;
        LowStockBox.Text = AppSettings.Get("LowStockThreshold", "5");
        LoadPrinters();
    }

    private void LoadPrinters()
    {
        var names = new List<string> { "" };
        try
        {
            names.AddRange(new LocalPrintServer().GetPrintQueues()
                .Select(queue => queue.FullName).OrderBy(name => name));
        }
        catch
        {
            // Keep the blank Windows-default option when no print server is available.
        }

        PrinterBox.ItemsSource = names;
        var current = AppSettings.Get("ReceiptPrinterName", "");
        PrinterBox.SelectedItem = names.Contains(current) ? current : "";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(LowStockBox.Text.Trim(), out var threshold) || threshold < 0)
        {
            MessageBox.Show("Low-stock threshold must be a whole number (0 or more).", "MyPos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            LowStockBox.Focus();
            return;
        }

        var changed = new List<string>();
        void Set(string key, string value)
        {
            if (AppSettings.Get(key) != value) changed.Add(key);
            AppSettings.Set(key, value);
        }

        Set("CompanyName", CompanyNameBox.Text.Trim());
        Set("CompanyAddress", CompanyAddressBox.Text.Trim());
        Set("CompanyTin", CompanyTinBox.Text.Trim());
        Set("ReceiptIssuanceEnabled", ReceiptsEnabledBox.IsChecked == true ? "true" : "false");
        Set("ReceiptWidth", WidthBox.SelectedIndex == 1 ? "48" : "32");
        Set("ReceiptPrinterName", PrinterBox.SelectedItem as string ?? "");
        Set("LowStockThreshold", threshold.ToString());

        if (changed.Count > 0)
        {
            App.Db.AuditLogs.Add(new AuditLog
            {
                Date = DateTime.Now, UserId = App.CurrentUser?.Id,
                Action = "SettingChanged", EntityName = "Setting",
                Details = string.Join(", ", changed)
            });
            App.Db.SaveChanges();
        }

        MessageBox.Show("Settings saved.", "MyPos", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        var path = BackupService.BackupNow();
        MessageBox.Show(path != null ? $"Backup saved:\n{path}" : "Backup failed — see the log file.",
            "MyPos", MessageBoxButton.OK,
            path != null ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
