using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Entities;
namespace RetailForma.Desktop.Views;

public partial class BackupRestoreView : UserControl
{
    public BackupRestoreView()
    {
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators may manage backups.");
        InitializeComponent();
        BackupCopyFolderBox.Text = AppSettings.Get("BackupCopyFolder", "");
        GoogleDriveEnabledBox.IsChecked = AppSettings.Get("Backup.GoogleDrive.Enabled", "false") == "true";
        DropboxEnabledBox.IsChecked = AppSettings.Get("Backup.Dropbox.Enabled", "false") == "true";
        GoogleDriveFolderBox.Text = AppSettings.Get("Backup.GoogleDrive.Folder", "");
        DropboxFolderBox.Text = AppSettings.Get("Backup.Dropbox.Folder", "");
        RefreshBackupStatus();
        BackupCopyFolderBox.TextChanged += (_, _) => SavedFeedbackText.Text = "Unsaved changes";
        foreach (var box in new[] { GoogleDriveFolderBox, DropboxFolderBox })
            box.TextChanged += (_, _) => { SavedFeedbackText.Text = "Unsaved changes"; RefreshCopyStatus(); };
        foreach (var box in new[] { GoogleDriveEnabledBox, DropboxEnabledBox })
        {
            box.Checked += (_, _) => { SavedFeedbackText.Text = "Unsaved changes"; RefreshCopyStatus(); };
            box.Unchecked += (_, _) => { SavedFeedbackText.Text = "Unsaved changes"; RefreshCopyStatus(); };
        }
    }

