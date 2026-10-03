using System.Windows;
using MyPos.Core.Entities;

namespace MyPos.Desktop.Dialogs;

public partial class UserEditDialog : Window
{
    private readonly User? _editing;
    private string _oldUsername = "";

    public UserEditDialog(User? editing)
    {
        InitializeComponent();
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators may add or edit users.");
        _editing = editing;
        RoleBox.ItemsSource = new[] { "ADMIN", "CASHIER" };
        if (editing != null)
        {
            _oldUsername = editing.Username;
            Title = "Edit user"; TitleText.Text = $"EDIT USER — {editing.Username}";
            UsernameBox.Text = editing.Username;
            FullNameBox.Text = editing.FullName;
            RoleBox.SelectedItem = editing.Role == UserRole.Admin ? "ADMIN" : "CASHIER";
            ActiveBox.IsChecked = editing.IsActive;
            PasswordLabel.Text = "New password (leave blank to keep current)";
            ConfirmLabel.Text = "Confirm new password";
            if (editing.Id == App.CurrentUser!.Id)
            {
                RoleBox.IsEnabled = false; ActiveBox.IsEnabled = false;
                RoleBox.ToolTip = "You cannot change your own role while logged in.";
                ActiveBox.ToolTip = "You cannot deactivate your own account.";
            }
        }
        else
        {
            Title = "Add user"; TitleText.Text = "Add user"; RoleBox.SelectedIndex = 1; ActiveBox.IsChecked = true; ActiveBox.IsEnabled = false;
        }
        if (editing != null) FullNameBox.Focus(); else UsernameBox.Focus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var fullName = FullNameBox.Text.Trim();
        var role = RoleBox.SelectedItem as string == "ADMIN" ? UserRole.Admin : UserRole.Cashier;
        var password = PasswordBox.Password;
        if (fullName.Length == 0) { Fail("Full name is required."); return; }

        if (_editing == null)
        {
            var username = UsernameBox.Text.Trim();
            if (username.Length < 3) { Fail("Username must be at least 3 characters."); return; }
            if (!username.All(char.IsLetterOrDigit)) { Fail("Username may contain letters and numbers only."); return; }
            if (App.Db.Users.ToList().Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase))) { Fail("That username is already taken."); return; }
            if (password.Length < 6) { Fail("Password must be at least 6 characters."); return; }
            if (password != ConfirmBox.Password) { Fail("Passwords do not match."); return; }
            var user = new User { Username = username, FullName = fullName, Role = role, IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password) };
            App.Db.Users.Add(user);
            App.Db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, UserId = App.CurrentUser?.Id, Action = "UserCreate", EntityName = "User", EntityId = user.Id, Details = $"{user.Username} — {role}" });
        }
        else
        {
            var self = _editing.Id == App.CurrentUser!.Id;
            var username = UsernameBox.Text.Trim();
            if (username.Length < 3) { Fail("Username must be at least 3 characters."); return; }
            if (!username.All(char.IsLetterOrDigit)) { Fail("Username may contain letters and numbers only."); return; }
            if (App.Db.Users.ToList().Any(u => u.Id != _editing.Id && string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase))) { Fail($"Username '{username}' is already taken."); return; }
            var renamed = !string.Equals(_editing.Username, username, StringComparison.OrdinalIgnoreCase);
            _editing.Username = username;
            _editing.FullName = fullName; _editing.Role = role; _editing.IsActive = self || ActiveBox.IsChecked == true;
            var details = renamed ? $"{_editing.Username} (RENAMED FROM {_oldUsername})" : _editing.Username;
            details += $" — {_editing.Role}" + (_editing.IsActive ? "" : " — DISABLED");
            if (password.Length > 0)
            {
                if (password.Length < 6) { Fail("New password must be at least 6 characters."); return; }
                if (password != ConfirmBox.Password) { Fail("Passwords do not match."); return; }
                _editing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password); details += " — PASSWORD RESET";
            }
            App.Db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, UserId = App.CurrentUser?.Id, Action = "UserEdit", EntityName = "User", EntityId = _editing.Id, Details = details });
        }
        App.Db.SaveChanges();
        DialogResult = true;
    }

    private void Fail(string message) => ErrorText.Text = message;
}
