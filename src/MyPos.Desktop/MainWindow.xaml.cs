using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Interop;
using MyPos.Desktop.Controls;
using MyPos.Desktop.Views;

namespace MyPos.Desktop;

public partial class MainWindow : Window
{
    private readonly PosView _posView = new();
    private readonly IdleSessionGuard _idleGuard;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
            ((HwndSource)PresentationSource.FromVisual(this)).AddHook(MaximizeToWorkArea);
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
        if (!ConfirmDiscardCart()) return;
        LogoutNow();
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        _idleGuard.LockNow();
    }

    private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeWindowButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        UpdateMaximizeButton();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateMaximizeButton();
    }

    private void UpdateMaximizeButton()
    {
        if (MaximizeWindowButton is null) return;
        var maximized = WindowState == WindowState.Maximized;
        MaximizeWindowButton.Content = maximized ? "\uE923" : "\uE922";
        MaximizeWindowButton.ToolTip = maximized ? "Restore" : "Maximize";
        System.Windows.Automation.AutomationProperties.SetName(
            MaximizeWindowButton, maximized ? "Restore window" : "Maximize window");
    }

    private static IntPtr MaximizeToWorkArea(IntPtr hwnd, int message,
        IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x0024) return IntPtr.Zero; // WM_GETMINMAXINFO

        var monitor = NativeWindowBounds.MonitorFromWindow(hwnd, 2); // nearest monitor
        var info = new NativeWindowBounds.MonitorInfo
        {
            Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeWindowBounds.MonitorInfo>()
        };
        if (monitor == IntPtr.Zero || !NativeWindowBounds.GetMonitorInfo(monitor, ref info))
            return IntPtr.Zero;

        var limits = System.Runtime.InteropServices.Marshal
            .PtrToStructure<NativeWindowBounds.MinMaxInfo>(lParam);
        limits.MaxPosition.X = info.WorkArea.Left - info.MonitorArea.Left;
        limits.MaxPosition.Y = info.WorkArea.Top - info.MonitorArea.Top;
        limits.MaxSize.X = info.WorkArea.Right - info.WorkArea.Left;
        limits.MaxSize.Y = info.WorkArea.Bottom - info.WorkArea.Top;
        System.Runtime.InteropServices.Marshal.StructureToPtr(limits, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private bool ConfirmDiscardCart()
    {
        if (!_posView.HasItems) return true;

        var result = MessageBox.Show(
            $"This sale has {_posView.CartCount} item(s). Logging out will discard the current sale. Continue?",
            "Discard current sale?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    private void LogoutNow()
    {
        App.CurrentUser = null;
        new LoginWindow().Show();
        Close();
    }
}

internal static class NativeWindowBounds
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        public int Size;
        public Rect MonitorArea;
        public Rect WorkArea;
        public int Flags;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
