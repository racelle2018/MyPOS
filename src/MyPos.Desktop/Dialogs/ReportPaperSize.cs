using System.Printing;

namespace MyPos.Desktop.Dialogs;

public sealed record ReportPaperSize(string Label, double Width, double Height, PageMediaSizeName? MediaName)
{
    // WPF and PrintTicket dimensions use 96 device-independent pixels per inch.
    public static IReadOnlyList<ReportPaperSize> Options { get; } =
    [
        new("Long — 8.5 × 13 in", 8.5 * 96, 13 * 96, null),
        new("Short / Letter — 8.5 × 11 in", 8.5 * 96, 11 * 96, PageMediaSizeName.NorthAmericaLetter),
        new("A4 — 210 × 297 mm", 210 / 25.4 * 96, 297 / 25.4 * 96, PageMediaSizeName.ISOA4)
    ];

    public PageMediaSize MediaSize => MediaName.HasValue
        ? new PageMediaSize(MediaName.Value, Width, Height)
        : new PageMediaSize(Width, Height);
    public override string ToString() => Label;
}
