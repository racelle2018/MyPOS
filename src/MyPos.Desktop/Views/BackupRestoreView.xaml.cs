using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MyPos.Core.Entities;
namespace MyPos.Desktop.Views;

public partial class BackupRestoreView : UserControl
{
    public BackupRestoreView()
    {
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators may manage backups.");
        InitializeComponent();
        BackupCopyFolderBox.Text = AppSettings.Get("BackupCopyFolder", "");
        RefreshBackupStatus();
        BackupCopyFolderBox.TextChanged += (_, _) => SavedFeedbackText.Text = "Unsaved changes";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("change backup preferences")) return;
        var folder = BackupCopyFolderBox.Text.Trim();
        if (folder.Length > 0 && !System.IO.Path.IsPathFullyQualified(folder))
        {
            MessageBox.Show("Enter a full folder path, or leave it blank for local backups only.",
                "Backup preferences", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (AppSettings.Get("BackupCopyFolder", "") == folder)
        { SavedFeedbackText.Text = "No changes to save."; return; }
        AppSettings.Set("BackupCopyFolder", folder);
        App.Db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, UserId = App.CurrentUser!.Id,
            Action = "SettingChanged", EntityName = "Setting", Details = "BackupCopyFolder" });
        App.Db.SaveChanges();
        SavedFeedbackText.Text = "Backup preferences saved.";
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
            MessageBox.Show(path != null ? $"Verified backup saved:\n{path}" +
                (string.IsNullOrWhiteSpace(issue) ? "" : $"\n\nWarning: {issue}") : $"Backup failed: {issue}",
                "MyPos", MessageBoxButton.OK, path != null && string.IsNullOrWhiteSpace(issue)
                    ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Backup failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { BackupButton.IsEnabled = RestoreButton.IsEnabled = true; }
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("restore backups")) return;
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


}
