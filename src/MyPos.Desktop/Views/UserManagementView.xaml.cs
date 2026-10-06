using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyPos.Core.Entities;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop.Views;

public partial class UserManagementView : UserControl
{
    public UserManagementView()
    {
        InitializeComponent();
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators may manage users.");
        LoadUsers();
        UsersGrid.MouseDoubleClick += (_, _) => { if (Selected != null) EditButton_Click(this, new RoutedEventArgs()); };
        SearchBox.Focus();
    }

    private UserRowVM? Selected => UsersGrid.SelectedItem as UserRowVM;

    private void LoadUsers()
    {
        var term = SearchBox.Text.Trim();
        var users = App.Db.Users.Where(user => ShowInactiveBox.IsChecked == true || user.IsActive).OrderBy(user => user.Username).ToList();
        if (term.Length > 0)
            users = users.Where(user => user.Username.Contains(term, StringComparison.OrdinalIgnoreCase) || user.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        var list = users.Select(user => new UserRowVM(user)).ToList();
        UsersGrid.ItemsSource = list;
        CountText.Text = ShowInactiveBox.IsChecked == true ? $"{list.Count} user(s) — showing inactive" : $"{list.Count} active user(s)";
        EmptyHint.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Text = term.Length > 0 ? "No users match your search" : "No users yet — add your first user";
        UpdateToggleButton();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => LoadUsers();
    private void ShowInactiveBox_Changed(object sender, RoutedEventArgs e) => LoadUsers();
    private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateToggleButton();

    private void UpdateToggleButton()
    {
        EditButton.IsEnabled = Selected != null;
        ToggleActiveButton.IsEnabled = Selected != null && Selected.User.Id != App.CurrentUser?.Id;
        ToggleActiveButton.Content = Selected is { User.IsActive: false } ? "Activate" : "Deactivate";
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (new UserEditDialog(null).ShowDialog() == true) LoadUsers();
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) { MessageBox.Show("Select a user first.", "MyPos", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (new UserEditDialog(Selected.User).ShowDialog() == true) LoadUsers();
    }

    private void ToggleActiveButton_Click(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null) { MessageBox.Show("Select a user first.", "MyPos", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (row.User.Id == App.CurrentUser!.Id) { MessageBox.Show("You cannot deactivate your own account while logged in.", "MyPos", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var activating = !row.User.IsActive;
        var message = activating ? $"Reactivate '{row.User.Username}'?" : $"Deactivate '{row.User.Username}'?\nTheir history stays intact.";
        if (MessageBox.Show(message, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        row.User.IsActive = activating;
        App.Db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, UserId = App.CurrentUser.Id, Action = activating ? "UserActivate" : "UserDeactivate", EntityName = "User", EntityId = row.User.Id, Details = row.User.Username });
        App.Db.SaveChanges();
        LoadUsers();
    }
}

public class UserRowVM
{
    public User User { get; }
    public UserRowVM(User user) => User = user;
    public string Username => User.Username;
    public string FullName => User.FullName;
    public string RoleText => User.Role.ToString().ToUpperInvariant();
    public string StatusText => User.IsActive ? "ACTIVE" : "DISABLED";
    public Brush StatusBrush => User.IsActive ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)) : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
    public string LastLoginText => User.LastLoginAt?.ToString("MMM d, h:mm tt").ToUpperInvariant() ?? "NEVER";
}
