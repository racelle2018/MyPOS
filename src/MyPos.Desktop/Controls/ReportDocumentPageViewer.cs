using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using MyPos.Desktop.Dialogs;

namespace MyPos.Desktop.Controls;

/// <summary>Keep the paged viewer connected to the page hosted inside its scroll viewport.</summary>
public sealed class ReportDocumentPageViewer : FlowDocumentPageViewer
{
    public void RefreshReportPagination()
    {
        if (Document is not FlowDocument document) return;
        var paginator = ReportPagination.Create(document);
        foreach (var page in PageViews) page.DocumentPaginator = paginator;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        RefreshReportPagination();
    }
    protected override ReadOnlyCollection<DocumentPageView> GetPageViewsCollection(out bool changed)
    {
        var pages = base.GetPageViewsCollection(out changed);
        // ScrollViewer creates its visual content after the viewer collects template pages.
        // The named template page is already available and must participate in zoom/paging.
        if (Template.FindName("PreviewPage", this) is DocumentPageView page && !pages.Contains(page))
        {
            changed = true;
            return new ReadOnlyCollection<DocumentPageView>(pages.Append(page).ToList());
        }
        return pages;
    }
}
