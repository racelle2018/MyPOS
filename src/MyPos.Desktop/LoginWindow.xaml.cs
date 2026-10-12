using System.Windows;
using System.Windows.Input;
using MyPos.Core.Entities;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop;

public partial class LoginWindow : Window
{
    private bool _signingIn;
    private bool _closed;
    public LoginWindow()
    {
        InitializeComponent();
        Deactivated += (_, _) => HidePasswordPreview();
        Closed += (_, _) => { _closed = true; HidePasswordPreview(); PasswordBox.Clear(); };
        CompanyLabel.Text = AppSettings.Get("CompanyName", "MY STORE");
        VersionText.Text = $"MYPOS {AppVersion.Current}";
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

        // ToList first, compare in memory — consistent habit with SQLite + our decimal rule
        var user = App.Db.Users.ToList().FirstOrDefault(
            u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

        var hash = user?.PasswordHash;
        var valid = hash is not null && await Task.Run(() => BCrypt.Net.BCrypt.Verify(password, hash));
        if (_closed) return;
        if (user == null || !valid)
        {
            App.Db.AuditLogs.Add(new AuditLog
            {
                Date = DateTime.Now,
                Action = "LoginFailed",
                EntityName = "User",
                Details = $"Attempted username: {username}"
            });
            App.Db.SaveChanges();

            PasswordBox.Clear();
            ShowLoginWarning("Invalid username or password.");
            PasswordBox.Focus();
            return;
        }

        if (!user.IsActive)
        {
            PasswordBox.Clear();
            ShowLoginWarning("This account has been disabled.");
            PasswordBox.Focus();
            return;
        }

        var initialAdminPassword = string.Equals(user.Username, "admin", StringComparison.OrdinalIgnoreCase)
            && await Task.Run(() => BCrypt.Net.BCrypt.Verify("admin123", hash!));
        if (_closed) return;
        if (initialAdminPassword)
        {
            var change = new ChangeInitialPasswordDialog(user) { Owner = this };
            if (change.ShowDialog() != true) return;
        }

        user.LastLoginAt = DateTime.Now;

        App.Db.AuditLogs.Add(new AuditLog
        {
            Date = DateTime.Now, UserId = user.Id, Action = "Login",
            EntityName = "User", EntityId = user.Id
        });
        App.Db.SaveChanges();

        App.CurrentUser = user;
        new MainWindow().Show();
        Close();
    }

    private void ShowLoginWarning(string message)
    {
        MyPos.Desktop.Dialogs.ActionDialog.Show(this, message, "Sign-in warning", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

}
