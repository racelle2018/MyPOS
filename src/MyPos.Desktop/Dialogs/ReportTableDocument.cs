using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MyPos.Core.Services;
namespace MyPos.Desktop.Dialogs;

public static class ReportTableDocument
{
    public static FlowDocument Build(ReportTableData data)
    {
        var doc = new FlowDocument { FontFamily = (FontFamily)System.Windows.Application.Current.FindResource("PrintInterFontFamily"),
            FontSize = 12, ColumnWidth = double.PositiveInfinity, PageWidth = 794, PageHeight = 1123, PagePadding = new Thickness(48) };
        void Text(string value, double size = 10, bool bold = false, bool center = false, double bottom = 4)
            => doc.Blocks.Add(new Paragraph(new Run(value)) { FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                TextAlignment = center ? TextAlignment.Center : TextAlignment.Left, Margin = new Thickness(0, 0, 0, bottom), KeepTogether = true });
        ReportDocumentChrome.AddHeader(doc, data.Title);
        Text(data.PresentationScope ?? data.Scope);
        var note = data.PresentationNote ?? data.Note;
        if (!data.NoteAfterTable && !string.IsNullOrWhiteSpace(note)) Text(note, 9);
        if (data.SummaryBeforeTable) Text(data.PresentationSummary ?? data.Summary, 11, true);
        if (data.Warning.Length > 0)
            doc.Blocks.Add(new Paragraph(new Run("Warning: " + data.Warning))
            {
                FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.FindResource("UiWarningBrush"),
                Margin = new Thickness(0, 4, 0, 4), KeepTogether = true
            });
        var table = new Table { FontSize = 10, CellSpacing = 0, Margin = new Thickness(0, 8, 0, 8) };
        var weights = data.Headers.Select((h, i) => data.NumericColumns.Contains(i) ? 75d :
            h.Contains("Product") || h.Contains("Reason") || h.Contains("Quantities") || h.Contains("Customer") ? 180d : 110d).ToArray();
        for (var i = 0; i < weights.Length; i++) table.Columns.Add(new TableColumn { Width = new GridLength(weights[i] / weights.Sum() * 698) });
        var group = new TableRowGroup(); table.RowGroups.Add(group);
        void Row(string[] values, bool header, bool subtotal = false)
        {
            var row = new TableRow { FontWeight = header || subtotal ? FontWeights.Bold : FontWeights.Normal,
                Background = header ? new SolidColorBrush(Color.FromRgb(0xEE, 0xF0, 0xF1)) : Brushes.White };
            for (var i = 0; i < values.Length; i++)
                row.Cells.Add(new TableCell(new Paragraph(new Run(!header && data.NumericColumns.Contains(i) && values[i].Contains('.') &&
                    decimal.TryParse(values[i], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                        ? amount.ToString("N2", CultureInfo.InvariantCulture) : values[i])) { Margin = new Thickness(0), KeepTogether = true,
                    TextAlignment = data.NumericColumns.Contains(i) ? TextAlignment.Right : TextAlignment.Left })
                    { Padding = new Thickness(3), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 2, 2) });
            group.Rows.Add(row);
        }
        Row(data.Headers, true);
        foreach (var row in data.Rows) Row(row.Cells, false, row.Kind is "Subtotal" or "Category");
        if (data.Rows.Count == 0) Row(data.Headers.Select((_, i) => i == 0 ? "No records" : "").ToArray(), false);
        doc.Blocks.Add(table);
        if (data.RepeatSummary) Text(data.PresentationSummary ?? data.Summary, 11, true);
        if (data.NoteAfterTable && !string.IsNullOrWhiteSpace(note)) Text(note, 9);
        ReportDocumentChrome.AddFooter(doc);
        ReportPagination.SetIdentity(doc, data.Title);
        return doc;
    }
}
