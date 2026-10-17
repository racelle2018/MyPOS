namespace RetailForma.Core;

/// <summary>New business timestamps retain the existing wall-clock storage shape, fixed to Philippine time.</summary>
public static class BusinessClock
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
    public static DateTime Now => FromUtc(DateTimeOffset.UtcNow);
    public static DateTime Today => Now.Date;
    public static DateTime FromUtc(DateTimeOffset instant) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(instant, Zone).DateTime, DateTimeKind.Unspecified);
}
