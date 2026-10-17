using System.Windows;
using RetailForma.Core.Entities;

namespace RetailForma.Desktop.Dialogs;

public partial class UserEditDialog : Window
{
    private readonly User? _editing;
    private string _oldUsername = "";

    public UserEditDialog(User? editing)
    {
        InitializeComponent();
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators may add or edit users.");
        if (!UserAccess.CanManageUser(App.CurrentUser, editing))
            throw new UnauthorizedAccessException("Only a System Administrator may manage a technical account.");
        _editing = editing;
        RoleBox.ItemsSource = editing?.Role == UserRole.SystemAdministrator
            ? new[] { "SYSTEM ADMINISTRATOR" } : new[] { "OWNER", "CASHIER" };
        if (editing != null)
        {
            _oldUsername = editing.Username;
            Title = "Edit user"; TitleText.Text = $"EDIT USER — {editing.Username}";
            UsernameBox.Text = editing.Username;
            FullNameBox.Text = editing.FullName;
            RoleBox.SelectedItem = UserAccess.RoleName(editing.Role).ToUpperInvariant();
            if (editing.Role == UserRole.SystemAdministrator) RoleBox.IsEnabled = false;
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
        if (!UserAccess.CanManageUser(App.CurrentUser, _editing))
        { Fail("You may not manage this account."); return; }
        var fullName = FullNameBox.Text.Trim();
        var role = _editing?.Role == UserRole.SystemAdministrator ? UserRole.SystemAdministrator :
            RoleBox.SelectedItem as string == "OWNER" ? UserRole.Admin : UserRole.Cashier;
        var password = PasswordBox.Password;
        if (fullName.Length == 0) { Fail("Full name is required."); return; }

        var username = UsernameBox.Text.Trim();
        if (username.Length < 3 || !username.All(char.IsLetterOrDigit)) { Fail("Username must contain at least three letters or numbers."); return; }
        if (App.Service.Staff().Any(u => u.Id != _editing?.Id && string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
        { Fail("That username is already taken."); return; }
        if ((_editing == null || password.Length > 0) && (password.Length is < 16 or > 256 || password != ConfirmBox.Password))
        { Fail("Enter matching individual passwords of 16–256 characters."); return; }
        var active = _editing == null || _editing.Id == App.CurrentUser!.Id || ActiveBox.IsChecked == true;
        try
        {
            App.Service.SaveUser(new RetailForma.ServiceApplication.UserInput(_editing?.Id, username, fullName, role, password, active));
        }
        catch (Exception ex) { Fail(ex.Message); return; }
        ActionDialog.Success(_editing == null ? "User added." : "User updated.", Title, this);
        DialogResult = true;
    }

    private void Fail(string message) { ErrorText.Text = message; ActionDialog.Warning(message, Title, this); }
}
