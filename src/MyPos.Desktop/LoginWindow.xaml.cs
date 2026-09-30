using System.Windows;
using MyPos.Core.Entities;

namespace MyPos.Desktop;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        UsernameBox.Focus();
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