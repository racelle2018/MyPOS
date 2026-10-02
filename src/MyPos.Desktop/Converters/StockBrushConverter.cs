using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MyPos.Desktop.Converters;

public sealed class StockBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var quantity = value is decimal decimalValue ? decimalValue : 0;
        return new SolidColorBrush(quantity <= 0
            ? Color.FromRgb(0xDC, 0x26, 0x26)
            : quantity <= 5
                ? Color.FromRgb(0xD9, 0x77, 0x06)
                : Color.FromRgb(0x0F, 0x17, 0x2A));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
