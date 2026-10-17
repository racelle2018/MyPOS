using System.Windows;

namespace RetailForma.Desktop.Dialogs;

/// <summary>A close-only report window that owns its dialog resource scope.</summary>
public partial class ReportHostWindow : Window
{
    public ReportHostWindow(string title, FrameworkElement report)
    {
        InitializeComponent();
        Title = title;
        Content = report;
    }
}
