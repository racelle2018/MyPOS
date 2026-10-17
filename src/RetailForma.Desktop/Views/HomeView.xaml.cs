using System.Windows;
using System.Windows.Controls;

namespace RetailForma.Desktop.Views;

public partial class HomeView : UserControl
{
    public event Action<string>? NavigationRequested;

    public HomeView()
    {
        InitializeComponent();
        GreetingText.Text = $"Welcome, {App.CurrentUser?.FullName ?? "RetailForma user"}";
    }

    private void Workspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string destination }) NavigationRequested?.Invoke(destination);
    }
}
