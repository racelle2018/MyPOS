using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using RetailForma.Desktop.Dialogs;
using RetailForma.LocalTransport;
using RetailForma.ServiceApplication;

namespace RetailForma.Setup;

public partial class MainWindow : Window
{
    private readonly LocalApplicationClient service = new();
    private ServiceStatusInfo? preview;
    private AccountRecoveryRequest? recoveryRequest;
    private bool busy;
    public MainWindow()
    {
        InitializeComponent(); VersionText.Text = typeof(MainWindow).Assembly.GetName().Version?.ToString(3);
        Loaded += (_, _) => MaxHeight = Math.Max(280, SystemParameters.WorkArea.Height - 32);
        Closing += (_, e) => { if (busy) e.Cancel = true; else ClearPasswords(); };
        Closed += (_, _) => { ClearPasswords(); service.Dispose(); };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape && !busy) { e.Handled = true; Close(); } };
    }
    private void ClearPasswords()
    { PasswordBox.Clear(); ConfirmPasswordBox.Clear(); RecoveryPasswordBox.Clear(); RecoveryConfirmBox.Clear(); ImportPasswordBox.Clear(); }
    private void Mode_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CreateButton == null || ResetButton == null || ImportButton == null || ModeTabs == null) return;
        CreateButton.Visibility = ModeTabs.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResetButton.Visibility = ModeTabs.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        ImportButton.Visibility = ModeTabs.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        CreateButton.IsDefault = ModeTabs.SelectedIndex == 0; ResetButton.IsDefault = ModeTabs.SelectedIndex == 1;
        ImportButton.IsDefault = ModeTabs.SelectedIndex == 2;
    }
    private void SetBusy(bool value)
    { busy = value; ModeTabs.IsEnabled = BrowseButton.IsEnabled = CreateButton.IsEnabled = ResetButton.IsEnabled = ImportButton.IsEnabled = !value; }
    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        preview = null; TargetConfirmedBox.IsChecked = false; DatabasePathBox.Text = ""; SetBusy(true);
        try
        {
            preview = await Task.Run(() => service.Call<ServiceStatusInfo>(Operation.ServiceStatus));
            DatabasePathBox.Text = preview.DatabasePath;
            NewCompanyPanel.Visibility = preview.Configured ? Visibility.Collapsed : Visibility.Visible;
            DatabaseStatusText.Text = !preview.Configured ? "No company configured. Initial setup creates a new protected company; it does not import existing data." :
                preview.Company + (preview.TechnicalAccountExists ? " — System Administrator already exists" : " — ready for initial technical account");
        }
        catch (Exception ex) { ActionDialog.Warning(ex.Message, Title, this); DatabaseStatusText.Text = "Service not verified. No database was opened or created by this tool."; }
        finally { SetBusy(false); CreateButton.IsEnabled = preview != null && !preview.TechnicalAccountExists; }
    }
    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (preview == null || TargetConfirmedBox.IsChecked != true)
        { ActionDialog.Warning("Connect and verify the intended company first.", Title, this); return; }
        if (preview.TechnicalAccountExists) { ActionDialog.Warning("A technical account already exists. Use authorized account recovery instead.", Title, this); return; }
        if (PasswordBox.Password != ConfirmPasswordBox.Password || PasswordBox.Password.Length is < 16 or > 256)
        { ActionDialog.Warning("Enter matching passwords of 16–256 characters.", Title, this); return; }
        var expected = preview;
        var input = new TechnicalProvisionInput(UsernameBox.Text, FullNameBox.Text, PasswordBox.Password, ConfirmPasswordBox.Password, CompanyBox.Text);
        if (!expected.Configured && string.IsNullOrWhiteSpace(input.Company)) { ActionDialog.Warning("Enter the new company name.", Title, this); return; }
        if (ActionDialog.Show(this, $"{(expected.Configured ? "Create the first System Administrator" : "Create a new protected company and System Administrator")} for {(!expected.Configured ? input.Company : expected.Company)}?",
            "Confirm initial setup", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        SetBusy(true);
        try
        {
            preview = await Task.Run(() =>
            {
                var current = service.Call<ServiceStatusInfo>(Operation.ServiceStatus);
                if (current != expected) throw new InvalidOperationException("The company changed. Connect and review it again.");
                return service.Call<ServiceStatusInfo>(expected.Configured ? Operation.ProvisionTechnicalAccount : Operation.ProvisionNewCompany, input);
            });
            DatabaseStatusText.Text = preview.Company + " — System Administrator created. Sign in and create the individual Owner account.";
            NewCompanyPanel.Visibility = Visibility.Collapsed;
            ActionDialog.Success("System Administrator created and audited.", Title, this);
        }
        catch (Exception ex) { ActionDialog.Warning(ex.Message, Title, this); }
        finally { ClearPasswords(); SetBusy(false); CreateButton.IsEnabled = preview != null && !preview.TechnicalAccountExists; }
    }
    private async void ExportRecovery_Click(object sender, RoutedEventArgs e)
    {
        var picker = new SaveFileDialog { Title = "Save account recovery request", Filter = "Recovery request (*.json)|*.json", FileName = "retailforma-account-recovery-request.json" };
        if (picker.ShowDialog(this) != true) return;
        var username = RecoveryUsernameBox.Text; SetBusy(true);
        try
        {
            recoveryRequest = await Task.Run(() => service.Call<AccountRecoveryRequest>(Operation.IssueAccountRecovery, new UsernameInput(username)));
            using var file = new FileStream(picker.FileName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(file, recoveryRequest, WireJson.Options); file.Flush(true);
            ActionDialog.Success($"Recovery request saved for {recoveryRequest.Username} at {recoveryRequest.Company}.", Title, this);
        }
        catch (Exception ex) { ActionDialog.Warning(ex.Message, Title, this); }
        finally { SetBusy(false); }
    }
    private void BrowseAuthorization_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Select programmer-signed authorization", Filter = "Recovery authorization (*.json)|*.json", CheckFileExists = true };
        if (picker.ShowDialog(this) == true) AuthorizationPathBox.Text = picker.FileName;
    }
    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (recoveryRequest == null) { ActionDialog.Warning("Export a recovery request on this connection first.", Title, this); return; }
        if (RecoveryPasswordBox.Password.Length is < 16 or > 256 || RecoveryPasswordBox.Password != RecoveryConfirmBox.Password)
        { ActionDialog.Warning("Enter matching temporary passwords of 16–256 characters.", Title, this); return; }
        AccountRecoveryResponse signed;
        try
        {
            var file = new FileInfo(AuthorizationPathBox.Text);
            if (!file.Exists || file.Length is < 1 or > 32768) throw new ArgumentException("Select a bounded signed recovery authorization file.");
            signed = JsonSerializer.Deserialize<AccountRecoveryResponse>(File.ReadAllBytes(file.FullName), WireJson.Options) ?? throw new ArgumentException("The authorization is empty.");
            if (signed.Format != 1 || signed.Challenge != recoveryRequest.Challenge) throw new ArgumentException("The authorization does not match this request.");
        }
        catch (Exception ex) { ActionDialog.Warning(ex.Message, Title, this); return; }
        if (ActionDialog.Show(this, $"Reset the password for {recoveryRequest.Username} at {recoveryRequest.Company}?",
            "Confirm account recovery", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var input = new AccountRecoveryInput(signed, RecoveryPasswordBox.Password, RecoveryConfirmBox.Password); SetBusy(true);
        try
        {
            await Task.Run(() => service.Call(Operation.CompleteAccountRecovery, input)); recoveryRequest = null;
            ActionDialog.Success("Password reset and audited after a verified encrypted safety snapshot. The user must choose a new password at sign-in.", Title, this);
        }
        catch (Exception ex) { ActionDialog.Warning(ex.Message, Title, this); }
        finally { ClearPasswords(); SetBusy(false); }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
