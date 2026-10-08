using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using MyPos.Desktop.Controls;
using MyPos.Desktop.Views;

namespace MyPos.Desktop;

public partial class MainWindow : Window
{
    private readonly PosView _posView = new();
    private readonly IdleSessionGuard _idleGuard;
    private bool _loggingOut;

    public MainWindow()
    {
        InitializeComponent();
        WelcomeText.Text = $"{App.CurrentUser?.FullName} ({App.CurrentUser?.Role})";
        SettingsButton.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        NavUsersButton.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        ScreenHost.Content = _posView;
        SetActiveNav(NavPosButton);

        _idleGuard = new IdleSessionGuard(TimeSpan.FromMinutes(30))
        {
            PendingCartCount = () => _posView.CartCount,
            OnSwitchUser = LogoutNow
        };
        _idleGuard.Start();
        Closed += (_, _) => _idleGuard.Stop();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F12)
            {
                e.Handled = true;
                _idleGuard.LockNow();
            }
        };
    }

    private void SetActiveNav(Button active)
    {
        var surface = (Brush)FindResource("UiSurfaceBrush");
        var text = (Brush)FindResource("UiTextBrush");
        var activeBackground = (Brush)FindResource("UiAccentSoftBrush");
        var accent = (Brush)FindResource("UiAccentBrush");
        foreach (var button in new[] { NavPosButton, NavProductsButton, NavReportsButton, NavShiftButton, NavUsersButton, SettingsButton })
        {
            button.Background = surface;
            button.Foreground = text;
        }

        active.Background = activeBackground;
        active.Foreground = accent;
        var section = active == NavPosButton ? "New Sale"
            : active == NavProductsButton ? "Products"
            : active == NavReportsButton ? "Reports"
            : active == NavShiftButton ? "Shift"
            : active == NavUsersButton ? "Users"
            : "Settings";
        ViewTitleText.Text = section;
        StatusSectionText.Text = section;
    }

    private void NavPos_Click(object sender, RoutedEventArgs e)
    {
        ScreenHost.Content = _posView;
        SetActiveNav(NavPosButton);
    }

    private void NavProducts_Click(object sender, RoutedEventArgs e)
    {
        ScreenHost.Content = new ProductCatalogView();
        SetActiveNav(NavProductsButton);
    }

    private void NavReports_Click(object sender, RoutedEventArgs e)
    {
        ScreenHost.Content = new ReportsView();
        SetActiveNav(NavReportsButton);
    }

    private void NavShift_Click(object sender, RoutedEventArgs e)
    {
        ScreenHost.Content = new ShiftView();
        SetActiveNav(NavShiftButton);
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("change settings")) return;
        ScreenHost.Content = new SettingsView();
        SetActiveNav(SettingsButton);
    }

    private void NavUsers_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("manage users")) return;
        ScreenHost.Content = new UserManagementView();
        SetActiveNav(NavUsersButton);
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardCart("Logging out")) return;
        LogoutNow();
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        _idleGuard.LockNow();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_loggingOut && !ConfirmDiscardCart("Closing MyPos"))
            e.Cancel = true;
        base.OnClosing(e);
    }

    private bool ConfirmDiscardCart(string action)
    {
        if (!_posView.HasItems) return true;

        var result = MessageBox.Show(
            $"This sale has {_posView.CartCount} item(s). {action} will discard the current sale. Continue?",
            "Discard current sale?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    private void LogoutNow()
    {
        _loggingOut = true;
        App.CurrentUser = null;
        new LoginWindow().Show();
        Close();
    }
}
