using System.Globalization;
using MyPos.Desktop.Dialogs;
using Xunit;

namespace MyPos.Desktop.Tests;

public class QuantityEditDialogTests
{
    [Fact]
    public void Quantity_must_be_positive_numeric_and_within_available_stock()
    {
        Assert.False(QuantityEditDialog.TryValidateQuantity("", 3m, out _, out _));
        Assert.False(QuantityEditDialog.TryValidateQuantity("abc", 3m, out _, out _));
        Assert.False(QuantityEditDialog.TryValidateQuantity("0", 3m, out _, out _));
        Assert.False(QuantityEditDialog.TryValidateQuantity("-1", 3m, out _, out _));
        Assert.False(QuantityEditDialog.TryValidateQuantity("4", 3m, out _, out _));

        Assert.True(QuantityEditDialog.TryValidateQuantity("3", 3m, out var quantity, out var error));
        Assert.Equal(3m, quantity);
        Assert.Equal("", error);

        var fractional = 1.5m.ToString(CultureInfo.CurrentCulture);
        Assert.True(QuantityEditDialog.TryValidateQuantity(fractional, 3m, out quantity, out _));
        Assert.Equal(1.5m, quantity);
    }
}
