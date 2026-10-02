using System.Windows;
using MyPos.Desktop.Views;

namespace MyPos.Desktop;

public partial class MainWindow : Window
{
    private readonly PosView _posView = new();
    public MainWindow()
    {
        InitializeComponent();
        WelcomeText.Text = $"Welcome, {App.CurrentUser?.FullName} ({App.CurrentUser?.Role})";
        SettingsButton.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        ScreenHost.Content = _posView;
    }

    private void NavPos_Click(object sender, RoutedEventArgs e) => ScreenHost.Content = _posView;

    private void NavProducts_Click(object sender, RoutedEventArgs e)
        => ScreenHost.Content = new ProductCatalogView();

    private void NavReports_Click(object sender, RoutedEventArgs e)
        => ScreenHost.Content = new DailySalesView();

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("change settings")) return;
        ScreenHost.Content = new SettingsView();
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        App.CurrentUser = null;
        new LoginWindow().Show();
        Close();
    }
}
