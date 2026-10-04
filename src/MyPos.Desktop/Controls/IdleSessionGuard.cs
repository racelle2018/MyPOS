using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
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
            var resumed = lockWindow.ShowDialog() == true;
            if (!resumed) OnSwitchUser?.Invoke();
        }
        finally
        {
            _lastActivity = DateTime.UtcNow;
            _locked = false;
        }
    }

    private void OnAnyInput(object? sender, PreProcessInputEventArgs e)
    {
        if (!_locked) _lastActivity = DateTime.UtcNow;
    }

    private void CheckIdle()
    {
        if (!_locked && DateTime.UtcNow - _lastActivity >= _timeout) LockNow();
    }
}
