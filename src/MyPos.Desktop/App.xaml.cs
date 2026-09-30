using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
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
        var dbPath = Path.Combine(folder, "mypos.db");

        var options = new DbContextOptionsBuilder<MyPosDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        Db = new MyPosDbContext(options);
        DbInitializer.Initialize(Db);

        new LoginWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Db.Dispose();
        base.OnExit(e);
    }
}