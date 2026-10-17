using System.IO;
using System.Security.Cryptography;
using System.Windows;
using Microsoft.Win32;
using RetailForma.Desktop.Dialogs;
using RetailForma.ServiceApplication;

namespace RetailForma.Setup;

public partial class MainWindow
{
    private void BrowseImport_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Select the original company backup", Filter = "RetailForma encrypted backup (*.zip)|*.zip|Legacy database backup (*.db)|*.db", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        ImportPathBox.Text = picker.FileName; ImportPasswordBox.Clear(); ImportConfirmedBox.IsChecked = false; ImportStatusText.Text = "";
    }
    private async void ImportCompany_Click(object sender, RoutedEventArgs e)
    {
        if (ImportConfirmedBox.IsChecked != true || ImportPasswordBox.Password.Length is < 16 or > 256 || !File.Exists(ImportPathBox.Text))
        { ActionDialog.Warning("Select the encrypted backup, enter its original passphrase, and confirm the preserved source first.", Title, this); return; }
        var path = ImportPathBox.Text; var phrase = ImportPasswordBox.Password; Guid? transfer = null; SetBusy(true);
        try
        {
            using var currentClient = new System.Threading.Mutex(false, @"Local\RetailFormaDesktop");
            using var oldClient = new System.Threading.Mutex(false, @"Local\MyPosDesktop");
            // This is a warning check only, not the service's security authority. Source consistency
            // comes from the authenticated completed archive, never from reading a live database.
            bool Owned(Mutex mutex) { try { return mutex.WaitOne(0); } catch (AbandonedMutexException) { return true; } }
            var ownsCurrent = Owned(currentClient); var ownsOld = Owned(oldClient);
            if (ownsCurrent) currentClient.ReleaseMutex(); if (ownsOld) oldClient.ReleaseMutex();
            if (!ownsCurrent || !ownsOld) throw new InvalidOperationException("Close RetailForma and the old POS normally before company conversion.");
            var status = await Task.Run(() => service.Call<ServiceStatusInfo>(Operation.ServiceStatus));
            if (status.Configured) throw new InvalidOperationException("Protected company storage already exists. Initial import cannot replace it; use Backup & Restore after signing in.");
            ImportStatusText.Text = "Uploading encrypted backup…";
            transfer = await Task.Run(() => UploadCompany(path));
            var report = await Task.Run(() => service.Call<RestorePreview>(Operation.PreviewCompanyImport, new PrepareRestoreInput(transfer.Value, phrase)));
            ImportPasswordBox.Clear();
            ImportStatusText.Text = $"Verified: {report.Company} · {report.Users} accounts · {report.Sales} sales";
            if (ActionDialog.Show(this, $"Import {report.Company} into protected service storage?\n\n{report.Users} accounts · {report.Sales} sales\n\nThe original archive is preserved. Do not reopen the old POS after cutover.",
                "Confirm protected company import", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            var imported = await Task.Run(() => service.Call<ServiceStatusInfo>(Operation.CommitCompanyImport, new IdInput(transfer.Value)));
            transfer = null; preview = imported;
            ImportStatusText.Text = $"Imported: {imported.Company}. Sign in and create a new verified local/cloud backup.";
            ActionDialog.Success(imported.TechnicalAccountExists ? "Company imported and verified. Existing accounts and roles were preserved." :
                "Company imported and verified. Use Initial setup to create the first individual System Administrator.", Title, this);
        }
        catch (Exception ex) { ActionDialog.Warning(ex.Message, "Import needs attention", this); }
        finally
        {
            if (transfer.HasValue) { try { await Task.Run(() => service.Call(Operation.CancelCompanyImport, new IdInput(transfer.Value))); } catch { } }
            phrase = ""; ImportPasswordBox.Clear(); SetBusy(false);
        }
    }
    private Guid UploadCompany(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".db", StringComparison.OrdinalIgnoreCase))
            foreach (var suffix in new[] { "-wal", "-journal" })
                if (File.Exists(path + suffix) && new FileInfo(path + suffix).Length > 0)
                    throw new InvalidOperationException("The selected database has pending WAL/journal data. Close its POS normally and create a verified backup; copying only this file could omit transactions.");
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = Convert.ToHexString(SHA256.HashData(input)); input.Position = 0;
        var transfer = service.Call<Guid>(Operation.BeginCompanyImport, new RestoreUploadInput(input.Length, hash));
        try
        {
            var buffer = new byte[192 * 1024]; long offset = 0; int count;
            while ((count = input.Read(buffer)) != 0)
            {
                var written = service.Call<long>(Operation.WriteCompanyImportChunk, new RestoreChunkInput(transfer, offset, buffer.AsSpan(0, count).ToArray()));
                if (written != offset + count) throw new InvalidDataException("The service did not acknowledge the import chunk."); offset = written;
            }
            return transfer;
        }
        catch { try { service.Call(Operation.CancelCompanyImport, new IdInput(transfer)); } catch { } throw; }
    }
}
