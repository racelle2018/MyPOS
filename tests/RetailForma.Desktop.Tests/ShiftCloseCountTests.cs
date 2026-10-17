using RetailForma.Desktop.Dialogs;
using Xunit;

namespace RetailForma.Desktop.Tests;

public class ShiftCloseCountTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    public void Empty_or_zero_quantity_is_valid_and_contributes_zero(string text)
    {
        var updates = 0;
        var row = new ShiftCloseDialog.CountRow(500m, () => updates++);
        row.CountText = "2";
        Assert.Equal(1000m, row.Amount);
        row.CountText = text;
        Assert.True(row.Valid);
        Assert.Equal("0", row.CountText);
        Assert.Equal(0, row.Count);
        Assert.Equal(0m, row.Amount);
        Assert.Equal(2, updates);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("2147483648")]
    public void Invalid_quantities_remain_rejected(string text)
    {
        var row = new ShiftCloseDialog.CountRow(100m, () => { }) { CountText = text };
        Assert.False(row.Valid);
    }
}
