using System.IO;
using System.Windows;

namespace RetailForma.Desktop.Dialogs;

public partial class RestoreBackupDialog : Window
{
    private readonly Guid actorId;
    private readonly string archivePath;
    private bool modal;
    private bool serviceBusy;
    private readonly bool legacyDatabase;
    public bool Staged { get; private set; }
    internal Action<string, string> VerifyArchive { get; set; } = (path, password) => EncryptedBackup.Verify(path, password);
    internal Action<string, string> StageArchive { get; set; } = (path, password) => BackupService.StageEncryptedRestore(path, password);

    public RestoreBackupDialog(string path)
    {
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators may restore backups.");
        archivePath = Path.GetFullPath(path);
        legacyDatabase = string.Equals(Path.GetExtension(archivePath), ".db", StringComparison.OrdinalIgnoreCase);
        if (!legacyDatabase && !string.Equals(Path.GetExtension(archivePath), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select an encrypted ZIP or legacy database backup.");
        actorId = App.CurrentUser!.Id;
        InitializeComponent(); BackupFileText.Text = Path.GetFileName(archivePath);
        if (legacyDatabase)
        {
            PassphraseLabel.Visibility = BackupPasswordBox.Visibility = Visibility.Collapsed;
            PassphraseHelp.Text = "This is an older unencrypted database backup. The service validates it and converts it to protected storage. Keep the original until the restored company has been verified.";
        }
        Loaded += (_, _) => BackupPasswordBox.Focus();
        Closed += (_, _) => BackupPasswordBox.Clear();
        Closing += (_, e) => { if (serviceBusy) e.Cancel = true; };
    }

    public bool? ShowForOwner(Window? owner)
    {
        if (owner != null) Owner = owner;
        modal = true; return ShowDialog();
    }

    private void RequireSameAdministrator()
    {
        if (App.CurrentUser?.Id != actorId || !Permissions.IsAdmin)
            throw new UnauthorizedAccessException("The session changed. Close and reopen this dialog.");
        _ = new DesktopBackupPreferences().ReadPassphraseState(actorId);
        if (App.Service.TestDatabase == null) return; // Service makes its own encrypted safety snapshot; no owner secret reaches the client.
        // Preserve the pre-restore safety-backup prerequisite; never replace this secret with the archive password.
        _ = EncryptedBackup.UnprotectPassword(AppSettings.Get("BackupRecoverySecret", ""));
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (App.Service.TestDatabase == null) { await RestoreThroughService(); return; }
        RestoreActionButton.IsEnabled = false;
        try
        {
            RequireSameAdministrator();
            if (BackupPasswordBox.Password.Length == 0) throw new ArgumentException("Enter the passphrase for the selected backup.");
            VerifyArchive(archivePath, BackupPasswordBox.Password);
            if (ActionDialog.Show(this,
                "Restore this backup on the next start? Current sales and settings will be replaced. RetailForma will first create a safety backup of the current database.",
                "Confirm database restore", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            RequireSameAdministrator();
            StageArchive(archivePath, BackupPasswordBox.Password);
            Staged = true; BackupPasswordBox.Clear();
            ActionDialog.Success("Backup verified and staged. Close RetailForma normally, then reopen it to finish restoring.", "Restore ready", this);
            if (modal) DialogResult = true; else Close();
        }
        catch (BackupRestoreException ex)
        {
            if (ex.Failure == BackupFailure.PassphraseRejected)
            { ActionDialog.Warning(ex.Message, "Passphrase not accepted", this); BackupPasswordBox.Clear(); BackupPasswordBox.Focus(); }
            else ActionDialog.Error(ex.Message, Title, this);
        }
        catch (FileNotFoundException)
        { ActionDialog.Error("The selected backup file no longer exists. Close this dialog and select it again.", Title, this); }
        catch (DirectoryNotFoundException)
        { ActionDialog.Error("The backup folder is unavailable. Reconnect the drive or make the synced file available locally, then select it again.", Title, this); }
        catch (UnauthorizedAccessException)
        { ActionDialog.Warning("Access was denied. Check that your administrator session is active and that you have permission to read the backup and write the restore file.", Title, this); }
        catch (IOException)
        { ActionDialog.Error("The backup or restore location could not be accessed. Check for a locked file, unavailable drive, or storage problem. No restore was staged.", Title, this); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { ActionDialog.Warning(ex.Message, Title, this); }
        catch (Exception ex)
        {
            Serilog.Log.Error("Backup restore failed ({FailureType}); passphrase values omitted", ex.GetType().Name);
            ActionDialog.Error("An unexpected error prevented restoration. No restore was completed. Contact support with the error log.", Title, this);
        }
        finally { RestoreActionButton.IsEnabled = true; }
    }

    private async Task RestoreThroughService()
    {
        Guid? transfer = null; RestoreActionButton.IsEnabled = false; serviceBusy = true;
        try
        {
            RequireSameAdministrator(); var phrase = BackupPasswordBox.Password;
            if (!legacyDatabase && phrase.Length == 0) throw new ArgumentException("Enter the original passphrase for this backup.");
            transfer = await Task.Run(() => ServiceBackupFiles.Upload(archivePath));
            var preview = await Task.Run(() => App.Service.Call<RetailForma.ServiceApplication.RestorePreview>(RetailForma.ServiceApplication.Operation.PrepareRestore,
                new RetailForma.ServiceApplication.PrepareRestoreInput(transfer.Value, phrase)));
            BackupPasswordBox.Clear();
            if (ActionDialog.Show(this, $"Replace the current company with {preview.Company}?\n\n{preview.Users} accounts · {preview.Sales} sales\n\nA verified encrypted safety snapshot is kept first. All sessions will be signed out. Existing records will be replaced, not merged.",
                "Confirm verified restore", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            RequireSameAdministrator();
            await Task.Run(() => App.Service.Call(RetailForma.ServiceApplication.Operation.CommitRestore, new RetailForma.ServiceApplication.IdInput(transfer.Value)));
            transfer = null; Staged = true;
            serviceBusy = false;
            ActionDialog.Success("Backup restored and verified. Close and reopen RetailForma, sign in to the restored company, and set its backup passphrase for this installation.", "Restore complete", this);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex) { ActionDialog.Warning(ex.Message, "Restore needs attention", this); }
        finally
        {
            if (transfer.HasValue) await Task.Run(() => ServiceBackupFiles.Cancel(transfer.Value));
            BackupPasswordBox.Clear(); RestoreActionButton.IsEnabled = true; serviceBusy = false;
        }
    }
}
