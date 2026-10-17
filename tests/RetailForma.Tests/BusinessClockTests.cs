using RetailForma.Core;

namespace RetailForma.Tests;

public sealed class BusinessClockTests
{
    [Theory]
    [InlineData("2026-10-11T15:59:59Z", 11, 23)]
    [InlineData("2026-10-11T16:00:00Z", 12, 0)]
    public void Philippine_business_day_is_independent_of_machine_timezone(string utc, int day, int hour)
    {
        var time = BusinessClock.FromUtc(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(day, time.Day);
        Assert.Equal(hour, time.Hour);
        Assert.Equal(DateTimeKind.Unspecified, time.Kind);
    }
}
