using System.Windows;
using RetailForma.Desktop.Dialogs;

namespace RetailForma.Setup;

public partial class App : Application
{
    internal bool IsolatedTestHost { get; set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        if (IsolatedTestHost) return;
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            ActionDialog.Error("The operation failed. Verify the selected database and existing accounts before retrying.", "Technician Setup", MainWindow);
            args.Handled = true;
        };
        MainWindow = new MainWindow(); MainWindow.Show();
    }
}
