using System.Windows;
using RetailForma.Desktop.Dialogs;

namespace RetailForma.LicenseIssuer.Gui;

public partial class PassphraseDialog : Window
{
    private readonly bool creating;
    public string Passphrase { get; private set; } = "";
    public PassphraseDialog(bool creating)
    {
        InitializeComponent(); this.creating = creating;
        MaxHeight = Math.Min(MaxHeight, Math.Max(180, SystemParameters.WorkArea.Height - 32));
        PasswordScroller.MaxHeight = Math.Max(64, MaxHeight - 140);
        ConfirmationPanel.Visibility = creating ? Visibility.Visible : Visibility.Collapsed;
        if (creating) HelpText.Text = "Choose a matching passphrase of at least 16 characters. Keep it securely; losing it prevents future issuance with this key.";
        Loaded += (_, _) => PasswordInput.Focus();
        Closed += (_, _) => { PasswordInput.Clear(); ConfirmationInput.Clear(); };
    }
    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordInput.Password;
        if (string.IsNullOrWhiteSpace(password) || creating && (password.Length < 16 || password != ConfirmationInput.Password))
        { ActionDialog.Warning(creating ? "Use matching passphrases of at least 16 characters." : "Enter the private-key passphrase.", Title, this); return; }
        Passphrase = password; DialogResult = true;
    }
    public void Forget() => Passphrase = "";
}
