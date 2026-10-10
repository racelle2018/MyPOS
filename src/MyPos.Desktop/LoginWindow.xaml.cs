using System.Windows;
using System.Windows.Input;
using MyPos.Core.Entities;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        CompanyLabel.Text = AppSettings.Get("CompanyName", "MY STORE");
        VersionText.Text = $"MYPOS {typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";
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
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
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

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
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

        if (string.Equals(user.Username, "admin", StringComparison.OrdinalIgnoreCase)
            && BCrypt.Net.BCrypt.Verify("admin123", user.PasswordHash))
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
        MessageBox.Show(this, message, "Sign-in warning", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void UsernameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {

    }

    private void UsernameBox_TextChanged_1(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {

    }
}
