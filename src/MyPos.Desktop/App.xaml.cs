using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;

namespace MyPos.Desktop;

public partial class App : Application
{
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

        using (var db = new MyPosDbContext(options))
            DbInitializer.Initialize(db);

        // TODO next: login window before MainWindow
    }
}