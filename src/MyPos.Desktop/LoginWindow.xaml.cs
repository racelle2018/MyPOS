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
        ErrorText.Text = "";

        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;

        if (username.Length == 0 || password.Length == 0)
        {
            ErrorText.Text = "Enter your username and password.";
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
                Details = $"Attempted username: {username.ToUpperInvariant()}"
            });
            App.Db.SaveChanges();

            ErrorText.Text = "Invalid username or password.";
            PasswordBox.Clear();
            PasswordBox.Focus();
            return;
        }

        if (!user.IsActive)
        {
            ErrorText.Text = "This account has been disabled.";
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
}
