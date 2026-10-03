using System.Globalization;
using System.Windows.Data;

namespace MyPos.Desktop.Converters;

public sealed class UpperTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString()?.ToUpperInvariant() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
