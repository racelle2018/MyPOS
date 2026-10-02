using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyPos.Desktop.Views;

namespace MyPos.Desktop;

public partial class MainWindow : Window
{
    private readonly PosView _posView = new();

    public MainWindow()
    {
        InitializeComponent();
        WelcomeText.Text = $"{App.CurrentUser?.FullName} ({App.CurrentUser?.Role})";
        SettingsButton.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        ScreenHost.Content = _posView;
        SetActiveNav(NavPosButton);
    }

    private void SetActiveNav(Button active)
    {
        foreach (var button in new[] { NavPosButton, NavProductsButton, NavReportsButton, SettingsButton })
        {
            button.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
            button.Foreground = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
        }

        active.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
        active.Foreground = Brushes.White;
    }

    private void NavPos_Click(object sender, RoutedEventArgs e)
    {
        ScreenHost.Content = _posView;
        SetActiveNav(NavPosButton);
    }

    private void NavProducts_Click(object sender, RoutedEventArgs e)
    {
        ScreenHost.Content = new ProductCatalogView();
        SetActiveNav(NavProductsButton);
    }

    private void NavReports_Click(object sender, RoutedEventArgs e)
    {
        ScreenHost.Content = new DailySalesView();
        SetActiveNav(NavReportsButton);
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!Permissions.RequireAdmin("change settings")) return;
        ScreenHost.Content = new SettingsView();
        SetActiveNav(SettingsButton);
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        App.CurrentUser = null;
        new LoginWindow().Show();
        Close();
    }
}
