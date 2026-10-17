using System.Windows;
using System.Windows.Controls;
using RetailForma.Core.Entities;
using RetailForma.Desktop.Dialogs;

namespace RetailForma.Desktop.Views;

public partial class SetupChecklistView : UserControl
{
    public sealed class ChecklistItem(string key, string title, string guidance)
    {
        public string Key { get; } = key;
        public string Title { get; } = title;
        public string Guidance { get; } = guidance;
        public bool Confirmed { get; set; } = AppSettings.Get("SetupCheck." + key, "false") == "true";
    }
    private readonly ChecklistItem[] items;
    public SetupChecklistView()
    {
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only Owner/System Administrator may review setup.");
        InitializeComponent();
        items = [
            new("Business", "Business identity reviewed", "General Settings: company, optional branch, address, TIN and actual registration details. Do not enable official receipt claims without professional review."),
            new("Users", "Individual accounts and drawer access reviewed", "Users: Owner/Cashier accounts. Drawer Access Settings: Shared or Assigned. Technical accounts use the separate local setup tool; no shared/default technical credentials."),
            new("Printing", "Scanner and physical print samples accepted", "General Settings: saved printer/mode/width. Test the XP-58IIH 58-mm printer, any selected 80-mm printer and Epson L3110 reports. Verify barcode Enter suffix, QR and reprints."),
            new("Inventory", "Opening stock and accounting review arranged", "Products: review quantities/costs. Distinct opening-inventory posting is still pending accountant mappings; do not record opening balances as paid purchases just to finish setup."),
            new("Backup", "Recovery passphrase safely held outside this computer", "Backup & Restore: save a unique passphrase and create a verified encrypted backup. Keep older passphrases for older archives. Windows-protected automation credentials alone are not portable recovery credentials."),
            new("External", "Independent external recovery drill performed", "Configure either/both provider folders in Backup & Restore and sign in through Google Drive/Dropbox desktop apps. Check remote sync there, retrieve an encrypted backup and restore only into an isolated test installation/profile; compare products, sales, users, settings and reports. A folder copy alone does not prove cloud recovery.")
        ];
        ChecklistItems.ItemsSource = items;
        RecoveryText.Text = $"Database: {BackupService.DatabasePath}\nBackups: {BackupService.BackupFolder}\n\n1. Preserve the current database and backups; never restore over the only surviving copy.\n2. On a working installation, save the recovery passphrase for the encrypted safety copy.\n3. In Backup & Restore, select the archive, enter its original passphrase and confirm. Legacy .db backups remain supported.\n4. Close normally and reopen. A verified pre-restore safety copy is created before replacement.\n5. Re-save backup credentials on a different Windows profile, then compare records and reports before returning to service.\n\nIf RetailForma cannot start, ask the System Administrator to recover into an isolated installation first. Do not delete sidecars, run migrations, reset passwords, or edit the database to bypass recovery errors.";
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("save setup review")) return;
        var values = items.ToDictionary(item => "SetupCheck." + item.Key, item => item.Confirmed ? "true" : "false");
        values["SetupChecklistReviewed"] = "true"; App.Service.SaveSettings(values);
        ActionDialog.Success("Review saved. Unchecked items remain pending; you can return here from System Utilities.", "Setup & Recovery", Window.GetWindow(this));
    }
}
