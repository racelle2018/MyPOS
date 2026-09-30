using System.Windows;

namespace MyPos.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WelcomeText.Text = $"Welcome, {App.CurrentUser?.FullName} ({App.CurrentUser?.Role})";
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        new LoginWindow().Show();
        Close();
    }
}