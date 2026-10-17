using System.Windows;
using RetailForma.Desktop.Dialogs;

namespace RetailForma.LicenseIssuer.Gui;

public partial class App : Application
{
    internal bool IsolatedTestHost { get; set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        if (IsolatedTestHost) return;
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            ActionDialog.Error("The operation failed. Check the output file and issuer ledger before retrying.\n" + args.Exception.Message, "License Generator", MainWindow);
            args.Handled = true;
        };
        MainWindow = new MainWindow(); MainWindow.Show();
    }
}
