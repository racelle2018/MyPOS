using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RetailForma.Core.Services;
using RetailForma.Desktop.Dialogs;
using RetailForma.Desktop.Licensing;
namespace RetailForma.Desktop.Views;
public partial class LicensingView : UserControl
{
    public LicensingView()
    {
        if (!Permissions.IsSystemAdministrator) throw new UnauthorizedAccessException("Only System Administrator may manage licensing.");
        InitializeComponent();
    }
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.IsSystemAdministrator) return;
        var picker = new SaveFileDialog { Filter = "RetailForma device request (*.json)|*.json", FileName = "retailforma-device-request.json" };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        IsEnabled = false;
        try
        {
            var request = await Task.Run(() => App.Service.Call<LicenseRequest>(RetailForma.ServiceApplication.Operation.LicenseRequest));
            File.WriteAllText(picker.FileName, JsonSerializer.Serialize(request));
            ActionDialog.Success("Device request saved. Send it to the programmer; no raw motherboard/CPU identifiers are included.", "Licensing", Window.GetWindow(this));
        }
        catch (Exception ex) { ActionDialog.Error(ex.Message, "Licensing", Window.GetWindow(this)); }
        finally { IsEnabled = true; }
    }
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.IsSystemAdministrator) return;
        var picker = new OpenFileDialog { Filter = "Signed RetailForma license (*.json)|*.json" };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        IsEnabled = false;
        try
        {
            if (new FileInfo(picker.FileName).Length > 32768) throw new InvalidDataException("License file is too large.");
            var token = JsonSerializer.Deserialize<SignedLicenseMessage>(File.ReadAllText(picker.FileName)) ?? throw new InvalidDataException("Missing signed license.");
            var claims = await Task.Run(() => App.Service.Call<LicenseClaims>(RetailForma.ServiceApplication.Operation.ImportLicense, token));
            StatusText.Text = claims.Kind == LicenseKind.Lifetime ? "Lifetime license verified — no expiry; offline operation." : "Trial verified online — server-controlled seven-day expiry.";
            ActionDialog.Success("Signed license verified and encrypted local storage updated.", "Licensing", Window.GetWindow(this));
        }
        catch (Exception ex) { ActionDialog.Error(ex.Message, "Licensing", Window.GetWindow(this)); }
        finally { IsEnabled = true; }
    }
    private async void StartTrial_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.IsSystemAdministrator) return;
        if (!Guid.TryParse(TrialIdBox.Text, out var id) || string.IsNullOrWhiteSpace(TrialKeyBox.Password))
        { ActionDialog.Warning("Enter the programmer's trial invitation ID and activation key.", "Licensing", Window.GetWindow(this)); return; }
        var activationKey = TrialKeyBox.Password; IsEnabled = false;
        try
        {
            var claims = await Task.Run(() => App.Service.Call<LicenseClaims>(RetailForma.ServiceApplication.Operation.StartTrial,
                new RetailForma.ServiceApplication.TrialStartInput(id, activationKey)));
            TrialKeyBox.Clear();
            StatusText.Text = "Trial activated using server time. Every business posting requires fresh online validation.";
            ActionDialog.Success("Trial verified online and stored. Repeating activation will not reset its server start date.", "Licensing", Window.GetWindow(this));
        }
        catch (Exception ex) { ActionDialog.Error(ex.Message, "Licensing", Window.GetWindow(this)); }
        finally { IsEnabled = true; }
    }
}
