using System.Windows;
using MyPos.Core.Entities;

namespace MyPos.Desktop.Dialogs;

public partial class ChangeInitialPasswordDialog : Window
{
    private readonly User _user;

    public ChangeInitialPasswordDialog(User user)
    {
        InitializeComponent();
        _user = user;
        Loaded += (_, _) => NewPasswordBox.Focus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var password = NewPasswordBox.Password;
        if (password.Length < 10 || password == "admin123")
        {
            ErrorText.Text = "Choose a new password with at least 10 characters."; ActionDialog.Warning("Choose a new password with at least 10 characters.", Title, this);
            return;
        }
        if (password != ConfirmPasswordBox.Password)
        {
            ErrorText.Text = "Passwords do not match."; ActionDialog.Warning("Passwords do not match.", Title, this);
            return;
        }

        _user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        App.Db.AuditLogs.Add(new AuditLog
        {
            Date = DateTime.Now,
            UserId = _user.Id,
            Action = "InitialPasswordChanged",
            EntityName = "User",
            EntityId = _user.Id
        });
        App.Db.SaveChanges();
        ActionDialog.Success("Password updated.", Title, this);
        DialogResult = true;
    }
}
