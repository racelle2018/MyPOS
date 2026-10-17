using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using RetailForma.Desktop.Controls;
using RetailForma.Desktop.Views;
using RetailForma.Core.Entities;

namespace RetailForma.Desktop;

public partial class MainWindow : Window
{
    private PosView? _posView;
    private readonly IdleSessionGuard _idleGuard;
    private bool _loggingOut;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"{RetailForma.Core.ProductBrand.Name} {AppVersion.Current}";
        UpdateClock();
        _clock.Tick += (_, _) => UpdateClock();
        Loaded += (_, _) =>
        {
            UpdateClock(); _clock.Start();
            if (Application.Current is App { IsolatedTestHost: false } && Permissions.IsAdmin &&
                AppSettings.Get("SetupChecklistReviewed", "false") != "true") Navigate("setup");
        };
        WelcomeText.Text = $"{App.CurrentUser?.FullName} ({UserAccess.RoleName(App.CurrentUser!.Role)})";
        MasterFileMenuItem.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        UtilitiesMenuItem.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        LicensingMenuItem.Visibility = Permissions.IsSystemAdministrator ? Visibility.Visible : Visibility.Collapsed;
        VoidEventsMenuItem.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        AuditMenuItem.Visibility = Permissions.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        Navigate("home");

        _idleGuard = new IdleSessionGuard(TimeSpan.FromMinutes(30))
        {
            PendingCartCount = () => _posView?.CartCount ?? 0,
            OnSwitchUser = LogoutNow
        };
        _idleGuard.Start();
        Closed += (_, _) => { _idleGuard.Stop(); _clock.Stop(); };
        Deactivated += (_, _) => ShortcutsHint.Visibility = Visibility.Collapsed;
        PreviewMouseDown += (_, _) =>
        {
            if (ShortcutsHint.Visibility == Visibility.Visible && !ShortcutsHint.IsMouseOver)
                ShortcutsHint.Visibility = Visibility.Collapsed;
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && ShortcutsHint.Visibility == Visibility.Visible)
            {
                ShortcutsHint.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F12)
            {
                ShortcutsHint.Visibility = Visibility.Collapsed;
                e.Handled = true;
                _idleGuard.LockNow();
            }
        };
    }

    private void UpdateClock()
        => DateTimeText.Text = RetailForma.Core.BusinessClock.Now.ToString("dddd, MMMM d, yyyy · h:mm:ss tt");

    private void MenuRoute_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string destination })
        {
            e.Handled = true;
            Navigate(destination);
        }
    }

    private void Menu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Role: MenuItemRole.TopLevelHeader } menu
            || !ReferenceEquals(e.OriginalSource, menu)) return;
        if (menu.Template.FindName("PART_Popup", menu) is not Popup popup) return;

        // Explicitly anchor each dropdown to its own menu header, not the
        // window or a detached template container.
        popup.SetCurrentValue(Popup.PlacementTargetProperty, menu);
        popup.SetCurrentValue(Popup.PlacementProperty, PlacementMode.Bottom);
        popup.SetCurrentValue(Popup.HorizontalOffsetProperty, 0d);
        popup.SetCurrentValue(Popup.VerticalOffsetProperty, 0d);
    }

    private void Navigate(string destination)
    {
        if (destination == "shortcuts")
        {
            if (!Permissions.RequireAdmin("view system utilities")) return;
            ShortcutsHint.Visibility = ShortcutsHint.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
            return;
        }
        ShortcutsHint.Visibility = Visibility.Collapsed;
        UserControl view;
        string title;
        switch (destination)
        {
            case "home":
                var home = new HomeView();
                home.NavigationRequested += Navigate;
                view = home;
                title = "Home";
                break;
            case "pos":
                view = _posView ??= new PosView();
                title = "New Sale";
                break;
            case "products":
                view = new ProductCatalogView();
                title = "Products";
                break;
            case "reports":
            case "audit":
                if (destination == "audit" && !Permissions.RequireAdmin("view the audit log")) return;
                var reports = new ReportsView(showAudit: destination == "audit");
                view = reports;
                title = reports.CurrentPageTitle;
                break;
            case "shift":
                view = new ShiftView();
                title = "Shift Operations";
                break;
            case "reconciliation":
                view = new ShiftReconciliationView();
                title = "Shift / Cashier Reconciliation";
                break;
            case "cash-movements":
                view = new CashMovementsView();
                title = "Cash Movements";
                break;
            case "sales-summary":
                view = new ExpandedReportsView(ExpandedReportKind.SalesSummary); title = "Sales Summary";
                break;
            case "product-sales":
                view = new ExpandedReportsView(ExpandedReportKind.ProductSales); title = "Product Sales";
                break;
            case "void-events":
                if (!Permissions.RequireAdmin("review void events")) return;
                view = new ExpandedReportsView(ExpandedReportKind.VoidEvents); title = "Void Events";
                break;
            case "users":
                if (!Permissions.RequireAdmin("manage users")) return;
                view = new UserManagementView();
                title = "Users";
                break;
            case "settings":
                if (!Permissions.RequireAdmin("change settings")) return;
                view = new SettingsView();
                title = "General Settings";
                break;
            case "drawer-settings":
                if (!Permissions.RequireAdmin("configure cash drawers")) return;
                view = new CashDrawerSettingsView();
                title = "Drawer Access Settings";
                break;
            case "counting-settings":
                if (!Permissions.RequireAdmin("configure cash counting")) return;
                view = new CashCountingSettingsView();
                title = "Cash Counting Settings";
                break;
            case "appearance":
                if (!Permissions.RequireAdmin("change appearance")) return;
                view = new AppearanceSettingsView(); title = "Appearance";
                break;
            case "backup":
                if (!Permissions.RequireAdmin("manage backups")) return;
                view = new BackupRestoreView();
                title = "Backup & Restore";
                break;
            case "setup":
                if (!Permissions.RequireAdmin("review setup and recovery")) return;
                view = new SetupChecklistView(); title = "Setup & Recovery";
                break;
            case "licensing":
                if (!Permissions.IsSystemAdministrator) return;
                view = new LicensingView(); title = "Licensing"; break;
            default:
                return;
        }
        ScreenHost.Content = view;
        UpdateNavigationGuide(destination, title);
    }

    private void ShortcutsHint_CloseRequested(object? sender, EventArgs e)
        => ShortcutsHint.Visibility = Visibility.Collapsed;

    private void UpdateNavigationGuide(string destination, string title)
    {
        var parent = destination switch
        {
            "pos" or "products" or "shift" => "Sales & Inventory",
            "reports" or "audit" or "reconciliation" or "cash-movements" or "sales-summary" or "product-sales" or "void-events" => "Reports & Inquiry",
            "users" => "Master File",
            "settings" or "drawer-settings" or "counting-settings" or "appearance" or "backup" or "setup" or "licensing" or "shortcuts" => "System Utilities",
            _ => null
        };
        ViewTitleText.Text = parent == null ? title : $"{parent} › {title}";
        StatusSectionText.Text = title;
        foreach (var menu in MainMenu.Items.OfType<MenuItem>())
            menu.IsChecked = parent == null
                ? ReferenceEquals(menu, HomeMenuItem)
                : string.Equals(menu.Header?.ToString()?.Replace("_", ""), parent, StringComparison.Ordinal);
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardCart("Logging out")) return;
        LogoutNow();
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        _idleGuard.LockNow();
    }

    private void ThirdPartyNotices_Click(object sender, RoutedEventArgs e)
    {
        var root = System.IO.Path.Combine(AppContext.BaseDirectory, "Service", "Licenses");
        var notices = new List<string>();
        foreach (var name in new[] { "SQLCipher-LICENSE.md", "OpenSSL-LICENSE.txt" })
        {
            var path = System.IO.Path.Combine(root, name);
            if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length <= 262144)
                notices.Add(name + "\n\n" + System.IO.File.ReadAllText(path));
        }
        Dialogs.ActionDialog.Information(notices.Count == 0 ? "Third-party notices are supplied in the installed Service\\Licenses folder. SQLCipher Community and OpenSSL are used by the protected data service." : string.Join("\n\n", notices),
            "Third-party notices", this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_loggingOut && !ConfirmDiscardCart("Closing RetailForma"))
            e.Cancel = true;
        base.OnClosing(e);
    }

    private bool ConfirmDiscardCart(string action)
    {
        if (_posView is not { HasItems: true }) return true;

        var result = RetailForma.Desktop.Dialogs.ActionDialog.Show(
            $"This sale has {_posView.CartCount} item(s). {action} will discard the current sale. Continue?",
            "Discard current sale?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    private void LogoutNow()
    {
        _loggingOut = true;
        App.Service.EndSession();
        App.CurrentUser = null;
        new LoginWindow().Show();
        Close();
    }
}
