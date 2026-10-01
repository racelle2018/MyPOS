using System.Windows;
using MyPos.Desktop.Views;

namespace MyPos.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WelcomeText.Text = $"Welcome, {App.CurrentUser?.FullName} ({App.CurrentUser?.Role})";
        ScreenHost.Content = new ProductCatalogView();   // default screen
    }

    private void NavProducts_Click(object sender, RoutedEventArgs e)
        => ScreenHost.Content = new ProductCatalogView();

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        App.CurrentUser = null;
        new LoginWindow().Show();
        Close();
    }
}