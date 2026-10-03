using System.Windows;
namespace MyPos.Desktop.Dialogs;
public partial class ShiftOpenDialog : Window
{
    public decimal OpeningFloat { get; private set; }
    public ShiftOpenDialog() { InitializeComponent(); FloatBox.Focus(); }
    private void SaveButton_Click(object sender, RoutedEventArgs e) { if (!decimal.TryParse(FloatBox.Text, out var value) || value < 0) { ErrorText.Text = "Enter a valid amount."; return; } OpeningFloat = value; DialogResult = true; }
}
