using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RetailForma.Desktop.Dialogs;

/// <summary>Approved print/PDF business header, page metadata and sign-off.</summary>
public static class ReportDocumentChrome
{
    public static void AddHeader(FlowDocument document, string title, string? reportDate = null)
    {
        void Detail(string text, bool company = false)
        {
            document.Blocks.Add(new Paragraph(new Run(text.Trim()))
            {
                FontSize = 10, FontWeight = company ? FontWeights.Bold : FontWeights.Normal,
                TextAlignment = TextAlignment.Left, Margin = new Thickness(0, 0, 190, 0),
                KeepTogether = true, KeepWithNext = true
            });
        }
        Detail(AppSettings.Get("CompanyName", "MY STORE"), company: true);
        foreach (var text in new[] { AppSettings.Get("BranchName", ""), AppSettings.Get("CompanyAddress", ""), reportDate })
            if (!string.IsNullOrWhiteSpace(text)) Detail(text);
        var heading = new Paragraph(new Run(title) { FontWeight = FontWeights.Bold })
        {
            FontSize = 14, TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 28), KeepTogether = true, KeepWithNext = true
        };
        document.Blocks.Add(heading);
        ReportPagination.SetTopRightMetadata(document, RetailForma.Core.BusinessClock.Now.ToString("MM/dd/yyyy"));
    }

    public static void AddFooter(FlowDocument document, bool includeReviewers = false)
    {
        var footer = new Paragraph
        {
            KeepTogether = true, FontSize = 10, TextAlignment = TextAlignment.Left,
            Margin = new Thickness(0, 32, 0, 0)
        };
        void Signature(string label)
        {
            footer.Inlines.Add(new Run(label + ": "));
            footer.Inlines.Add(new InlineUIContainer(new Border
            {
                Width = 90, Height = 12, HorizontalAlignment = HorizontalAlignment.Left,
                BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, 0, 1)
            }));
        }
        footer.Inlines.Add(new Run($"Prepared by: {App.CurrentUser?.FullName ?? "________________"}"));
        footer.Inlines.Add(new LineBreak());
        footer.Inlines.Add(new LineBreak());
        Signature("Signature");
        if (includeReviewers)
        {
            footer.Inlines.Add(new LineBreak());
            footer.Inlines.Add(new LineBreak());
            Signature("Checked by");
            footer.Inlines.Add(new Run("    "));
            Signature("Received by");
        }
        document.Blocks.Add(footer);
    }
}
