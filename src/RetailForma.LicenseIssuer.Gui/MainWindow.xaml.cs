using System.IO;
using System.Security.Cryptography;
using System.Windows;
using Microsoft.Win32;
using RetailForma.Core.Services;
using RetailForma.LicenseIssuer.Gui.Backend;
using RetailForma.Desktop.Dialogs;

namespace RetailForma.LicenseIssuer.Gui;

public partial class MainWindow : Window
{
    private LicenseRequest? request;
    private bool busy;
    private RetailForma.ServiceApplication.AccountRecoveryRequest? recoveryRequest;
    public MainWindow()
    {
        InitializeComponent(); EntitlementBox.Text = Guid.NewGuid().ToString();
        MaxHeight = Math.Max(400, SystemParameters.WorkArea.Height);
        Height = Math.Min(Height, MaxHeight);
        Closing += (_, args) => { if (busy) args.Cancel = true; };
    }
    private void Request_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "POS device request", Filter = "RetailForma request (*.json)|*.json" };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var loaded = IssuerWorkspace.ReadRequest(picker.FileName);
            request = loaded; RequestPathBox.Text = picker.FileName; CompanyBox.Text = loaded.Company; FingerprintBox.Text = loaded.Fingerprint;
            StatusText.Text = "Device request loaded. Verify the company and fingerprint before issuance.";
        }
        catch (Exception ex) { ReportFailure(ex); }
    }
    private void Key_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Encrypted paid signing key", Filter = "Encrypted private PEM (*.pem)|*.pem" };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            IssuerWorkspace.OutsideClientAndRepository(picker.FileName);
            if (Path.GetFileName(picker.FileName).StartsWith("trial-", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(picker.FileName).StartsWith("recovery-", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose the paid signing key, not a trial or developer recovery key.");
            KeyPathBox.Text = picker.FileName; RefreshHistory();
        }
        catch (Exception ex) { ReportFailure(ex); }
    }
    private void NewId_Click(object sender, RoutedEventArgs e) => EntitlementBox.Text = Guid.NewGuid().ToString();
    private void Replacement_Changed(object sender, RoutedEventArgs e)
    {
        if (NewIdButton == null || EntitlementBox == null) return;
        NewIdButton.IsEnabled = ReplaceBox.IsChecked != true;
        if (ReplaceBox.IsChecked == true) EntitlementBox.Clear();
        else if (string.IsNullOrWhiteSpace(EntitlementBox.Text)) EntitlementBox.Text = Guid.NewGuid().ToString();
    }
    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (request == null || string.IsNullOrWhiteSpace(KeyPathBox.Text) || !Guid.TryParse(EntitlementBox.Text, out var id) || id == Guid.Empty)
        { ActionDialog.Warning("Load a device request, select the encrypted paid key and enter a valid entitlement ID.", Title, this); return; }
        var replace = ReplaceBox.IsChecked == true; var portal = PortalBox.IsChecked == true;
        var confirmedRequest = request; var requestPath = RequestPathBox.Text; var keyPath = KeyPathBox.Text;
        var confirmation = $"Company: {confirmedRequest.Company}\nDevice: {confirmedRequest.Fingerprint}\nEntitlement: {id}\nType: lifetime, no expiry\nOwner portal entitlement: {(portal ? "Yes" : "No")}\n\n" +
            (replace ? "Have you verified ownership and approved replacing the existing device? The old offline token cannot be instantly revoked." : "Have you verified the purchase and device ownership?");
        if (ActionDialog.Show(this, confirmation, replace ? "Approve hardware replacement" : "Issue lifetime license",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var output = new SaveFileDialog { Title = "Save signed lifetime license", Filter = "Signed license (*.json)|*.json", FileName = $"retailforma-{id}.license.json", OverwritePrompt = false };
        if (output.ShowDialog(this) != true) return;
        var passwordDialog = new PassphraseDialog(false) { Owner = this };
        if (passwordDialog.ShowDialog() != true) return;
        var password = passwordDialog.Passphrase; passwordDialog.Forget();
        SetBusy(true, "Signing license and recording issuance…");
        try
        {
            await Task.Run(() => IssuerWorkspace.IssueLifetime(keyPath, requestPath, output.FileName, id, password, portal, replace, confirmedRequest));
            RefreshHistory(); StatusText.Text = "Signed lifetime license saved: " + output.FileName;
            ActionDialog.Success("Signed license and issuer record saved. Deliver only the signed JSON to the client, never your private key.", Title, this);
        }
        catch (Exception ex) { ReportFailure(ex); }
        finally { password = ""; SetBusy(false); }
    }
    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Choose the parent folder for a NEW private key folder" };
        if (picker.ShowDialog(this) == true)
            NewFolderBox.Text = Path.Combine(picker.FolderName, PurposeBox.SelectedIndex switch
                { 0 => "retailforma-paid-keys", 1 => "retailforma-trial-keys", _ => "retailforma-recovery-keys" });
    }
    private async void CreateKeys_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var folder = NewFolderBox.Text.Trim(); var purpose = (SigningPurpose)PurposeBox.SelectedIndex;
        try
        {
            IssuerWorkspace.OutsideClientAndRepository(folder);
            if (Directory.Exists(folder) || File.Exists(folder)) throw new ArgumentException("Choose a new private folder; existing folders are not overwritten.");
        }
        catch (Exception ex) { ReportFailure(ex); return; }
        if (ActionDialog.Show(this, $"Create a new {purpose.ToString().ToLowerInvariant()} key set in:\n{folder}\n\nKeep the encrypted private file and passphrase secure. A new key does not replace trust in previously built clients.",
            "Create key pair", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var passwordDialog = new PassphraseDialog(true) { Owner = this };
        if (passwordDialog.ShowDialog() != true) return;
        var password = passwordDialog.Passphrase; passwordDialog.Forget();
        SetBusy(true, "Creating encrypted RSA key pair…");
        try
        {
            await Task.Run(() => IssuerWorkspace.CreateKeys(folder, password, password, purpose));
            if (purpose == SigningPurpose.Paid) { KeyPathBox.Text = Path.Combine(folder, "paid-private.encrypted.pem"); RefreshHistory(); }
            StatusText.Text = "Encrypted key pair created: " + folder;
            ActionDialog.Success("Encrypted private key and public PEM saved. Back them up securely. Only the public PEM belongs in the POS build.", Title, this);
        }
        catch (Exception ex) { ReportFailure(ex); }
        finally { password = ""; SetBusy(false); }
    }
    private void History_Click(object sender, RoutedEventArgs e)
    {
        try { RefreshHistory(); }
        catch (Exception ex) { ReportFailure(ex); }
    }
    private void RecoveryRequest_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Account recovery request", Filter = "Recovery request (*.json)|*.json" };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var loaded = RecoveryWorkspace.ReadRequest(picker.FileName);
            recoveryRequest = loaded; RecoveryRequestPathBox.Text = picker.FileName;
            RecoverySummaryBox.Text = $"{loaded.Company}\n{loaded.Username}\nInstallation: {loaded.InstallationId}\nAccount: {loaded.AccountId}";
        }
        catch (Exception ex) { ReportFailure(ex); }
    }
    private void RecoveryKey_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Encrypted developer recovery key", Filter = "Encrypted private PEM (*.pem)|*.pem" };
        if (picker.ShowDialog(this) != true) return;
        try { IssuerWorkspace.OutsideClientAndRepository(picker.FileName); RecoveryKeyPathBox.Text = picker.FileName; }
        catch (Exception ex) { ReportFailure(ex); }
    }
    private async void AuthorizeRecovery_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (recoveryRequest == null || string.IsNullOrWhiteSpace(RecoveryKeyPathBox.Text))
        { ActionDialog.Warning("Load an account recovery request and select its private recovery key.", Title, this); return; }
        var preview = recoveryRequest; var requestPath = RecoveryRequestPathBox.Text; var keyPath = RecoveryKeyPathBox.Text;
        if (ActionDialog.Show(this, $"{preview.Company}\nAccount: {preview.Username}\nInstallation: {preview.InstallationId}\n\nHave you verified ownership and approved resetting only this account's password? Roles and disabled access will not change.",
            "Authorize account recovery", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var output = new SaveFileDialog { FileName = "retailforma-account-recovery.json", Filter = "Recovery authorization (*.json)|*.json" };
        if (output.ShowDialog(this) != true) return;
        var passwordDialog = new PassphraseDialog(false) { Owner = this }; if (passwordDialog.ShowDialog() != true) return;
        var password = passwordDialog.Passphrase; passwordDialog.Forget(); SetBusy(true, "Authorizing account recovery…");
        try
        {
            await Task.Run(() => RecoveryWorkspace.Authorize(requestPath, keyPath, password, output.FileName, preview));
            StatusText.Text = "One-use recovery authorization saved: " + output.FileName;
            ActionDialog.Success("Authorization saved. Return it to the technician before the request expires. No customer account was changed by this tool.", Title, this);
        }
        catch (Exception ex) { ReportFailure(ex); }
        finally { password = ""; SetBusy(false); }
    }
    private void RecoveryBackup_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Backup requiring developer recovery", Filter = "Encrypted backup (*.zip)|*.zip" };
        if (picker.ShowDialog(this) != true) return;
        try { _ = RetailForma.EncryptedStorage.RecoveryBackupPackage.ReadHeader(picker.FileName); RecoveryArchivePathBox.Text = picker.FileName; }
        catch (Exception ex) { ReportFailure(ex); }
    }
    private async void RecoverBackup_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var archive = RecoveryArchivePathBox.Text; var privateKey = RecoveryKeyPathBox.Text;
        if (string.IsNullOrWhiteSpace(archive) || string.IsNullOrWhiteSpace(privateKey))
        { ActionDialog.Warning("Select a new-format backup and its private developer recovery key.", Title, this); return; }
        if (ActionDialog.Show(this, $"Backup: {Path.GetFileName(archive)}\n\nHave you verified ownership and approved creating a new backup with a replacement owner passphrase? The original archive will be preserved.",
            "Recover encrypted backup", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var output = new SaveFileDialog { FileName = Path.GetFileNameWithoutExtension(archive) + "-recovered.zip", Filter = "Encrypted backup (*.zip)|*.zip" };
        if (output.ShowDialog(this) != true) return;
        var unlock = new PassphraseDialog(false) { Owner = this, Title = "Unlock private recovery key" }; if (unlock.ShowDialog() != true) return;
        var keyPhrase = unlock.Passphrase; unlock.Forget();
        var owner = new PassphraseDialog(true) { Owner = this, Title = "New owner backup passphrase" };
        if (owner.ShowDialog() != true) { keyPhrase = ""; return; }
        var ownerPhrase = owner.Passphrase; owner.Forget(); SetBusy(true, "Verifying and recovering encrypted backup…");
        try
        {
            await Task.Run(() => RecoveryWorkspace.ReprotectBackup(archive, output.FileName, privateKey, keyPhrase, ownerPhrase));
            StatusText.Text = "Verified recovered backup saved: " + output.FileName;
            ActionDialog.Success("A new encrypted backup was saved and verified. The original archive is unchanged. Keep the replacement owner passphrase safe.", Title, this);
        }
        catch (Exception ex) { ReportFailure(ex); }
        finally { keyPhrase = ownerPhrase = ""; SetBusy(false); }
    }
    private void RefreshHistory()
    {
        if (string.IsNullOrWhiteSpace(KeyPathBox.Text))
        { HistoryGrid.ItemsSource = null; HistoryHint.Text = "Select a paid signing key on Generate license first."; return; }
        var entries = IssuerWorkspace.History(KeyPathBox.Text);
        HistoryGrid.ItemsSource = entries.Reverse().Select(entry => new HistoryRow(entry.IssuedUtc.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd h:mm tt"),
            entry.Company, entry.RetiredFingerprint == null ? "Issued" : "Replacement", entry.EntitlementId)).ToList();
        HistoryHint.Text = $"{entries.Count} issuance record(s). Philippine time; history is read-only.";
    }
    private void SetBusy(bool value, string? message = null)
    {
        busy = value; Pages.IsEnabled = !value;
        if (message != null) StatusText.Text = message;
    }
    private void ReportFailure(Exception ex)
    {
        StatusText.Text = "Operation not completed. Check the selected paths and issuer records before retrying.";
        if (ex is ArgumentException or InvalidOperationException or CryptographicException)
            ActionDialog.Warning(ex is CryptographicException ? "The encrypted private key could not be opened. Check the key file and passphrase." : ex.Message, Title, this);
        else ActionDialog.Error(ex.Message + "\nIf an output file was created, reconcile it with the issuer ledger before retrying.", Title, this);
    }
    private sealed record HistoryRow(string Issued, string Company, string Action, Guid EntitlementId);
}
