using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Serilog;
using MyPos.Core.Data;
using MyPos.Core.Entities;

namespace MyPos.Desktop;

public partial class App : Application
{
    public static MyPosDbContext Db { get; private set; } = null!;
    public static User? CurrentUser { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyPos");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, "logs"));
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(folder, "logs", "mypos-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
        var dbPath = Path.Combine(folder, "mypos.db");

        var options = new DbContextOptionsBuilder<MyPosDbContext>()
            .UseSqlite($"Data Source={dbPath}", options => options.CommandTimeout(5))
            .Options;

        Db = new MyPosDbContext(options);
        DbInitializer.Initialize(Db);
        BackupService.RunIfDue();

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
        Db.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
