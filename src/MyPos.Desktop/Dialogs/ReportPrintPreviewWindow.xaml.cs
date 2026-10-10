using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Printing;
using System.Windows.Input;

namespace MyPos.Desktop.Dialogs;

public partial class ReportPrintPreviewWindow : Window
{
    private readonly FlowDocument _document;

    public ReportPrintPreviewWindow(FlowDocument document)
    {
        InitializeComponent();
        _document = document;
        PaperSizeBox.ItemsSource = ReportPaperSize.Options;
        PaperSizeBox.SelectedIndex = 2;
        ApplyPaperSize();
        DocViewer.Document = document;
        DocViewer.CommandBindings.Add(new CommandBinding(ApplicationCommands.Print,
            (_, args) => { PrintReport(); args.Handled = true; }));
    }

    private void PaperSizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_document != null) ApplyPaperSize();
    }

    private void ApplyPaperSize()
    {
        if (PaperSizeBox.SelectedItem is not ReportPaperSize paper) return;
        _document.PageWidth = paper.Width;
        _document.PageHeight = paper.Height;
        _document.PagePadding = new Thickness(48);
        _document.ColumnWidth = double.PositiveInfinity;
        var contentWidth = _document.PageWidth - 96;
        foreach (var table in _document.Blocks.OfType<Table>())
        {
            var total = table.Columns.Sum(column => column.Width.Value);
            if (total <= 0) continue;
            foreach (var column in table.Columns)
                column.Width = new GridLength(column.Width.Value / total * contentWidth);
            // Keep the sales table compact enough for portrait paper.
            if (table.Columns.Count > 5)
            {
                table.FontSize = 10;
                foreach (var cell in table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells))
                    cell.Padding = new Thickness(2, 3, 2, 3);
            }
        }
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        => DocViewer.SetCurrentValue(FlowDocumentPageViewer.ZoomProperty, Math.Max(DocViewer.MinZoom, DocViewer.Zoom - 10));

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        => DocViewer.SetCurrentValue(FlowDocumentPageViewer.ZoomProperty, Math.Min(DocViewer.MaxZoom, DocViewer.Zoom + 10));

    private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
    {
        DocViewer.SetCurrentValue(FlowDocumentPageViewer.ZoomProperty, 100d);
        ZoomPercentBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
    }

    private void CommitZoomInput()
    {
        var text = ZoomPercentBox.Text.Trim();
        if (text.EndsWith('%')) text = text[..^1].TrimEnd();
        if (int.TryParse(text, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var zoom))
            DocViewer.SetCurrentValue(FlowDocumentPageViewer.ZoomProperty,
                Math.Clamp((double)zoom, DocViewer.MinZoom, DocViewer.MaxZoom));
        // Invalid/empty input returns to the current zoom; keep the display binding intact.
        ZoomPercentBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
    }

    private void ZoomPercentBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => CommitZoomInput();

    private void ZoomPercentBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitZoomInput();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ZoomPercentBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            e.Handled = true;
        }
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        PrintReport();
    }

    private void SavePdfButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save daily sales report as PDF",
            Filter = "PDF document (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"Daily-Sales-{DateTime.Now:yyyy-MM-dd}.pdf"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            ReportPdfExporter.Save(_document, dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The PDF could not be saved.\n{ex.Message}", "Save PDF",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private void PrintReport()
    {
        try
        {
            var paper = (ReportPaperSize)PaperSizeBox.SelectedItem;
            var orientation = PageOrientation.Portrait;
            var dialog = new PrintDialog();
            var ticket = dialog.PrintTicket ?? new PrintTicket();
            ticket.PageOrientation = orientation;
            ticket.PageMediaSize = paper.MediaSize;
            dialog.PrintTicket = ticket;
            if (dialog.ShowDialog() != true) return;
            ticket = dialog.PrintTicket;
            ticket.PageOrientation = orientation;
            ticket.PageMediaSize = paper.MediaSize;
            var validated = dialog.PrintQueue.MergeAndValidatePrintTicket(dialog.PrintQueue.DefaultPrintTicket, ticket).ValidatedPrintTicket;
            var media = validated.PageMediaSize;
            if (media?.Width is not double width || media.Height is not double height ||
                Math.Abs(width - paper.Width) > 3 || Math.Abs(height - paper.Height) > 3 ||
                validated.PageOrientation != orientation)
            {
                MessageBox.Show("This printer does not support the selected paper size in portrait. Choose another paper size or printer to avoid clipped output.",
                    "Paper size not supported", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            dialog.PrintTicket = validated;
            dialog.PrintDocument(((IDocumentPaginatorSource)_document).DocumentPaginator,
                "MyPos Daily Sales Report");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The report could not be printed.\n{ex.Message}", "Print report",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
