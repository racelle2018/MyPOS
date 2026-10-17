using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Serilog;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;

namespace RetailForma.Desktop;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private bool _ownsInstance;
    private Mutex? _legacyInstance;
    private bool _ownsLegacyInstance;
    private DispatcherTimer? _backupTimer;
    private static RetailFormaDbContext? testDatabase;
    // Compatibility for isolated in-memory UI fixtures only. Production has no writable EF handle.
    public static RetailFormaDbContext Db
    {
        get => Current is App { IsolatedTestHost: true } && testDatabase != null ? testDatabase :
            throw new InvalidOperationException("The desktop cannot open the working database. Use typed service operations.");
        private set
        {
            if (Current is not App { IsolatedTestHost: true }) throw new InvalidOperationException("Only the isolated UI test host accepts a context.");
            if (value != null && new SqliteConnectionStringBuilder(value.Database.GetDbConnection().ConnectionString).Mode != SqliteOpenMode.Memory &&
                new SqliteConnectionStringBuilder(value.Database.GetDbConnection().ConnectionString).DataSource != ":memory:")
                throw new InvalidOperationException("The isolated UI test host only accepts an in-memory database.");
            testDatabase = value;
        }
    }
    public static User? CurrentUser { get; set; }
    public static DesktopService Service { get; } = new();
    // UI tests use an in-memory context and must not run live startup while pumping modal windows.
    internal bool IsolatedTestHost { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (IsolatedTestHost) return;
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown; // Pre-login migration dialogs must not terminate startup when closed.

        // Keep dialogs inside the usable desktop area at 125%/150% scaling.
        // Scrollable dialogs can then reveal the full form instead of placing
        // their action buttons below the screen edge.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window { WindowState: not WindowState.Maximized } window)
                {
                    window.MaxHeight = Math.Min(window.MaxHeight,
                        Math.Max(240, SystemParameters.WorkArea.Height - 32));
                    window.MaxWidth = Math.Min(window.MaxWidth,
                        Math.Max(280, SystemParameters.WorkArea.Width - 32));
                }
            }));

        _singleInstance = new Mutex(true, @"Local\RetailFormaDesktop", out _ownsInstance);
        if (!_ownsInstance)
        {
            RetailForma.Desktop.Dialogs.ActionDialog.Show("RetailForma is already running. Return to the open window.",
                "RetailForma", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Transitional lock prevents old/new clients writing diverging copies in the same Windows session.
        _legacyInstance = new Mutex(false, LegacyMigrationIdentity.Mutex);
        try { _ownsLegacyInstance = _legacyInstance.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsLegacyInstance = true; }
        if (!_ownsLegacyInstance)
        {
            Dialogs.ActionDialog.Warning("Close the old POS before opening RetailForma. No data has been changed.");
            Shutdown(); return;
        }
        var folder = AppPaths.Folder;
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, "logs"));
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(folder, "logs", "retailforma-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
        try
        {
            var identity = Service.Settings(beforeLogin: true);
            if (identity.GetValueOrDefault("System.ServiceConfigured") == "false")
                throw new InvalidOperationException("Complete Technician Setup or migrate the existing company before opening RetailForma. No replacement company was created.");
            Controls.AppearanceResources.Apply(Service.Appearance());
        }
        catch (Exception ex)
        {
            Log.Error("Protected database startup rejected ({FailureType}); credentials omitted", ex.GetType().Name);
            Dialogs.ActionDialog.Error("RetailForma could not connect to its protected company service.\n\n" + ex.Message +
                "\n\nUse Technician Setup for installation or recovery. Do not move, delete or recreate database files.", "Protected storage needs attention");
            Shutdown(); return;
        }
        _ = Task.Run(BackupService.RunIfDue);
        _backupTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _backupTimer.Tick += (_, _) => _ = Task.Run(BackupService.RunIfDue);
        _backupTimer.Start();

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            var message = args.Exception is SqliteException
                ? "The database is busy or locked. Close other programs using the database and try again."
                : "An unexpected error occurred and was logged.\n\n" + args.Exception.Message;
            RetailForma.Desktop.Dialogs.ActionDialog.Show(message, "RetailForma", MessageBoxButton.OK,
                args.Exception is SqliteException ? MessageBoxImage.Warning : MessageBoxImage.Error);
            args.Handled = true;
        };

        var login = new LoginWindow();
        MainWindow = login;
        login.Show();
        ShutdownMode = ShutdownMode.OnLastWindowClose;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _backupTimer?.Stop();
        testDatabase?.Dispose(); Service.Dispose();
        Log.CloseAndFlush();
        if (_ownsInstance) _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        if (_ownsLegacyInstance) _legacyInstance?.ReleaseMutex();
        _legacyInstance?.Dispose();
        base.OnExit(e);
    }
}
