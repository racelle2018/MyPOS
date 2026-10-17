using System.Windows;
using System.Windows.Controls;

namespace RetailForma.Desktop.Dialogs;

public partial class BackupPassphraseDialog : Window
{
    private readonly Guid actorId;
    private readonly DesktopBackupPreferences service;
    private readonly BackupPreferencesService.PassphraseState state;
    private bool verified;
    private bool closing;
    private bool modal;
    internal static Func<BackupPassphraseDialog, Window?, bool?>? Presenter { get; set; }
    public bool Saved { get; private set; }

    public BackupPassphraseDialog()
    {
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators can manage the recovery passphrase.");
        actorId = App.CurrentUser!.Id; service = new(); state = service.ReadPassphraseState(actorId);
        InitializeComponent();
        Title = HeadingText.Text = state.Configured ? "Change passphrase" : "Set passphrase";
        CurrentPanel.Visibility = state.Configured ? Visibility.Visible : Visibility.Collapsed;
        SetReady(!state.Configured);
        Loaded += (_, _) => { if (state.Configured) CurrentPasswordBox.Focus(); else NewPasswordBox.Focus(); };
        Closed += (_, _) => { closing = true; CurrentPasswordBox.Clear(); NewPasswordBox.Clear(); ConfirmPasswordBox.Clear(); };
    }

    public bool? ShowForOwner(Window? owner)
    {
        if (owner != null) Owner = owner;
        if (Presenter != null) return Presenter(this, owner);
        modal = true;
        return ShowDialog();
    }
    private void RequireSameSession()
    {
        if (App.CurrentUser?.Id != actorId || !Permissions.IsAdmin)
            throw new UnauthorizedAccessException("The session changed. Close and reopen this dialog.");
    }
    private void SetReady(bool ready)
    {
        NewFieldsPanel.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        NewFieldsPanel.IsEnabled = ready;
        SavePassphraseButton.IsEnabled = SavePassphraseButton.IsDefault = ready;
        VerifyCurrentButton.IsDefault = state.Configured && !ready;
    }
    private void Current_Changed(object sender, RoutedEventArgs e)
    {
        if (closing || !verified || NewFieldsPanel == null) return;
        verified = false; CurrentPasswordBox.IsEnabled = VerifyCurrentButton.IsEnabled = true;
        VerifiedText.Visibility = Visibility.Collapsed; NewPasswordBox.Clear(); ConfirmPasswordBox.Clear(); SetReady(false);
    }
    private void Verify_Click(object sender, RoutedEventArgs e)
    {
        if (!state.Configured) return;
        try
        {
            RequireSameSession(); service.VerifyCurrent(actorId, CurrentPasswordBox.Password, state.Version);
            verified = true; CurrentPasswordBox.IsEnabled = VerifyCurrentButton.IsEnabled = false;
            VerifiedText.Visibility = Visibility.Visible; SetReady(true); NewPasswordBox.Focus();
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or InvalidOperationException)
        { ActionDialog.Warning(ex.Message, Title, this); }
        catch (Exception ex)
        { Serilog.Log.Error("Backup passphrase verification failed ({FailureType}); values omitted", ex.GetType().Name); ActionDialog.Error("The current passphrase could not be verified. No passphrase was changed.", Title, this); }
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (state.Configured && !verified) { ActionDialog.Warning("Verify the current passphrase before entering a replacement.", Title, this); return; }
        try
        {
            RequireSameSession();
            if (NewPasswordBox.Password.Length == 0 || ConfirmPasswordBox.Password.Length == 0)
                throw new ArgumentException("Enter and confirm the new recovery passphrase.");
            var result = service.Save(actorId, new Dictionary<string, string>(), CurrentPasswordBox.Password,
                NewPasswordBox.Password, ConfirmPasswordBox.Password, state.Version);
            if (!result.Changed) { ActionDialog.Information("The passphrase is unchanged.", Title, this); return; }
            Saved = true;
            ActionDialog.Success("Recovery passphrase saved. Existing ZIP backups still require their original passphrase.", Title, this);
            if (modal) DialogResult = true; else Close();
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or InvalidOperationException)
        { ActionDialog.Warning(ex.Message, Title, this); }
        catch (Exception ex)
        { Serilog.Log.Error("Backup passphrase persistence failed ({FailureType}); values omitted", ex.GetType().Name); ActionDialog.Error("The passphrase could not be saved. No replacement was committed.", Title, this); }
    }
}
