using System.Windows;
using System.Windows.Input;
using MyPos.Core.Entities;

namespace MyPos.Desktop.Dialogs;

public partial class LockWindow : Window
{
    private readonly User _user;
    private readonly int _pendingCartCount;
    private bool _allowClose;

    public LockWindow(User user, int pendingCartCount)
    {
        InitializeComponent();
        _user = user;
        _pendingCartCount = pendingCartCount;
        UserText.Text = $"{user.FullName} ({user.Username})";
        if (pendingCartCount > 0)
        {
            PendingCartText.Text = $"Switching users will discard the current sale ({pendingCartCount} item(s)).";
            PendingCartText.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) =>
        {
            PasswordBox.Focus();
            UpdateCapsLockHint();
        };
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose) e.Cancel = true;
        base.OnClosing(e);
    }

    private void UnlockButton_Click(object sender, RoutedEventArgs e) => Unlock();

    private void PasswordBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Unlock();
        }
    }

    private void PasswordBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        => Dispatcher.BeginInvoke(UpdateCapsLockHint);

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        UpdateCapsLockHint();
    }

    private void UpdateCapsLockHint()
    {
        CapsLockHint.Visibility = Keyboard.IsKeyToggled(Key.CapsLock)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Unlock()
    {
        if (!BCrypt.Net.BCrypt.Verify(PasswordBox.Password, _user.PasswordHash))
        {
            App.Db.AuditLogs.Add(new MyPos.Core.Entities.AuditLog
            {
                UserId = _user.Id,
                Action = "UnlockFailed",
                Details = "Failed session unlock attempt"
            });
            App.Db.SaveChanges();
            PasswordBox.Clear();
            MessageBox.Show(this, "Incorrect password. Please try again.",
                "Unable to unlock", MessageBoxButton.OK, MessageBoxImage.Warning);
            PasswordBox.Focus();
            return;
        }

        _allowClose = true;
        DialogResult = true;
    }

    private void SwitchUserButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingCartCount > 0)
        {
            var result = MessageBox.Show(
                $"This sale has {_pendingCartCount} item(s). Switching users will discard it. Continue?",
                "Discard current sale?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }

        _allowClose = true;
        DialogResult = false;
    }
}
