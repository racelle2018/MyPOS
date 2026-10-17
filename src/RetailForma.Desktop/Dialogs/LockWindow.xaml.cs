using System.Windows;
using System.Windows.Input;
using RetailForma.Core.Entities;

namespace RetailForma.Desktop.Dialogs;

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
            // The shell/dialogs have already been hidden by RunLocked. A service outage must
            // not abort lock-window construction and leave the previous session visible.
            if (App.Service.TestDatabase == null)
            {
                try { App.Service.Call(RetailForma.ServiceApplication.Operation.Lock); }
                catch (Exception ex) { ActionDialog.Warning(ex.Message + "\nSwitch users to reconnect; the previous screens remain hidden.", "Session locked", this); }
            }
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
        try
        {
            if (App.Service.TestDatabase != null)
            {
                if (!BCrypt.Net.BCrypt.Verify(PasswordBox.Password, _user.PasswordHash))
                { App.Service.ClientEvent("UnlockFailed", "Failed session unlock attempt"); throw new UnauthorizedAccessException("Incorrect password. Please try again."); }
            }
            else App.CurrentUser = DesktopService.UserView(App.Service.Call<RetailForma.ServiceApplication.ClientUser>(RetailForma.ServiceApplication.Operation.Unlock,
                new RetailForma.ServiceApplication.PasswordInput(PasswordBox.Password)));
        }
        catch (Exception ex)
        {
            PasswordBox.Clear();
            RetailForma.Desktop.Dialogs.ActionDialog.Show(this, ex.Message,
                "Unable to unlock", MessageBoxButton.OK, MessageBoxImage.Warning);
            PasswordBox.Focus();
            return;
        }

        _allowClose = true;
        PasswordBox.Clear();
        DialogResult = true;
    }

    private void SwitchUserButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingCartCount > 0)
        {
            var result = RetailForma.Desktop.Dialogs.ActionDialog.Show(
                $"This sale has {_pendingCartCount} item(s). Switching users will discard it. Continue?",
                "Discard current sale?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }

        _allowClose = true;
        DialogResult = false;
    }
}
