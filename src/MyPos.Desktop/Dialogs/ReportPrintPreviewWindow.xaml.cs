using System.Windows;
using System.Windows.Documents;

namespace MyPos.Desktop.Dialogs;

public partial class ReportPrintPreviewWindow : Window
{
    private readonly FlowDocument _document;

    public ReportPrintPreviewWindow(FlowDocument document)
    {
        InitializeComponent();
        _document = document;
        DocViewer.Document = document;
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Controls.PrintDialog();
        if (dialog.ShowDialog() == true)
        {
            dialog.PrintDocument(((IDocumentPaginatorSource)_document).DocumentPaginator,
                "MyPos Daily Sales Report");
        }
    }
}
