using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using RetailForma.Core.Entities;
using RetailForma.Desktop.Controls;

namespace RetailForma.Desktop.Printing;

/// <summary>Proportional-font receipt layout; independent of native thermal encoding.</summary>
public static class ReceiptDocument
{
    public static FlowDocument Build(Sale sale, ReceiptOptions options, bool reprint = false,
        bool preview = false)
    {
        var font = Application.Current?.TryFindResource("PrintInterFontFamily") as FontFamily
            ?? AppearanceResources.ResolveFont("Inter");
        var document = new FlowDocument
        {
            PageWidth = options.Width == 48 ? 302 : 219,
            PageHeight = 1100,
            PagePadding = new Thickness(14, 20, 14, 20),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = font,
            FontSize = preview ? 12 : 10,
            Foreground = Brushes.Black
        };
        var lines = ReceiptPrinter.BuildLines(sale, options, reprint);
        var contentWidth = document.PageWidth - document.PagePadding.Left - document.PagePadding.Right;
        // Measured amounts share a real right-aligned column, never space padding.
        var rightWidth = lines.Where(l => l.RightText != null).Select(l => new FormattedText(
            l.RightText!, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(font, FontStyles.Normal, l.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal), document.FontSize, Brushes.Black, 1).WidthIncludingTrailingWhitespace)
            .DefaultIfEmpty(0).Max();
        rightWidth = Math.Min(contentWidth * 0.55, Math.Ceiling(rightWidth) + 2);
        foreach (var line in lines)
        {
            if (line.IsSeparator)
            {
                document.Blocks.Add(new Paragraph
                {
                    Margin = new Thickness(0, 3, 0, 3),
                    BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 0),
                    FontSize = 1, LineHeight = 1
                });
            }
            else if (line.RightText != null)
            {
                var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 1, 0, 1) };
                table.Columns.Add(new TableColumn { Width = new GridLength(contentWidth - rightWidth) });
                table.Columns.Add(new TableColumn { Width = new GridLength(rightWidth) });
                var row = new TableRow();
                row.Cells.Add(new TableCell(Paragraph(line.LeftText ?? "", TextAlignment.Left, !preview && line.Bold))
                    { Padding = new Thickness(0, 0, 6, 0) });
                row.Cells.Add(new TableCell(Paragraph(line.RightText, TextAlignment.Right, !preview && line.Bold))
                    { Padding = new Thickness(0) });
                var group = new TableRowGroup();
                group.Rows.Add(row);
                table.RowGroups.Add(group);
                document.Blocks.Add(table);
            }
            else
            {
                var paragraph = Paragraph(line.Text, line.Align switch
                {
                    ReceiptAlign.Center => TextAlignment.Center,
                    ReceiptAlign.Right => TextAlignment.Right,
                    _ => TextAlignment.Left
                }, !preview && (line.Bold || line.Big));
                paragraph.Margin = new Thickness(0, 1, 0, 1);
                if (line.Big && !preview) paragraph.FontSize = 14;
                document.Blocks.Add(paragraph);
            }
        }
        if (!preview && options.ShowQr && sale.SaleNumber != 0)
            document.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Image
            {
                Source = ReceiptPrinting.CreateQrImage(sale), Width = 112, Height = 112
            }) { TextAlignment = TextAlignment.Center });
        return document;
    }

    private static Paragraph Paragraph(string text, TextAlignment alignment, bool bold) => new(new Run(text))
    {
        Margin = new Thickness(0), TextAlignment = alignment,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
    };
}
