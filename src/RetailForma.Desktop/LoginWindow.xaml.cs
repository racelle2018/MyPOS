using System.Windows;
using System.Windows.Input;
using RetailForma.Core.Entities;
using RetailForma.Desktop.Dialogs;

namespace RetailForma.Desktop;

public partial class LoginWindow : Window
{
    private bool _signingIn;
    private bool _closed;
    public LoginWindow()
    {
        InitializeComponent();
        Title = RetailForma.Core.ProductBrand.Name;
        Deactivated += (_, _) => HidePasswordPreview();
        Closed += (_, _) => { _closed = true; HidePasswordPreview(); PasswordBox.Clear(); };
        CompanyLabel.Text = AppSettings.Get("CompanyName", "MY STORE");
        VersionText.Text = $"{RetailForma.Core.ProductBrand.Name} {AppVersion.Current}";
        Loaded += (_, _) =>
        {
            UsernameBox.Focus();
            UpdateCapsLockHint();
        };
    }

    private void PasswordBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Dispatcher.BeginInvoke(UpdateCapsLockHint);
    }

    private void UpdateCapsLockHint()
    {
        CapsLockHint.Visibility = Keyboard.IsKeyToggled(Key.CapsLock)
            ? Visibility.Visible : Visibility.Hidden;
    }

    private void ShowPasswordPreview()
    {
        if (_signingIn) return;
        // Keep keyboard holds on the eye button so its KeyUp can hide the text.
        if (!RevealPasswordButton.IsKeyboardFocusWithin) PasswordBox.Focus();
        if (PasswordBox.Password.Length == 0) return;
        VisiblePasswordBox.Text = PasswordBox.Password;
        VisiblePasswordBox.Visibility = Visibility.Visible;
    }

    private void HidePasswordPreview()
    {
        VisiblePasswordBox.Visibility = Visibility.Collapsed;
        VisiblePasswordBox.Clear();
        if (RevealPasswordButton.IsMouseCaptured) RevealPasswordButton.ReleaseMouseCapture();
    }

    private void RevealPassword_MouseDown(object sender, MouseButtonEventArgs e)
    {
        RevealPasswordButton.CaptureMouse();
        ShowPasswordPreview();
        e.Handled = true;
    }

    private void RevealPassword_MouseUp(object sender, MouseButtonEventArgs e)
    {
        HidePasswordPreview();
        e.Handled = true;
    }

    private void RevealPassword_End(object sender, RoutedEventArgs e) => HidePasswordPreview();

    private void RevealPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter)) return;
        ShowPasswordPreview();
        e.Handled = true;
    }

    private void RevealPassword_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter)) return;
        HidePasswordPreview();
        e.Handled = true;
    }

    private void SetSigningIn(bool busy)
    {
        _signingIn = busy;
        HidePasswordPreview();
        LoginButton.Content = busy ? "Signing in…" : "Login";
        LoginButton.IsEnabled = UsernameBox.IsEnabled = PasswordBox.IsEnabled = RevealPasswordButton.IsEnabled = !busy;
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_signingIn) return;
        SetSigningIn(true);
        try { await SignInAsync(); }
        finally
        {
            if (!_closed)
            {
                SetSigningIn(false);
                if (UsernameBox.Text.Trim().Length == 0) UsernameBox.Focus();
                else PasswordBox.Focus();
            }
        }
    }

    private async Task SignInAsync()
    {
        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;

        if (username.Length == 0 || password.Length == 0)
        {
            ShowLoginWarning("Enter your username and password.");
            if (username.Length == 0) UsernameBox.Focus();
            else PasswordBox.Focus();
            return;
        }

        RetailForma.ServiceApplication.ClientUser authenticated;
        try
        {
            authenticated = await Task.Run(() => App.Service.Call<RetailForma.ServiceApplication.ClientUser>(
                RetailForma.ServiceApplication.Operation.Login, new RetailForma.ServiceApplication.LoginInput(username, password)));
        }
        catch (Exception ex)
        {
            PasswordBox.Clear(); ShowLoginWarning(ex.Message); PasswordBox.Focus(); return;
        }
        if (_closed) { App.Service.Call(RetailForma.ServiceApplication.Operation.Logout); return; }
        App.CurrentUser = DesktopService.UserView(authenticated);
        if (authenticated.MustChangePassword)
        {
            var change = new ChangeInitialPasswordDialog(App.CurrentUser, password) { Owner = this };
            if (change.ShowDialog() != true)
            {
                App.Service.Call(RetailForma.ServiceApplication.Operation.Logout); App.CurrentUser = null;
                PasswordBox.Clear(); return;
            }
        }
        PasswordBox.Clear();
        new MainWindow().Show();
        Close();
    }

    private void ShowLoginWarning(string message)
    {
        RetailForma.Desktop.Dialogs.ActionDialog.Show(this, message, "Sign-in warning", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

}
