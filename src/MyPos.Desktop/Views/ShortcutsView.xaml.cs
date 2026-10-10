using System.Windows.Controls;
using System.Windows;

namespace MyPos.Desktop.Views;

public partial class ShortcutsView : UserControl
{
    public event EventHandler? CloseRequested;

    private void CloseHintButton_Click(object sender, RoutedEventArgs e)
        => CloseRequested?.Invoke(this, EventArgs.Empty);

    public ShortcutsView()
    {
        InitializeComponent();
        ShortcutsList.ItemsSource = KeyboardShortcutCatalog.Groups;
    }
}
