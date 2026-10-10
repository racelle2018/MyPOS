using System.Windows.Controls;

namespace MyPos.Desktop.Views;

public partial class ReportsView : UserControl
{
    public string CurrentPageTitle => ReportHost.Content is AuditLogView ? "Audit Log" : "Daily Sales";

    public ReportsView(bool showAudit = false)
    {
        InitializeComponent();
        ReportHost.Content = showAudit && Permissions.IsAdmin
            ? new AuditLogView()
            : new DailySalesView();
    }
}
