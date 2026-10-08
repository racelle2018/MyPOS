using System.Printing;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MyPos.Core.Entities;
using MyPos.Desktop.Dialogs;

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
        BranchNameBox.Text = AppSettings.Get("BranchName", "");
        AccrBox.Text = AppSettings.Get("AccrNo", "");
        MinBox.Text = AppSettings.Get("Min", "");
        SnBox.Text = AppSettings.Get("Sn", "");
        ReceiptsEnabledBox.IsChecked = AppSettings.Get("ReceiptIssuanceEnabled", "false") == "true";
        WidthBox.SelectedIndex = AppSettings.Get("ReceiptWidth", "32") == "48" ? 1 : 0;
        PrintModeBox.SelectedIndex = AppSettings.Get("ReceiptPrintMode", "Thermal") == "Regular" ? 1 : 0;
        LowStockBox.Text = AppSettings.Get("LowStockThreshold", "5");
        BackupCopyFolderBox.Text = AppSettings.Get("BackupCopyFolder", "");
        RefreshBackupStatus();
        LoadPrinters();
        foreach (var box in new[] { CompanyNameBox, CompanyAddressBox, CompanyTinBox,
                     BranchNameBox, AccrBox, MinBox, SnBox, LowStockBox, BackupCopyFolderBox })
            box.TextChanged += (_, _) => SavedFeedbackText.Text = "Unsaved changes";
        foreach (var combo in new[] { WidthBox, PrintModeBox, PrinterBox })
            combo.SelectionChanged += (_, _) => SavedFeedbackText.Text = "Unsaved changes";
        ReceiptsEnabledBox.Checked += (_, _) => SavedFeedbackText.Text = "Unsaved changes";
        ReceiptsEnabledBox.Unchecked += (_, _) => SavedFeedbackText.Text = "Unsaved changes";
        LowStockBox.TextChanged += (_, _) => LowStockErrorText.Text = "";
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

        var current = AppSettings.Get("ReceiptPrinterName", "");
        if (current.Length > 0 && !names.Contains(current))
        {
            names.Add(current);
            PrinterWarningText.Text = $"Saved printer '{current}' is not available on this computer.";
            PrinterWarningText.Visibility = Visibility.Visible;
        }
        PrinterBox.ItemsSource = names;
        PrinterBox.SelectedItem = current;
    }

    private void SettingsSections_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var stacked = e.NewSize.Width < 900;
        SettingsSections.ColumnDefinitions[1].Width = stacked
            ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetRow(RightSettings, stacked ? 1 : 0);
        Grid.SetColumn(RightSettings, stacked ? 0 : 1);
        LeftSettings.Margin = stacked ? new Thickness(0) : new Thickness(0, 0, 6, 0);
        RightSettings.Margin = stacked ? new Thickness(0) : new Thickness(6, 0, 0, 0);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(LowStockBox.Text.Trim(), out var threshold) || threshold < 0)
        {
            LowStockErrorText.Text = "Enter a whole number (0 or more).";
            SavedFeedbackText.Text = "Settings were not saved.";
            LowStockBox.BringIntoView();
            LowStockBox.Focus();
            return;
        }
        LowStockErrorText.Text = "";

        var changed = new List<string>();
        void Set(string key, string value)
        {
            if (AppSettings.Get(key) != value) changed.Add(key);
            AppSettings.Set(key, value);
        }

        Set("CompanyName", CompanyNameBox.Text.Trim());
        Set("CompanyAddress", CompanyAddressBox.Text.Trim());
        Set("CompanyTin", CompanyTinBox.Text.Trim());
        Set("BranchName", BranchNameBox.Text.Trim().ToUpperInvariant());
        Set("AccrNo", AccrBox.Text.Trim().ToUpperInvariant());
        Set("Min", MinBox.Text.Trim().ToUpperInvariant());
        Set("Sn", SnBox.Text.Trim().ToUpperInvariant());
        Set("ReceiptIssuanceEnabled", ReceiptsEnabledBox.IsChecked == true ? "true" : "false");
        Set("ReceiptWidth", WidthBox.SelectedIndex == 1 ? "48" : "32");
        Set("ReceiptPrintMode", PrintModeBox.SelectedIndex == 1 ? "Regular" : "Thermal");
        Set("ReceiptPrinterName", PrinterBox.SelectedItem as string ?? "");
        Set("LowStockThreshold", threshold.ToString());
        Set("BackupCopyFolder", BackupCopyFolderBox.Text.Trim());

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

        SavedFeedbackText.Text = changed.Count == 0
            ? "No changes to save."
            : "Settings saved. New sales and prints use the updated values.";
    }

    private async void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        BackupButton.IsEnabled = false;
        var path = await Task.Run(BackupService.BackupNow);
        BackupButton.IsEnabled = true;
        RefreshBackupStatus();
        MessageBox.Show(path != null ? $"Verified backup saved:\n{path}" : $"Backup failed: {BackupService.LastError}",
            "MyPos", MessageBoxButton.OK,
            path != null ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Choose a MyPos backup to restore",
            Filter = "MyPos database backup (*.db)|*.db",
            InitialDirectory = BackupService.BackupFolder
        };
        if (picker.ShowDialog() != true) return;
        if (MessageBox.Show(
                "Restore this backup on the next start? Current sales and settings will be replaced. " +
                "MyPos will first create a safety backup of the current database.",
                "Confirm database restore", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;

        try
        {
            BackupService.StageRestore(picker.FileName);
            MessageBox.Show("Backup verified and staged. Close MyPos normally, then reopen it to finish restoring.",
                "Restore ready", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Restore could not be staged", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshBackupStatus()
    {
        var latest = BackupService.LastVerifiedBackupAt;
        BackupStatusText.Text = latest.HasValue
            ? $"Last verified backup: {latest.Value:g} · {BackupService.BackupFolder}"
            : "No verified backup found yet.";
        if (BackupService.LastError is { Length: > 0 } error)
            BackupStatusText.Text += $"\nLast backup issue: {error}";
    }

    private void TestReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        var sale = new Sale
        {
            SaleNumber = 0,
            SaleDate = DateTime.Now,
            GrossAmount = 112m,
            TotalAmount = 112m,
            NetAmount = 100m,
            VatAmount = 12m,
            VatRate = 0.12m,
            TenderedAmount = 120m,
            ChangeAmount = 8m,
            Items = new List<SaleItem>
            {
                new() { ProductName = "Sample item", Qty = 1m, UnitPrice = 112m, LineGross = 112m }
            },
            Payments = new List<Payment> { new() { Method = PaymentMethod.Cash, Amount = 112m } }
        };
        new ReceiptPreviewDialog(sale) { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
