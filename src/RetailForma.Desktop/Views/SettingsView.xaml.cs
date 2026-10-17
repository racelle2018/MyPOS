using System.Printing;
using System.Windows;
using System.Windows.Controls;
using RetailForma.Core.Entities;
using RetailForma.Desktop.Dialogs;

namespace RetailForma.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        if (!Permissions.IsAdmin)
            throw new UnauthorizedAccessException("Only administrators may change settings.");

        CompanyNameBox.Text = AppSettings.Get("CompanyName", "MY STORE");
        LegalFormBox.ItemsSource = new Dictionary<string, string> { ["Unspecified"] = "Not yet confirmed", ["SoleProprietor"] = "Sole proprietor", ["Corporation"] = "Corporation", ["Partnership"] = "Partnership", ["Other"] = "Other" };
        LegalFormBox.SelectedValue = AppSettings.Get("CompanyLegalForm", "Unspecified");
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
        LoadPrinters();
        foreach (var box in new[] { CompanyNameBox, CompanyAddressBox, CompanyTinBox,
                     BranchNameBox, AccrBox, MinBox, SnBox, LowStockBox })
            box.TextChanged += (_, _) => SavedFeedbackText.Text = "Unsaved changes";
        foreach (var combo in new[] { WidthBox, PrintModeBox, PrinterBox, LegalFormBox })
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
        if (!Permissions.RequireAdmin("change settings")) return;
        if (!int.TryParse(LowStockBox.Text.Trim(), out var threshold) || threshold < 0)
        {
            LowStockErrorText.Text = "Enter a whole number (0 or more).";
            SavedFeedbackText.Text = "Settings were not saved.";
            RetailForma.Desktop.Dialogs.ActionDialog.Warning("Enter a whole number (0 or more) for the low-stock threshold. Settings were not saved.", "Settings", Window.GetWindow(this));
            LowStockBox.BringIntoView();
            LowStockBox.Focus();
            return;
        }
        LowStockErrorText.Text = "";

        var changed = new List<string>();
        var values = new Dictionary<string, string>();
        void Set(string key, string value)
        {
            if (AppSettings.Get(key) != value) changed.Add(key);
            values[key] = value;
        }

        Set("CompanyName", CompanyNameBox.Text.Trim());
        Set("CompanyLegalForm", LegalFormBox.SelectedValue as string ?? "Unspecified");
        Set("CompanyAddress", CompanyAddressBox.Text.Trim());
        Set("CompanyTin", CompanyTinBox.Text.Trim());
        Set("BranchName", BranchNameBox.Text.Trim());
        Set("AccrNo", AccrBox.Text.Trim());
        Set("Min", MinBox.Text.Trim());
        Set("Sn", SnBox.Text.Trim());
        Set("ReceiptIssuanceEnabled", ReceiptsEnabledBox.IsChecked == true ? "true" : "false");
        Set("ReceiptWidth", WidthBox.SelectedIndex == 1 ? "48" : "32");
        Set("ReceiptPrintMode", PrintModeBox.SelectedIndex == 1 ? "Regular" : "Thermal");
        Set("ReceiptPrinterName", PrinterBox.SelectedItem as string ?? "");
        Set("LowStockThreshold", threshold.ToString());

        if (changed.Count > 0)
        {
            App.Service.SaveSettings(values);
        }

        SavedFeedbackText.Text = "";
        if (changed.Count == 0) RetailForma.Desktop.Dialogs.ActionDialog.Information("No changes to save.", "Settings", Window.GetWindow(this));
        else RetailForma.Desktop.Dialogs.ActionDialog.Success("Settings saved. New sales and prints use the updated values.", "Settings", Window.GetWindow(this));
    }

    private void TestReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        var sale = new Sale
        {
            SaleNumber = 0,
            SaleDate = RetailForma.Core.BusinessClock.Now,
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
