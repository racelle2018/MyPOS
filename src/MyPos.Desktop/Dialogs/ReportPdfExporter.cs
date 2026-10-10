using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace MyPos.Desktop.Dialogs;

/// <summary>Export the paginated report without a printer, preserving its WPF layout.</summary>
public static class ReportPdfExporter
{
    public static void Export(FlowDocument document, Stream output)
    {
        document.Dispatcher.VerifyAccess();
        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.ComputePageCount();
        using var pdf = new PdfDocument();
        pdf.Info.Title = "MyPos Daily Sales Report";
        pdf.Info.Creator = "MyPos";
        const double dpi = 300;
        for (var index = 0; index < paginator.PageCount; index++)
        {
            var source = paginator.GetPage(index);
            if (source == DocumentPage.Missing) throw new InvalidOperationException("A report page could not be generated.");
            var size = source.Size;
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                var bounds = new Rect(size);
                drawing.DrawRectangle(Brushes.White, null, bounds);
                var brush = new VisualBrush(source.Visual)
                {
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = bounds,
                    Stretch = Stretch.Fill
                };
                drawing.DrawRectangle(brush, null, bounds);
            }
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * dpi / 96),
                (int)Math.Ceiling(size.Height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var imageStream = new MemoryStream();
            encoder.Save(imageStream);
            imageStream.Position = 0;
            using var image = XImage.FromStream(imageStream);
            var page = pdf.AddPage();
            page.Width = XUnit.FromPoint(size.Width * 72 / 96);
            page.Height = XUnit.FromPoint(size.Height * 72 / 96);
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
        }
        pdf.Save(output, false);
    }

    public static void Save(FlowDocument document, string path)
    {
        // Render completely before touching the destination; replace only after a successful write.
        using var output = new MemoryStream();
        Export(document, output);
        var destination = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".mypos-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, output.ToArray());
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
