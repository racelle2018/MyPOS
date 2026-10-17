using System.Globalization;
using System.Windows.Data;

namespace RetailForma.Desktop.Controls;

// Render plain menu captions while the transparent AccessText registers mnemonics.
public sealed class MenuHeaderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value?.ToString() ?? "").Replace("_", "");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
