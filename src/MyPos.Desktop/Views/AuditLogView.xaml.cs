using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyPos.Core.Entities;

namespace MyPos.Desktop.Views;

public partial class AuditLogView : UserControl
{
    private bool _loading;
    private List<AuditRowVM> _rows = new();
    private static readonly string[] SecurityActions = { "LoginFailed", "PermissionDenied" };
    private static readonly string[] SensitiveActions = { "VoidSale", "ReceiptReprint", "SettingChanged", "SetupCompleted", "UserCreate", "UserEdit", "UserActivate", "UserDeactivate", "ProductDeactivate", "ProductActivate" };

    public AuditLogView()
    {
        InitializeComponent();
        if (!Permissions.IsAdmin) throw new UnauthorizedAccessException("Only administrators may view the audit log.");
        LoadFilterChoices();
        SetRange(DateTime.Today.AddDays(-6), DateTime.Today);
    }

    public static bool IsSecurity(string action) => SecurityActions.Contains(action);
    public static bool IsSensitive(string action) => SensitiveActions.Contains(action);

    private void LoadFilterChoices()
    {
        var actions = App.Db.AuditLogs.Select(log => log.Action).Distinct().OrderBy(action => action).ToList();
        ActionCombo.ItemsSource = new[] { "ALL ACTIONS" }.Concat(actions).ToList(); ActionCombo.SelectedItem = "ALL ACTIONS";
        var users = App.Db.Users.ToDictionary(user => user.Id, user => user.Username);
        var usernames = App.Db.AuditLogs.ToList().Where(log => log.UserId != null).Select(log => users.GetValueOrDefault(log.UserId!.Value)).Where(name => name != null).Distinct().OrderBy(name => name).ToList();
        UserCombo.ItemsSource = new[] { "ALL USERS" }.Concat(usernames!).ToList(); UserCombo.SelectedItem = "ALL USERS";
    }

    private void SetRange(DateTime? from, DateTime? to)
    {
        _loading = true; FromPick.SelectedDate = from; ToPick.SelectedDate = to; _loading = false; LoadLogs();
    }

    private void LoadLogs()
    {
        if (FromPick.SelectedDate > ToPick.SelectedDate)
        {
            RangeErrorText.Text = "The start date must be on or before the end date.";
            AuditGrid.ItemsSource = Array.Empty<AuditRowVM>();
            _rows = new List<AuditRowVM>();
            CountText.Text = "0 entries";
            EmptyHint.Text = "Choose a valid date range.";
            EmptyHint.Visibility = Visibility.Visible;
            return;
        }

        RangeErrorText.Text = "";
        EmptyHint.Text = "No audit entries match these filters";
        var from = FromPick.SelectedDate?.Date ?? DateTime.MinValue;
        var toExclusive = ToPick.SelectedDate?.Date.AddDays(1) ?? DateTime.MaxValue;
        var users = App.Db.Users.ToDictionary(user => user.Id, user => user.Username);
        var logs = App.Db.AuditLogs.Where(log => log.Date >= from && log.Date < toExclusive).OrderByDescending(log => log.Date).ToList();
        var rows = logs.Select(log => new AuditRowVM(log, users.GetValueOrDefault(log.UserId ?? Guid.Empty))).ToList();
        var action = ActionCombo.SelectedItem as string; var user = UserCombo.SelectedItem as string; var term = SearchBox.Text.Trim();
        if (!string.IsNullOrEmpty(action) && action != "ALL ACTIONS") rows = rows.Where(row => row.Action == action).ToList();
        if (!string.IsNullOrEmpty(user) && user != "ALL USERS") rows = rows.Where(row => row.UserText == user).ToList();
        if (term.Length > 0) rows = rows.Where(row => row.Details.Contains(term, StringComparison.OrdinalIgnoreCase) || row.UserText.Contains(term, StringComparison.OrdinalIgnoreCase) || row.Action.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        _rows = rows; AuditGrid.ItemsSource = rows; CountText.Text = $"{rows.Count} entr{(rows.Count == 1 ? "y" : "ies")}"; EmptyHint.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FromPick_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) LoadLogs(); }
    private void ToPick_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) LoadLogs(); }
    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) LoadLogs(); }
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => LoadLogs();
    private void TodayButton_Click(object sender, RoutedEventArgs e) => SetRange(DateTime.Today, DateTime.Today);
    private void WeekButton_Click(object sender, RoutedEventArgs e) => SetRange(DateTime.Today.AddDays(-6), DateTime.Today);
    private void MonthButton_Click(object sender, RoutedEventArgs e) => SetRange(DateTime.Today.AddDays(-29), DateTime.Today);
    private void AllButton_Click(object sender, RoutedEventArgs e) => SetRange(null, null);

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0) { MessageBox.Show("Nothing to export with these filters.", "MyPos", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"AUDIT-LOG-{DateTime.Now:yyyyMMdd-HHmm}.csv" };
        if (dialog.ShowDialog() != true) return;
        var csv = new StringBuilder("Date,Time,User,Action,Entity,Details\n");
        foreach (var row in _rows) csv.AppendLine(string.Join(",", Quote(row.Log.Date.ToString("MM/dd/yyyy")), Quote(row.Log.Date.ToString("HH:mm:ss")), Quote(row.UserText), Quote(row.Action), Quote(row.Entity), Quote(row.Details)));
        File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
        MessageBox.Show($"Exported {_rows.Count} entries to:\n{dialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

public class AuditRowVM
{
    public AuditLog Log { get; }
    private readonly string? _username;
    public AuditRowVM(AuditLog log, string? username) { Log = log; _username = username; }
    public string DateText => Log.Date.ToString("MM/dd/yy  h:mm:ss tt");
    public string UserText => _username ?? "—";
    public string Action => Log.Action;
    public string Entity => Log.EntityName;
    public string Details => Log.Details ?? "";
    public Brush ActionBrush => AuditLogView.IsSecurity(Action) ? Brushes.Firebrick : AuditLogView.IsSensitive(Action) ? Brushes.DarkOrange : Brushes.DarkSlateGray;
}
