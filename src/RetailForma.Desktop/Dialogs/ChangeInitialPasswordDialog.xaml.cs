using System.Windows;
using RetailForma.Core.Entities;

namespace RetailForma.Desktop.Dialogs;

public partial class ChangeInitialPasswordDialog : Window
{
    private readonly User _user;
    private readonly string? currentPassword;

    public ChangeInitialPasswordDialog(User user, string? currentPassword = null)
    {
        InitializeComponent();
        _user = user;
        this.currentPassword = currentPassword;
        Loaded += (_, _) => NewPasswordBox.Focus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var password = NewPasswordBox.Password;
        if (password.Length is < 16 or > 256 || password == "admin123")
        {
            ErrorText.Text = "Choose a new password of 16–256 characters."; ActionDialog.Warning(ErrorText.Text, Title, this);
            return;
        }
        if (password != ConfirmPasswordBox.Password)
        {
            ErrorText.Text = "Passwords do not match."; ActionDialog.Warning("Passwords do not match.", Title, this);
            return;
        }

        try
        {
            if (App.Service.TestDatabase is { } db)
            { _user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password); App.Service.ClientEvent("InitialPasswordChanged", "Initial password changed.", _user.Id); }
            else
                App.CurrentUser = DesktopService.UserView(App.Service.Call<RetailForma.ServiceApplication.ClientUser>(RetailForma.ServiceApplication.Operation.ChangePassword,
                    new RetailForma.ServiceApplication.ChangePasswordInput(currentPassword ?? "", password, ConfirmPasswordBox.Password)));
        }
        catch (Exception ex) { ActionDialog.Warning(ex.Message, Title, this); return; }
        ActionDialog.Success("Password updated.", Title, this);
        DialogResult = true;
    }
}
