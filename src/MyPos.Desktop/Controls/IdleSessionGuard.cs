using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop.Controls;

public sealed class IdleSessionGuard
{
    private readonly TimeSpan _timeout;
    private readonly DispatcherTimer _timer;
    private DateTime _lastActivity;
    private bool _locked;
    private bool _subscribed;

    public Func<int>? PendingCartCount { get; set; }
    public Action? OnSwitchUser { get; set; }

    public IdleSessionGuard(TimeSpan timeout)
    {
        _timeout = timeout;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => CheckIdle();
    }

    public void Start()
    {
        _lastActivity = DateTime.UtcNow;
        if (!_subscribed)
        {
            InputManager.Current.PreProcessInput += OnAnyInput;
            _subscribed = true;
        }

        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        if (_subscribed)
        {
            InputManager.Current.PreProcessInput -= OnAnyInput;
            _subscribed = false;
        }
    }

    public void LockNow()
    {
        if (_locked) return;

        _locked = true;
        try
        {
            var user = App.CurrentUser;
            if (user is null)
            {
                OnSwitchUser?.Invoke();
                return;
            }

            var lockWindow = new LockWindow(user, PendingCartCount?.Invoke() ?? 0)
            {
                Owner = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault()
            };
            RunLocked(lockWindow.Owner, () => lockWindow.ShowDialog() == true,
                () => OnSwitchUser?.Invoke());
        }
        finally
        {
            _lastActivity = DateTime.UtcNow;
            _locked = false;
        }
    }

    internal static void RunLocked(Window? shell, Func<bool> showLock, Action switchUser)
    {
        var windows = Application.Current.Windows.OfType<Window>()
            .Select(window => (Window: window, Handle: new WindowInteropHelper(window).Handle))
            .Where(item => item.Handle != IntPtr.Zero && IsWindowVisible(item.Handle))
            .OrderBy(item => OwnerDepth(item.Window))
            .ToArray();
        var activeWindow = windows.LastOrDefault(item => item.Window.IsActive).Window;
        // Window.Hide ends WPF's ShowDialog loop. Hide native surfaces instead so
        // pending modal forms stay open, disabled owners stay disabled, and no
        // dialog result/save/cancel continuation runs merely because we lock.
        foreach (var item in windows.Reverse())
            ShowWindow(item.Handle, 0); // SW_HIDE
        bool resumed;
        try
        {
            resumed = showLock();
        }
        catch
        {
            // Never expose the previous session if the lock dialog fails.
            switchUser();
            throw;
        }

        if (!resumed)
        {
            switchUser();
            return;
        }

        foreach (var item in windows)
            if (IsWindow(item.Handle) && item.Window.IsVisible)
                ShowWindow(item.Handle, 8); // SW_SHOWNA preserves size/state without activation
        if (activeWindow?.IsVisible == true) activeWindow.Activate();
        else if (shell?.IsVisible == true) shell.Activate();
    }

    private static int OwnerDepth(Window window)
    {
        var depth = 0;
        for (var owner = window.Owner; owner is not null; owner = owner.Owner) depth++;
        return depth;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr handle);

    private void OnAnyInput(object? sender, PreProcessInputEventArgs e)
    {
        if (!_locked) _lastActivity = DateTime.UtcNow;
    }

    private void CheckIdle()
    {
        if (!_locked && DateTime.UtcNow - _lastActivity >= _timeout) LockNow();
    }
}
