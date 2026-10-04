using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Serilog;
using MyPos.Core.Data;
using MyPos.Core.Entities;

namespace MyPos.Desktop;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private bool _ownsInstance;
    private DispatcherTimer? _backupTimer;
    public static MyPosDbContext Db { get; private set; } = null!;
    public static User? CurrentUser { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        _singleInstance = new Mutex(true, @"Local\MyPosDesktop", out _ownsInstance);
        if (!_ownsInstance)
        {
            MessageBox.Show("MyPos is already running. Return to the open window.",
                "MyPos", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyPos");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, "logs"));
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(folder, "logs", "mypos-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
        BackupService.ApplyPendingRestore();
        var dbPath = BackupService.DatabasePath;

        var options = new DbContextOptionsBuilder<MyPosDbContext>()
            .UseSqlite($"Data Source={dbPath}", options => options.CommandTimeout(5))
            .Options;

        Db = new MyPosDbContext(options);
        DbInitializer.Initialize(Db);
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
            System.Windows.MessageBox.Show(message, "MyPos", MessageBoxButton.OK,
                args.Exception is SqliteException ? MessageBoxImage.Warning : MessageBoxImage.Error);
            args.Handled = true;
        };

        new LoginWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _backupTimer?.Stop();
        if (Db != null) Db.Dispose();
        Log.CloseAndFlush();
        if (_ownsInstance) _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
