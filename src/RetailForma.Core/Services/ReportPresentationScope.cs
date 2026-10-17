using System.Globalization;

namespace RetailForma.Core.Services;

/// <summary>Print/PDF scope, separate from complete CSV metadata and query filtering.</summary>
public static class ReportPresentationScope
{
    public static string Format(DateOnly from, DateOnly to,
        IEnumerable<(string Label, string? Value)> filters, string periodLabel = "Period")
    {
        var selected = filters.Where(f => !string.IsNullOrWhiteSpace(f.Value))
            .Select(f => $"{f.Label}: {f.Value!.Trim()}").ToList();
        var period = $"{periodLabel}: {from.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)} – {to.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)}";
        return period + (selected.Count > 0 ? "\nFilters: " + string.Join(" | ", selected) : "");
    }
}
