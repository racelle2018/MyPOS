using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
namespace RetailForma.Desktop.Dialogs;

/// <summary>One print/PDF paginator: page identity, page count and continuation table headings.</summary>
public static class ReportPagination
{
    public static readonly DependencyProperty IdentityProperty = DependencyProperty.RegisterAttached(
        "Identity", typeof(string), typeof(ReportPagination), new PropertyMetadata(""));
    public static void SetIdentity(FlowDocument document, string identity) => document.SetValue(IdentityProperty, identity);
    public static string GetIdentity(FlowDocument document) => (string)document.GetValue(IdentityProperty);
    public static readonly DependencyProperty PlainTableHeadersProperty = DependencyProperty.RegisterAttached(
        "PlainTableHeaders", typeof(bool), typeof(ReportPagination), new PropertyMetadata(false));
    public static void SetPlainTableHeaders(FlowDocument document, bool value) => document.SetValue(PlainTableHeadersProperty, value);
    public static bool GetPlainTableHeaders(FlowDocument document) => (bool)document.GetValue(PlainTableHeadersProperty);
    public static readonly DependencyProperty TopRightMetadataProperty = DependencyProperty.RegisterAttached(
        "TopRightMetadata", typeof(string), typeof(ReportPagination), new PropertyMetadata(""));
    public static void SetTopRightMetadata(FlowDocument document, string value) => document.SetValue(TopRightMetadataProperty, value);
    public static string GetTopRightMetadata(FlowDocument document) => (string)document.GetValue(TopRightMetadataProperty);
    public static DynamicDocumentPaginator Create(FlowDocument document) => new DecoratedPaginator(document);

    private sealed class DecoratedPaginator : DynamicDocumentPaginator
    {
        private readonly FlowDocument _document;
        private readonly DynamicDocumentPaginator _inner;
        private readonly Dictionary<DocumentPage, DocumentPage> _sources = new();
        public DecoratedPaginator(FlowDocument document)
        {
            _document = document;
            _inner = (DynamicDocumentPaginator)((IDocumentPaginatorSource)document).DocumentPaginator;
            _inner.PagesChanged += (_, e) => { _sources.Clear(); OnPagesChanged(e); };
        }
        public override bool IsPageCountValid => _inner.IsPageCountValid;
        public override int PageCount => _inner.PageCount;
        public override Size PageSize { get => _inner.PageSize; set => _inner.PageSize = value; }
        public override IDocumentPaginatorSource Source => _inner.Source;
        public override int GetPageNumber(ContentPosition position) => _inner.GetPageNumber(position);
        public override ContentPosition GetObjectPosition(object value) => _inner.GetObjectPosition(value);
        public override ContentPosition GetPagePosition(DocumentPage page) =>
            _inner.GetPagePosition(_sources.GetValueOrDefault(page, page));
        public override void ComputePageCount() => _inner.ComputePageCount();
        public override DocumentPage GetPage(int pageNumber)
        {
            _inner.ComputePageCount();
            var source = _inner.GetPage(pageNumber);
            if (source == DocumentPage.Missing) return source;
            var size = source.Size; var padding = _document.PagePadding;
            var identity = GetIdentity(_document);
            if (identity.Length == 0)
            {
                var lines = _document.Blocks.OfType<Paragraph>().Select(p => new TextRange(p.ContentStart, p.ContentEnd).Text.Trim()).Where(s => s.Length > 0).Take(6);
                identity = string.Join(" | ", lines);
            }
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.White, null, new Rect(size));
                drawing.DrawRectangle(new VisualBrush(source.Visual) { Stretch = Stretch.Fill,
                    ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(size) }, null, new Rect(size));
                var width = Math.Max(1, size.Width - padding.Left - padding.Right);
                var topMetadata = GetTopRightMetadata(_document);
                var metadataWidth = Math.Min(180, width);
                void Text(string value, double x, double y, double maxWidth, double fontSize = 9, bool bold = false, bool right = false)
                {
                    var formatted = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface(_document.FontFamily, FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                        fontSize, Brushes.Black, 1) { MaxTextWidth = Math.Max(1, maxWidth), MaxTextHeight = 16,
                            TextAlignment = right ? TextAlignment.Right : TextAlignment.Left, Trimming = TextTrimming.CharacterEllipsis };
                    drawing.DrawText(formatted, new Point(x, y));
                }
                if (pageNumber > 0)
                {
                    Text(identity + " (continued)", padding.Left, 5,
                        topMetadata.Length > 0 ? Math.Max(1, width - metadataWidth - 10) : width, 9, true);
                    var position = _inner.GetPagePosition(source) as TextPointer;
                    DependencyObject? parent = position?.Parent as DependencyObject;
                    while (parent != null && parent is not Table) parent = LogicalTreeHelper.GetParent(parent);
                    var table = parent as Table;
                    var header = table?.RowGroups.FirstOrDefault()?.Rows.FirstOrDefault();
                    if (table != null && header != null && table.Columns.Count == header.Cells.Count)
                    {
                        var total = table.Columns.Sum(c => c.Width.Value);
                        var x = padding.Left;
                        for (var i = 0; i < header.Cells.Count; i++)
                        {
                            var cellWidth = total > 0 ? table.Columns[i].Width.Value / total * width : width / header.Cells.Count;
                            if (GetPlainTableHeaders(_document))
                            {
                                drawing.DrawLine(new Pen(Brushes.Black, 1), new Point(x, 24), new Point(x + cellWidth, 24));
                                drawing.DrawLine(new Pen(Brushes.Black, 1), new Point(x, 43), new Point(x + cellWidth, 43));
                            }
                            else
                                drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xEE, 0xF0, 0xF1)),
                                    new Pen(Brushes.LightGray, 1), new Rect(x, 24, cellWidth, 19));
                            Text(new TextRange(header.Cells[i].ContentStart, header.Cells[i].ContentEnd).Text.Trim(),
                                x + 3, 26, cellWidth - 6, 9, true);
                            x += cellWidth;
                        }
                    }
                }
                if (topMetadata.Length > 0)
                {
                    var x = size.Width - padding.Right - metadataWidth;
                    var y = pageNumber > 0 ? 0 : padding.Top;
                    var labelWidth = metadataWidth * 0.45;
                    var valueWidth = metadataWidth * 0.5;
                    var valueX = size.Width - padding.Right - valueWidth;
                    Text("Page No.", x, y, labelWidth, 10);
                    Text($"{pageNumber + 1}/{PageCount}", valueX, y, valueWidth, 10, right: true);
                    Text("Print Date", x, y + 12, labelWidth, 10);
                    Text(topMetadata, valueX, y + 12, valueWidth, 10, right: true);
                }
                else
                    Text($"Page {pageNumber + 1} of {PageCount}", size.Width - padding.Right - 100, size.Height - 29, 100, 9);
            }
            var result = new DocumentPage(visual, size, source.BleedBox, source.ContentBox);
            _sources[result] = source;
            return result;
        }
    }
}
