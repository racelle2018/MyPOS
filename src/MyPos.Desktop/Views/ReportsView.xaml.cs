using System.Windows;
using System.Windows.Controls;

namespace MyPos.Desktop.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        AuditToggleButton.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        ShowDaily();
    }

    private void ShowDaily() { ReportHost.Content = new DailySalesView(); SetActive(DailyToggleButton); }
    private void ShowAudit() { if (!Permissions.RequireAdmin("view the audit log")) return; ReportHost.Content = new AuditLogView(); SetActive(AuditToggleButton); }
    private void SetActive(Button active)
    {
        foreach (var button in new[] { DailyToggleButton, AuditToggleButton })
            button.Tag = button == active ? "Active" : null;
    }
    private void DailyToggle_Click(object sender, RoutedEventArgs e) => ShowDaily();
    private void AuditToggle_Click(object sender, RoutedEventArgs e) => ShowAudit();
}