    private void Passphrase_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("manage the recovery passphrase")) return;
        try
        {
            var dialog = new Dialogs.BackupPassphraseDialog();
            dialog.ShowForOwner(Window.GetWindow(this));
            RefreshBackupStatus();
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or InvalidOperationException)
        { Dialogs.ActionDialog.Warning(ex.Message, "Recovery passphrase", Window.GetWindow(this)); }
        catch (Exception ex)
        { Serilog.Log.Error("Recovery passphrase dialog failed ({FailureType}); values omitted", ex.GetType().Name); Dialogs.ActionDialog.Error("The recovery passphrase dialog could not be opened.", "Recovery passphrase", Window.GetWindow(this)); }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("choose backup folders")) return;
        var picker = new OpenFolderDialog { Title = "Choose a dedicated locally available backup folder" };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        if (sender is Button { Tag: "GoogleDrive" }) GoogleDriveFolderBox.Text = picker.FolderName;
        else DropboxFolderBox.Text = picker.FolderName;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("change backup preferences")) return;
        var folder = BackupCopyFolderBox.Text.Trim();
        var settings = new Dictionary<string, string>
        {
            ["BackupCopyFolder"] = folder,
            ["Backup.GoogleDrive.Enabled"] = GoogleDriveEnabledBox.IsChecked == true ? "true" : "false",
            ["Backup.Dropbox.Enabled"] = DropboxEnabledBox.IsChecked == true ? "true" : "false",
            ["Backup.GoogleDrive.Folder"] = GoogleDriveFolderBox.Text.Trim(),
            ["Backup.Dropbox.Folder"] = DropboxFolderBox.Text.Trim()
        };
        var destinations = new List<BackupCopies.Destination>();
        if (folder.Length > 0) destinations.Add(new("Additional copy", folder));
        if (GoogleDriveEnabledBox.IsChecked == true) destinations.Add(new("Google Drive", settings["Backup.GoogleDrive.Folder"]));
        if (DropboxEnabledBox.IsChecked == true) destinations.Add(new("Dropbox", settings["Backup.Dropbox.Folder"]));
        try { BackupCopies.ValidateFolders(destinations, BackupService.BackupFolder); }
        catch (Exception ex)
        { RetailForma.Desktop.Dialogs.ActionDialog.Warning(ex.Message, "Backup preferences", Window.GetWindow(this)); return; }
        if (folder.Length > 0 && !System.IO.Path.IsPathFullyQualified(folder))
        {
            RetailForma.Desktop.Dialogs.ActionDialog.Show("Enter a full folder path, or leave it blank for local backups only.",
                "Backup preferences", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        BackupPreferencesService.Result result;
        try
        {
            result = new DesktopBackupPreferences().Save(App.CurrentUser!.Id, settings, "", "", "");
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or InvalidOperationException)
        { RetailForma.Desktop.Dialogs.ActionDialog.Warning(ex.Message, "Backup preferences", Window.GetWindow(this)); return; }
        catch (Exception ex)
        {
            Serilog.Log.Error("Backup preferences persistence failed ({FailureType}); values omitted", ex.GetType().Name);
            RetailForma.Desktop.Dialogs.ActionDialog.Error("Backup preferences could not be saved. No passphrase or preference changes were committed.", "Backup preferences", Window.GetWindow(this)); return;
        }
        if (!result.Changed)
        { SavedFeedbackText.Text = ""; RetailForma.Desktop.Dialogs.ActionDialog.Information("No changes to save.", "Backup preferences", Window.GetWindow(this)); return; }
        SavedFeedbackText.Text = "";
        RefreshBackupStatus();
        RetailForma.Desktop.Dialogs.ActionDialog.Success(result.PassphraseChanged
            ? "Backup preferences saved. Future backups use the saved passphrase; existing ZIP backups still need their original passphrase."
            : "Backup preferences saved.", "Backup preferences", Window.GetWindow(this));
    }

    private async void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("create backups")) return;
        BackupButton.IsEnabled = RestoreButton.IsEnabled = false;
        try
        {
            var path = await Task.Run(BackupService.BackupNow);
            RefreshBackupStatus();
            var issue = BackupService.LastError;
            if (path == null) RetailForma.Desktop.Dialogs.ActionDialog.Error($"Backup failed: {issue}", "Backup", Window.GetWindow(this));
            else if (!string.IsNullOrWhiteSpace(issue)) RetailForma.Desktop.Dialogs.ActionDialog.Warning($"Local backup verified:\n{path}\n\n{issue}", "Backup", Window.GetWindow(this));
            else RetailForma.Desktop.Dialogs.ActionDialog.Success($"Verified backup saved:\n{path}", "Backup", Window.GetWindow(this));
        }
        catch (Exception ex)
        {
            RetailForma.Desktop.Dialogs.ActionDialog.Error(ex.Message, "Backup failed", Window.GetWindow(this));
        }
        finally { BackupButton.IsEnabled = RestoreButton.IsEnabled = true; }
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("restore backups")) return;
        try { _ = new DesktopBackupPreferences().ReadPassphraseState(App.CurrentUser!.Id); }
        catch (Exception ex)
        { RetailForma.Desktop.Dialogs.ActionDialog.Warning(ex.Message, "Restore", Window.GetWindow(this)); return; }
        var picker = new OpenFileDialog
        {
            Title = "Choose a RetailForma backup to restore",
            Filter = "RetailForma encrypted backup (*.zip)|*.zip|Legacy database backup (*.db)|*.db",
            InitialDirectory = BackupService.BackupFolder
        };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        if (App.Service.TestDatabase == null || string.Equals(System.IO.Path.GetExtension(picker.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            try { new Dialogs.RestoreBackupDialog(picker.FileName).ShowForOwner(Window.GetWindow(this)); }
            catch (Exception ex)
            {
                Serilog.Log.Error("Backup restore dialog failed ({FailureType}); values omitted", ex.GetType().Name);
                Dialogs.ActionDialog.Error("The restore dialog could not be opened. No restore was staged.", "Restore", Window.GetWindow(this));
            }
            return;
        }
        if (RetailForma.Desktop.Dialogs.ActionDialog.Show(
                "Restore this backup on the next start? Current sales and settings will be replaced. " +
                "RetailForma will first create a safety backup of the current database.",
                "Confirm database restore", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;

        try
        {
            BackupService.StageRestore(picker.FileName);
            RetailForma.Desktop.Dialogs.ActionDialog.Success("Backup verified and staged. Close RetailForma normally, then reopen it to finish restoring.", "Restore ready", Window.GetWindow(this));
        }
        catch (Exception ex)
        {
            RetailForma.Desktop.Dialogs.ActionDialog.Show(ex.Message, "Restore could not be staged", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshBackupStatus()
    {
        var configured = new DesktopBackupPreferences().ReadPassphraseState(App.CurrentUser!.Id).Configured;
        PassphraseActionButton.Content = configured ? "Change passphrase" : "Set passphrase";
        PassphraseStatusText.Text = configured ? "A recovery passphrase is configured." : "No recovery passphrase is set.";
        var latest = BackupService.LastVerifiedBackupAt;
        BackupStatusText.Text = latest.HasValue
            ? $"Last verified backup: {latest.Value:g} · {BackupService.BackupFolder}"
            : "No verified backup found yet.";
        if (BackupService.LastError is { Length: > 0 } error)
            BackupStatusText.Text += $"\nLast backup issue: {error}";
        if (!configured)
            BackupStatusText.Text += "\nAction required: save a recovery passphrase to enable new backups.";
        RefreshCopyStatus();
    }

    private void RefreshCopyStatus()
    {
        void Show(string name, CheckBox enabled, TextBox folder, TextBlock text)
        {
            var result = BackupService.LastCopyResults.FirstOrDefault(result => result.Name == name &&
                string.Equals(result.Folder.Trim(), folder.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            text.Text = enabled.IsChecked != true ? "Disabled" : result == null
                ? "No folder-copy result this session. Cloud sync is managed by the provider's app."
                : result.Succeeded ? "Folder copy verified; cloud synchronization not verified by RetailForma."
                : "Folder copy failed: " + result.Message;
        }
        Show("Google Drive", GoogleDriveEnabledBox, GoogleDriveFolderBox, GoogleDriveStatusText);
        Show("Dropbox", DropboxEnabledBox, DropboxFolderBox, DropboxStatusText);
    }


}
