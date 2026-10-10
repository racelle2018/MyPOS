using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MaterialDesignThemes.Wpf;
using MyPos.Core.Data;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using MyPos.Desktop;
using MyPos.Desktop.Controls;
using MyPos.Desktop.Dialogs;
using MyPos.Desktop.Views;
using Xunit;

namespace MyPos.Desktop.Tests;

public class ThemeResourceTests
{
    [Fact]
    public void App_theme_loads_material_resources_and_mypos_overrides()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();

                Assert.NotNull(app.TryFindResource("MaterialDesignFlatButton"));
                Assert.NotNull(app.TryFindResource("MaterialDesignOutlinedTextBox"));
                Assert.NotNull(app.TryFindResource("MaterialDesignDataGrid"));
                Assert.Same(app.TryFindResource("MaterialDesignFlatMidBgButton"),
                    Assert.IsType<Style>(app.TryFindResource("MyPosButton")).BasedOn);
                Assert.NotNull(app.TryFindResource("MyPosButton"));
                Assert.NotNull(app.TryFindResource("PrimaryButton"));
                Assert.NotNull(app.TryFindResource("UiTextBrush"));
                Assert.NotNull(app.TryFindResource("UiMutedBrush"));
                Assert.NotNull(app.TryFindResource("UiAccentSoftBrush"));
                Assert.NotNull(app.TryFindResource("MyPosFocusVisual"));
                Assert.Equal(new Thickness(1), Assert.IsType<Thickness>(app.FindResource("UiBorderThickness")));
                Assert.Equal(1d, Assert.IsType<double>(app.FindResource("UiBorderWidth")));
                Assert.NotNull(app.TryFindResource("PageTitle"));
                Assert.NotNull(app.TryFindResource("FieldHint"));
                Assert.NotNull(app.TryFindResource(typeof(TextBox)));
                var underline = new Underline { IsActive = true };
                underline.Style = Assert.IsType<Style>(app.FindResource(typeof(Underline)));
                Assert.Equal(1d, underline.Height);
                var uiFont = Assert.IsType<FontFamily>(app.TryFindResource("UiFontFamily"));
                Assert.Contains("Wix Madefor Text", uiFont.Source);
                Assert.NotNull(Application.GetResourceStream(
                    new Uri("pack://application:,,,/MyPos.Desktop;component/Assets/Fonts/WixMadeforText-VariableFont_wght.ttf")));
                Assert.Contains(Fonts.GetFontFamilies(new Uri("pack://application:,,,/"),
                    "/MyPos.Desktop;component/Assets/Fonts/"), family => family.FamilyNames.Values.Contains("Wix Madefor Text"));

                var loginTheme = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MyPos.Desktop;component/Styles/LoginMaterial.xaml")
                };
                Assert.IsType<Style>(loginTheme["LoginMaterialTextBox"]);
                Assert.IsType<Style>(loginTheme["LoginMaterialPasswordBox"]);
                Assert.IsType<Style>(loginTheme["LoginMaterialButton"]);
                Assert.Null(app.TryFindResource("LoginMaterialButton"));

                using var connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                using var db = new MyPosDbContext(new DbContextOptionsBuilder<MyPosDbContext>()
                    .UseSqlite(connection).Options);
                db.Database.EnsureCreated();
                db.Branches.Add(new Branch { Name = "Test Branch" });
                db.Settings.Add(new Setting { Key = "VatRate", Value = "0.12" });
                var admin = new User
                {
                    Username = "admin",
                    FullName = "Test Administrator",
                    Role = UserRole.Admin
                };
                db.Users.Add(admin);
                db.SaveChanges();
                var dbProperty = typeof(App).GetProperty(nameof(App.Db))!;
                dbProperty.SetValue(null, db);
                try
                {
                    App.CurrentUser = admin;
                    var login = new LoginWindow();
                    var username = Assert.IsType<TextBox>(login.FindName("UsernameBox"));
                    var password = Assert.IsType<PasswordBox>(login.FindName("PasswordBox"));
                    Assert.Equal(280, username.Width);
                    Assert.Equal(280, password.Width);
                    Assert.Equal(48, username.Height);
                    Assert.Equal(48, password.Height);
                    Assert.Equal(14, username.FontSize);
                    Assert.Equal(14, password.FontSize);
                    Assert.Equal(new Thickness(12, 8, 12, 8), username.Padding);
                    Assert.Equal(new Thickness(12, 8, 12, 8), password.Padding);
                    Assert.True(HintAssist.GetIsFloating(username));
                    Assert.True(HintAssist.GetIsFloating(password));
                    Assert.Equal(new Thickness(1), username.BorderThickness);
                    Assert.Equal(new Thickness(1), password.BorderThickness);
                    Assert.Equal(new Thickness(1), TextFieldAssist.GetOutlinedBorderActiveThickness(username));
                    Assert.Equal(new Thickness(1), TextFieldAssist.GetOutlinedBorderActiveThickness(password));
                    username.Text = "MixedCase.Username";
                    Assert.Equal("MixedCase.Username", username.Text);
                    Assert.Equal(new CornerRadius(0), TextFieldAssist.GetTextFieldCornerRadius(username));
                    Assert.Equal(new CornerRadius(0), TextFieldAssist.GetTextFieldCornerRadius(password));
                    Assert.Equal(Colors.White, Assert.IsType<SolidColorBrush>(HintAssist.GetBackground(username)).Color);
                    Assert.Equal(Colors.White, Assert.IsType<SolidColorBrush>(HintAssist.GetBackground(password)).Color);
                    Assert.Equal(Colors.White,
                        Assert.IsType<SolidColorBrush>(login.FindResource("MaterialDesign.Brush.Background")).Color);
                    username.ApplyTemplate();
                    var hint = Assert.IsType<SmartHint>(username.Template.FindName("Hint", username));
                    hint.ApplyTemplate();
                    Assert.True(hint.UseFloating);
                    AssertTextInputFits(username);
                    AssertPasswordInputFits(password);
                    Assert.Null(login.FindName("ErrorText"));
                    Assert.IsType<Style>(login.FindResource("LoginMaterialButton"));
                    Assert.Equal(WindowStyle.SingleBorderWindow, login.WindowStyle);
                    Assert.Equal(ResizeMode.CanMinimize, login.ResizeMode);
                    Assert.Equal(WindowState.Normal, login.WindowState);
                    Assert.Equal(350, login.Width);
                    Assert.Equal(500, login.Height);
                    Assert.IsType<ScrollViewer>(login.Content);
                    Assert.Null(login.FindName("MinimizeButton"));
                    Assert.Null(login.FindName("MaximizeButton"));
                    Assert.Null(login.FindName("CloseButton"));

                    var mainWindow = new MainWindow();
                    var screenHost = Assert.IsType<ContentControl>(mainWindow.FindName("ScreenHost"));
                    Assert.IsType<HomeView>(screenHost.Content);
                    var navigationGuide = Assert.IsType<TextBlock>(mainWindow.FindName("ViewTitleText"));
                    Assert.Equal("Home", navigationGuide.Text);
                    var mainMenu = Assert.IsType<Menu>(mainWindow.FindName("MainMenu"));
                    var shellClock = Assert.IsType<TextBlock>(mainWindow.FindName("DateTimeText"));
                    var versionText = Assert.IsType<TextBlock>(mainWindow.FindName("VersionText"));
                    Assert.False(string.IsNullOrWhiteSpace(shellClock.Text));
                    Assert.Equal(11, shellClock.FontSize);
                    Assert.Same(shellClock.Parent, versionText.Parent);
                    Assert.Equal(1, Grid.GetColumn(shellClock));
                    Assert.Equal(2, Grid.GetColumn(versionText));
                    var destinations = mainMenu.Items.OfType<MenuItem>()
                        .Concat(mainMenu.Items.OfType<MenuItem>().SelectMany(item => item.Items.OfType<MenuItem>()))
                        .Where(item => item.Tag is string)
                        .ToDictionary(item => (string)item.Tag);
                    destinations["pos"].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert.Equal("Sales & Inventory › New Sale", navigationGuide.Text);
                    Assert.Equal("_Sales & Inventory", Assert.Single(mainMenu.Items.OfType<MenuItem>(), item => item.IsChecked).Header);
                    var retainedPos = Assert.IsType<PosView>(screenHost.Content);
                    var retainedCart = Assert.IsType<System.Collections.ObjectModel.ObservableCollection<CartLineVM>>(
                        Assert.IsType<DataGrid>(retainedPos.FindName("CartGrid")).ItemsSource);
                    var retainedLine = new CartLineVM(new Product { Name = "DRAFT ITEM", Price = 10m, StockQty = 30m }) { Qty = 1m };
                    retainedCart.Add(retainedLine);
                    var retainedCustomer = Assert.IsType<TextBox>(retainedPos.FindName("CustomerNameBox"));
                    retainedCustomer.Text = "CUSTOMER DRAFT";
                    destinations["home"].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert.IsType<HomeView>(screenHost.Content);
                    destinations["pos"].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert.Same(retainedPos, screenHost.Content);
                    Assert.Equal("CUSTOMER DRAFT", retainedCustomer.Text);
                    Assert.Contains(retainedLine, retainedCart);
                    retainedCart.Clear();
                    foreach (var (destination, viewType) in new[]
                    {
                        ("products", typeof(ProductCatalogView)), ("reports", typeof(ReportsView)),
                        ("audit", typeof(ReportsView)), ("shift", typeof(ShiftView)),
                        ("users", typeof(UserManagementView)), ("settings", typeof(SettingsView))
                    })
                    {
                        destinations[destination].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        Assert.Equal(viewType, screenHost.Content.GetType());
                    }
                    var pageBeforeHint = screenHost.Content;
                    destinations["shortcuts"].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    var shortcutsHint = Assert.IsType<ShortcutsView>(mainWindow.FindName("ShortcutsHint"));
                    Assert.Equal(Visibility.Visible, shortcutsHint.Visibility);
                    Assert.Equal(360, shortcutsHint.Width);
                    Assert.Same(pageBeforeHint, screenHost.Content);
                    Assert.Equal("System Utilities › Settings", navigationGuide.Text);
                    Assert.IsType<Button>(shortcutsHint.FindName("CloseHintButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(Visibility.Collapsed, shortcutsHint.Visibility);
                    destinations["shortcuts"].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert.Equal(Visibility.Visible, shortcutsHint.Visibility);
                    Assert.Same(destinations["settings"].Parent, destinations["shortcuts"].Parent);
                    Assert.True(Assert.IsType<MenuItem>(mainWindow.FindName("UtilitiesMenuItem")).IsChecked);
                    destinations["reports"].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert.Equal(Visibility.Collapsed, shortcutsHint.Visibility);
                    var reportPage = Assert.IsType<ReportsView>(screenHost.Content);
                    Assert.Equal("Reports & Inquiry › Daily Sales", navigationGuide.Text);
                    Assert.Null(reportPage.FindName("AuditToggleButton"));
                    Assert.Null(reportPage.FindName("DailyToggleButton"));
                    Assert.IsType<DailySalesView>(Assert.IsType<ContentControl>(reportPage.FindName("ReportHost")).Content);
                    destinations["audit"].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    var auditPage = Assert.IsType<ReportsView>(screenHost.Content);
                    Assert.IsType<AuditLogView>(Assert.IsType<ContentControl>(auditPage.FindName("ReportHost")).Content);
                    Assert.Equal("Reports & Inquiry › Audit Log", navigationGuide.Text);
                    destinations["reports"].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert.Equal("Reports & Inquiry › Daily Sales", navigationGuide.Text);
                    destinations["home"].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert.Equal(WindowState.Maximized, mainWindow.WindowState);
                    Assert.Same(destinations["home"], Assert.Single(mainMenu.Items.OfType<MenuItem>(), item => item.IsChecked));
                    Assert.Equal(480, mainWindow.MinHeight);
                    Assert.Equal(WindowStyle.SingleBorderWindow, mainWindow.WindowStyle);
                    Assert.Equal(ResizeMode.CanResize, mainWindow.ResizeMode);
                    Assert.Null(mainWindow.FindName("MinimizeWindowButton"));
                    Assert.Null(mainWindow.FindName("MaximizeWindowButton"));
                    Assert.Null(mainWindow.FindName("CloseWindowButton"));
                    mainWindow.WindowState = WindowState.Normal;
                    Assert.Equal(WindowState.Normal, mainWindow.WindowState);
                    mainWindow.WindowState = WindowState.Maximized;
                    Assert.Equal(WindowState.Maximized, mainWindow.WindowState);
                    mainWindow.Opacity = 0;
                    mainWindow.Show();
                    mainWindow.UpdateLayout();
                    var menuBorder = Assert.IsType<Border>(mainMenu.Parent);
                    Assert.Equal(new Thickness(0, 0, 0, 1), menuBorder.BorderThickness);
                    foreach (var header in mainMenu.Items.OfType<MenuItem>())
                    {
                        var headerBounds = header.TransformToAncestor(menuBorder)
                            .TransformBounds(new Rect(0, 0, header.ActualWidth, header.ActualHeight));
                        Assert.InRange(Math.Abs(headerBounds.Bottom -
                            (menuBorder.ActualHeight - menuBorder.BorderThickness.Bottom)), 0, 0.5);
                    }
                    var homeEdge = Assert.IsType<Border>(destinations["home"].Template.FindName("OpenMenuEdge", destinations["home"]));
                    Assert.Equal(Visibility.Visible, homeEdge.Visibility);
                    Assert.Equal(Color.FromRgb(0x28, 0x78, 0xBD), Assert.IsType<SolidColorBrush>(homeEdge.Background).Color);
                    foreach (var menu in mainMenu.Items.OfType<MenuItem>().Where(item => item.HasItems))
                    {
                        menu.ApplyTemplate();
                        Assert.Equal(MenuItemRole.TopLevelHeader, menu.Role);
                        var dropdown = Assert.IsType<System.Windows.Controls.Primitives.Popup>(
                            menu.Template.FindName("PART_Popup", menu));
                        Assert.Equal(System.Windows.Controls.Primitives.PlacementMode.Bottom, dropdown.Placement);
                        dropdown.Child.Opacity = 0;
                        menu.IsSubmenuOpen = true;
                        dropdown.GetBindingExpression(System.Windows.Controls.Primitives.Popup.IsOpenProperty)!
                            .UpdateTarget();
                        Assert.True(dropdown.IsOpen, $"{menu.Header}: submenu={menu.IsSubmenuOpen}, visible={menu.IsVisible}, loaded={menu.IsLoaded}");
                        Assert.Same(menu, dropdown.PlacementTarget);
                        var menuChrome = Assert.IsType<Border>(menu.Template.FindName("MenuChrome", menu));
                        var openEdge = Assert.IsType<Border>(menu.Template.FindName("OpenMenuEdge", menu));
                        Assert.Equal(Color.FromRgb(0x28, 0x78, 0xBD), Assert.IsType<SolidColorBrush>(menuChrome.Background).Color);
                        Assert.Equal(Colors.White, Assert.IsType<SolidColorBrush>(menu.Foreground).Color);
                        Assert.Equal(Visibility.Collapsed, openEdge.Visibility);
                        Assert.Equal(Visibility.Visible, homeEdge.Visibility);
                        Assert.Same(destinations["home"], Assert.Single(mainMenu.Items.OfType<MenuItem>(), item => item.IsChecked));
                        Assert.Equal(1, openEdge.Height);
                        Assert.Equal(Color.FromRgb(0x28, 0x78, 0xBD), Assert.IsType<SolidColorBrush>(openEdge.Background).Color);
                        var popupContent = Assert.IsAssignableFrom<FrameworkElement>(dropdown.Child);
                        popupContent.UpdateLayout();
                        foreach (var submenu in menu.Items.OfType<MenuItem>().Where(item => item.IsEnabled && item.IsVisible))
                        {
                            submenu.ApplyTemplate();
                            // Exercise the same state set by mouse/keyboard navigation.
                            typeof(MenuItem).GetProperty(nameof(MenuItem.IsHighlighted))!.SetValue(submenu, true);
                            var submenuChrome = Assert.IsType<Border>(submenu.Template.FindName("MenuChrome", submenu));
                            Assert.Equal(Color.FromRgb(0x28, 0x78, 0xBD),
                                Assert.IsType<SolidColorBrush>(submenuChrome.Background).Color);
                            Assert.Equal(Colors.White,
                                Assert.IsType<SolidColorBrush>(submenu.Foreground).Color);
                            typeof(MenuItem).GetProperty(nameof(MenuItem.IsHighlighted))!.SetValue(submenu, false);
                            Assert.Equal(Colors.Transparent, Assert.IsType<SolidColorBrush>(submenuChrome.Background).Color);
                            Assert.Equal(Color.FromRgb(0x1F, 0x29, 0x37), Assert.IsType<SolidColorBrush>(submenu.Foreground).Color);
                        }
                        var origin = menu.PointToScreen(new Point(0, menu.ActualHeight));
                        var dropdownOrigin = popupContent.PointToScreen(new Point(0, 0));
                        Assert.InRange(Math.Abs(dropdownOrigin.X - origin.X), 0, 2);
                        Assert.InRange(Math.Abs(dropdownOrigin.Y - origin.Y), 0, 2);
                        menu.IsSubmenuOpen = false;
                        dropdown.GetBindingExpression(System.Windows.Controls.Primitives.Popup.IsOpenProperty)!
                            .UpdateTarget();
                        Assert.Equal(Visibility.Collapsed, openEdge.Visibility);
                        dropdown.Child.Opacity = 1;
                    }
                    var hwnd = new WindowInteropHelper(mainWindow).Handle;
                    Assert.True(GetClientRect(hwnd, out var clientRect),
                        $"GetClientRect failed for hwnd={hwnd}; last error={Marshal.GetLastWin32Error()}.");
                    var clientBottom = new WindowPoint { X = clientRect.Right, Y = clientRect.Bottom };
                    Assert.True(ClientToScreen(hwnd, ref clientBottom));
                    var dpi = VisualTreeHelper.GetDpi(mainWindow).DpiScaleY;
                    Assert.True(clientBottom.Y <= SystemParameters.WorkArea.Bottom * dpi + 1,
                        $"The maximized content ends at {clientBottom.Y}px, " +
                        $"below the taskbar-safe work area ({SystemParameters.WorkArea.Bottom * dpi:0.#}px).");
                    mainWindow.WindowState = WindowState.Normal;
                    Assert.Equal(WindowState.Normal, mainWindow.WindowState);
                    mainWindow.WindowState = WindowState.Maximized;
                    Assert.Equal(WindowState.Maximized, mainWindow.WindowState);
                    Assert.True(GetClientRect(hwnd, out clientRect));
                    clientBottom = new WindowPoint { X = clientRect.Right, Y = clientRect.Bottom };
                    Assert.True(ClientToScreen(hwnd, ref clientBottom));
                    Assert.True(clientBottom.Y <= SystemParameters.WorkArea.Bottom * dpi + 1,
                        "Restoring and maximizing again must not cover the taskbar.");
                    foreach (var (reviewWidth, reviewHeight) in new[] { (1274, 690), (760, 430) })
                    {
                        mainWindow.WindowState = WindowState.Normal;
                        mainWindow.Width = reviewWidth;
                        mainWindow.Height = reviewHeight;
                        mainWindow.UpdateLayout();
                        var shell = Assert.IsAssignableFrom<FrameworkElement>(mainWindow.Content);
                        shell.UpdateLayout();
                        Assert.True(mainMenu.Items.OfType<MenuItem>().Where(item => item.Visibility == Visibility.Visible)
                            .Sum(item => item.ActualWidth) <= mainMenu.ActualWidth);
                        var clockBounds = shellClock.TransformToAncestor(shell).TransformBounds(new Rect(shellClock.RenderSize));
                        var versionBounds = versionText.TransformToAncestor(shell).TransformBounds(new Rect(versionText.RenderSize));
                        Assert.True(clockBounds.Left >= 0 && clockBounds.Right <= versionBounds.Left);
                        Assert.True(versionBounds.Right <= shell.ActualWidth && versionBounds.Bottom <= shell.ActualHeight);
                    }
                    mainWindow.Close();
                    App.CurrentUser = new User { Username = "cashier", FullName = "Cashier", Role = UserRole.Cashier };
                    var cashierWindow = new MainWindow();
                    Assert.Equal(Visibility.Collapsed, Assert.IsType<MenuItem>(cashierWindow.FindName("MasterFileMenuItem")).Visibility);
                    Assert.Equal(Visibility.Collapsed, Assert.IsType<MenuItem>(cashierWindow.FindName("UtilitiesMenuItem")).Visibility);
                    Assert.Equal(Visibility.Collapsed, Assert.IsType<MenuItem>(cashierWindow.FindName("AuditMenuItem")).Visibility);
                    var cashierReports = new ReportsView(showAudit: true);
                    Assert.IsType<DailySalesView>(Assert.IsType<ContentControl>(cashierReports.FindName("ReportHost")).Content);
                    cashierWindow.Close();
                    App.CurrentUser = admin;
                    login.Close();

                    var shift = new ShiftView();
                    Assert.NotNull(shift.FindName("ShiftsGrid"));
                    Assert.NotNull(shift.FindName("ExpectedText"));

                    var pos = new PosView();
                    var headerFields = Assert.IsType<Grid>(pos.FindName("HeaderFields"));
                    Assert.Equal(3, headerFields.ColumnDefinitions.Count);
                    Assert.True(double.IsNaN(headerFields.Width));
                    Assert.Equal(GridUnitType.Star, headerFields.ColumnDefinitions[0].Width.GridUnitType);
                    Assert.True(double.IsNaN(Assert.IsType<Border>(pos.FindName("TotalCard")).Width));
                    var invoice = Assert.IsType<TextBox>(pos.FindName("InvoiceBox"));
                    Assert.Equal(34, invoice.Height);
                    Assert.Contains(invoice.Style.Triggers.OfType<Trigger>(), trigger =>
                        trigger.Property == UIElement.IsMouseOverProperty &&
                        Equals(trigger.Value, true) &&
                        trigger.Setters.OfType<Setter>().Any(setter =>
                            setter.Property == ToolTipService.IsEnabledProperty && Equals(setter.Value, true)));
                    Assert.Contains(invoice.Style.Setters.OfType<Setter>(), setter =>
                        setter.Property == ToolTipService.IsEnabledProperty && Equals(setter.Value, false));
                    Assert.False(HintAssist.GetIsFloating(invoice));
                    Assert.Equal("Enter invoice / OR number", HintAssist.GetHint(invoice));
                    Assert.Equal(Colors.White,
                        Assert.IsType<SolidColorBrush>(invoice.Background).Color);
                    Assert.Null(pos.FindName("InvoiceErrorText"));
                    var invoiceLabel = Assert.IsType<TextBlock>(pos.FindName("InvoiceLabel"));
                    var requiredWord = invoiceLabel.Inlines.OfType<System.Windows.Documents.Run>()
                        .Single(run => run.Text == "Required");
                    Assert.Equal(FontStyles.Italic, requiredWord.FontStyle);
                    Assert.Same(pos.FindResource("UiDangerBrush"), requiredWord.Foreground);
                    Assert.Equal(24, invoice.MaxLength);
                    Assert.Equal(Visibility.Collapsed,
                        TextFieldAssist.GetCharacterCounterVisibility(invoice));
                    Assert.Equal(CharacterCasing.Upper, invoice.CharacterCasing);
                    AssertTextInputFits(invoice);
                    foreach (var (fieldName, expectedHint) in new[]
                    {
                        ("CustomerNameBox", "Enter customer name"),
                        ("AddressBox", "Enter customer address")
                    })
                    {
                        var field = Assert.IsType<TextBox>(pos.FindName(fieldName));
                        Assert.Equal(invoice.Height, field.Height);
                        Assert.True(field.Margin.Top >= 0);
                        Assert.Equal(expectedHint, HintAssist.GetHint(field));
                        Assert.False(HintAssist.GetIsFloating(field));
                        Assert.Equal(CharacterCasing.Upper, field.CharacterCasing);
                        Assert.Equal(new Thickness(1), TextFieldAssist.GetOutlinedBorderActiveThickness(field));
                        AssertTextInputFits(field);
                        var fieldHint = Assert.IsType<SmartHint>(field.Template.FindName("Hint", field));
                        fieldHint.ApplyTemplate();
                        Assert.False(fieldHint.UseFloating);
                        Assert.Equal(Colors.White,
                            Assert.IsType<SolidColorBrush>(field.Background).Color);
                    }
                    AssertPosLayoutFits(pos, 1366, 768);
                    AssertPosLayoutFits(pos, 1093, 614); // 1366x768 at 125% scaling
                    AssertPosLayoutFits(pos, 911, 512); // 1366x768 at 150% scaling
                    AssertPosLayoutFits(pos, 911, 395); // 150% scaling after window chrome
                    Assert.Equal(Visibility.Collapsed,
                        Assert.IsType<TextBlock>(pos.FindName("SaleDetailsTitle")).Visibility);
                    AssertPosLayoutFits(pos, 1024, 680);
                    Assert.Equal(Visibility.Visible,
                        Assert.IsType<TextBlock>(pos.FindName("SaleDetailsTitle")).Visibility);
                    AssertPosLayoutFits(pos, 840, 460);
                    AssertPosLayoutFits(pos, 720, 460);
                    foreach (var gridName in new[] { "ProductsGrid", "CartGrid" })
                    {
                        var grid = Assert.IsType<DataGrid>(pos.FindName(gridName));
                        grid.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, grid));
                        var viewer = Assert.IsType<ScrollViewer>(grid.Template.FindName("DG_ScrollViewer", grid));
                        var scrollbar = Assert.IsType<System.Windows.Controls.Primitives.ScrollBar>(
                            viewer.Template.FindName("PART_VerticalScrollBar", viewer));
                        Assert.Equal(0, Grid.GetRow(scrollbar));
                        Assert.Equal(2, Grid.GetRowSpan(scrollbar));
                    }
                    foreach (var fieldName in new[] { "SearchBox", "DiscountBox" })
                        AssertTextInputFits(Assert.IsType<TextBox>(pos.FindName(fieldName)));
                    var loadedCart = Assert.IsType<DataGrid>(pos.FindName("CartGrid"));
                    var originalCartItems = loadedCart.ItemsSource;
                    loadedCart.ItemsSource = Enumerable.Range(0, 100)
                        .Select(index => new { ItemCode = $"A{index}", Name = "TEST ITEM", Qty = 1, Price = 10m, LineTotal = 10m });
                    AssertPosLayoutFits(pos, 1093, 582); // available work area at 125% scaling
                    AssertPosLayoutFits(pos, 911, 485); // available work area at 150% scaling
                    AssertPosLayoutFits(pos, 911, 395); // actual POS space inside the window
                    var discountBreakdown = Assert.IsType<Border>(pos.FindName("DiscountBreakdown"));
                    discountBreakdown.Visibility = Visibility.Visible;
                    AssertPosLayoutFits(pos, 911, 395);
                    discountBreakdown.Visibility = Visibility.Collapsed;
                    loadedCart.ItemsSource = originalCartItems;
                    Assert.Equal("Product Code", Assert.IsType<DataGrid>(pos.FindName("ProductsGrid")).Columns[0].Header);
                    Assert.True(Assert.IsType<Border>(pos.FindName("ProductsPanel"))
                        .IsAncestorOf(Assert.IsType<TextBox>(pos.FindName("SearchBox"))));
                    Assert.True(Assert.IsType<Border>(pos.FindName("CartArea"))
                        .IsAncestorOf(Assert.IsType<Button>(pos.FindName("PayButton"))));
                    var cartGrid = Assert.IsType<DataGrid>(pos.FindName("CartGrid"));
                    var productGrid = Assert.IsType<DataGrid>(pos.FindName("ProductsGrid"));
                    var cartEmptyHint = Assert.IsType<TextBlock>(pos.FindName("CartEmptyHint"));
                    Assert.Equal(HorizontalAlignment.Center, cartEmptyHint.HorizontalAlignment);
                    Assert.Equal(new Thickness(0), cartEmptyHint.Margin);
                    Assert.Empty(Assert.IsType<Grid>(cartEmptyHint.Parent).ColumnDefinitions);
                    Assert.True(cartGrid.IsReadOnly);
                    Assert.Equal(5, cartGrid.Columns.Count);
                    var quantityColumn = Assert.IsType<System.Windows.Controls.DataGridTextColumn>(cartGrid.Columns[2]);
                    Assert.Equal("QTY", quantityColumn.Header);
                    Assert.Equal("Qty", Assert.IsType<Binding>(quantityColumn.Binding).Path.Path);
                    Assert.Equal("Pay (F2)", Assert.IsType<Button>(pos.FindName("PayButton")).Content);
                    Assert.Null(cartGrid.ToolTip);
                    var sharedRowStyle = Assert.IsType<Style>(pos.FindResource(typeof(DataGridRow)));
                    var sharedCellStyle = Assert.IsType<Style>(pos.FindResource(typeof(DataGridCell)));
                    Assert.Same(sharedRowStyle, cartGrid.RowStyle);
                    Assert.Same(sharedCellStyle, cartGrid.CellStyle);
                    Assert.Same(sharedRowStyle, productGrid.RowStyle);
                    Assert.Same(sharedCellStyle, productGrid.CellStyle);
                    // Selected rows are blue only while their own grid has keyboard focus.
                    Assert.Single(sharedRowStyle.Triggers.OfType<MultiDataTrigger>());
                    Assert.DoesNotContain(sharedRowStyle.Triggers.OfType<Trigger>(),
                        trigger => trigger.Property == DataGridRow.IsSelectedProperty);
                    var cellTemplate = Assert.IsType<ControlTemplate>(sharedCellStyle.Setters
                        .OfType<Setter>().Single(setter => setter.Property == Control.TemplateProperty).Value);
                    Assert.Single(cellTemplate.Triggers.OfType<MultiDataTrigger>());
                    foreach (var grid in new[] { cartGrid, productGrid })
                        Assert.All(grid.Columns, column => Assert.NotNull(
                            Assert.IsType<System.Windows.Controls.DataGridTextColumn>(column).ElementStyle));
                    var shiftPrompt = Assert.IsType<Border>(pos.FindName("ShiftPrompt"));
                    Assert.True(Assert.IsType<Border>(pos.FindName("CartArea"))
                        .IsAncestorOf(shiftPrompt));
                    Assert.True(shiftPrompt.IsAncestorOf(
                        Assert.IsType<Button>(pos.FindName("OpenShiftButton"))));
                    Assert.Equal(Visibility.Visible, shiftPrompt.Visibility);
                    Assert.False(Assert.IsType<Button>(pos.FindName("PayButton")).IsEnabled);
                    var customerDraft = Assert.IsType<TextBox>(pos.FindName("CustomerNameBox"));
                    customerDraft.Text = "TEST CUSTOMER";
                    new ShiftService(db).OpenShift(db.Branches.First().Id, admin.Id, 100m);
                    pos.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                    Assert.Equal(Visibility.Collapsed, shiftPrompt.Visibility);
                    Assert.Equal("TEST CUSTOMER", customerDraft.Text);
                    var discountKind = Assert.IsType<ComboBox>(pos.FindName("DiscountKindBox"));
                    var discountDetail = Assert.IsType<TextBox>(pos.FindName("DiscountBox"));
                    var discountLabel = Assert.IsType<TextBlock>(pos.FindName("DiscountDetailLabel"));
                    Assert.False(discountDetail.IsEnabled);
                    Assert.Equal(Visibility.Collapsed,
                        Assert.IsType<Border>(pos.FindName("DiscountBreakdown")).Visibility);
                    discountKind.SelectedIndex = 1;
                    Assert.True(discountDetail.IsEnabled);
                    Assert.Equal("DISCOUNT AMOUNT", discountLabel.Text);
                    Assert.Equal(CharacterCasing.Normal, discountDetail.CharacterCasing);
                    discountDetail.Text = "10";
                    discountKind.SelectedIndex = 2;
                    Assert.True(discountDetail.IsEnabled);
                    Assert.Equal("", discountDetail.Text);
                    Assert.Equal("SC/PWD ID *", discountLabel.Text);
                    Assert.Equal(CharacterCasing.Upper, discountDetail.CharacterCasing);
                    discountDetail.Text = "sc-Ab123";
                    Assert.Equal("SC-AB123", discountDetail.Text);
                    discountKind.SelectedIndex = 0;
                    Assert.False(discountDetail.IsEnabled);

                    var firstProduct = new Product { Barcode = "TEST-FIRST", Name = "FIRST TEST PRODUCT", Price = 10m };
                    db.Products.Add(firstProduct);
                    db.SaveChanges();
                    var productCatalog = new ProductCatalogView();
                    var selectedProductGrid = Assert.IsType<DataGrid>(productCatalog.FindName("ProductsGrid"));
                    Assert.Equal(firstProduct.Id, Assert.IsType<ProductRowVM>(selectedProductGrid.SelectedItem).Product.Id);
                    Assert.True(Assert.IsType<Button>(productCatalog.FindName("ReceiveStockButton")).IsEnabled);
                    Assert.True(Assert.IsType<Button>(productCatalog.FindName("EditButton")).IsEnabled);
                    Assert.True(Assert.IsType<Button>(productCatalog.FindName("DeactivateButton")).IsEnabled);
                    var refreshSearch = Assert.IsType<TextBox>(productCatalog.FindName("SearchBox"));
                    refreshSearch.Text = "TEST-FIRST";
                    firstProduct.Price = 25m;
                    firstProduct.StockQty = 30m;
                    db.SaveChanges();
                    Assert.IsType<Button>(productCatalog.FindName("RefreshButton"))
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal("TEST-FIRST", refreshSearch.Text);
                    var refreshedProduct = Assert.IsType<ProductRowVM>(selectedProductGrid.SelectedItem);
                    Assert.Equal(firstProduct.Id, refreshedProduct.Product.Id);
                    Assert.Equal(25m, refreshedProduct.Price);
                    Assert.Equal(30m, refreshedProduct.StockQty);
                    refreshSearch.Clear();
                    var doubleClickCatalog = new ProductCatalogView();
                    var doubleClickGrid = Assert.IsType<DataGrid>(doubleClickCatalog.FindName("ProductsGrid"));
                    doubleClickCatalog.Measure(new Size(1000, 570));
                    doubleClickCatalog.Arrange(new Rect(0, 0, 1000, 570));
                    doubleClickCatalog.UpdateLayout();
                    var editRow = Assert.IsType<DataGridRow>(doubleClickGrid.ItemContainerGenerator.ContainerFromIndex(0));
                    var doubleClickHandler = typeof(ProductCatalogView).GetMethod("ProductsGrid_MouseDoubleClick",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                    System.Windows.Input.MouseButtonEventArgs ProductDoubleClick(object source,
                        System.Windows.Input.MouseButton button = System.Windows.Input.MouseButton.Left)
                    {
                        var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, button)
                            { RoutedEvent = Control.MouseDoubleClickEvent };
                        ((UIElement)source).RaiseEvent(args);
                        return args;
                    }
                    App.CurrentUser = new User { Role = UserRole.Cashier };
                    var cashierDoubleClick = ProductDoubleClick(editRow);
                    doubleClickHandler.Invoke(doubleClickCatalog, [doubleClickGrid, cashierDoubleClick]);
                    Assert.False(cashierDoubleClick.Handled);
                    Assert.DoesNotContain(Application.Current.Windows.Cast<Window>(), window => window is ProductEditDialog);
                    App.CurrentUser = admin;
                    var emptyDoubleClick = ProductDoubleClick(doubleClickGrid);
                    doubleClickHandler.Invoke(doubleClickCatalog, [doubleClickGrid, emptyDoubleClick]);
                    Assert.False(emptyDoubleClick.Handled);
                    var rightDoubleClick = ProductDoubleClick(editRow, System.Windows.Input.MouseButton.Right);
                    doubleClickHandler.Invoke(doubleClickCatalog, [doubleClickGrid, rightDoubleClick]);
                    Assert.False(rightDoubleClick.Handled);
                    var closeEditorTimer = new System.Windows.Threading.DispatcherTimer
                        { Interval = TimeSpan.FromMilliseconds(50) };
                    closeEditorTimer.Tick += (_, _) =>
                    {
                        var editor = Application.Current.Windows.OfType<ProductEditDialog>().FirstOrDefault();
                        if (editor == null) return;
                        closeEditorTimer.Stop();
                        editor.Close();
                    };
                    closeEditorTimer.Start();
                    try
                    {
                        var adminDoubleClick = ProductDoubleClick(editRow);
                        Assert.IsType<DataGridRow>(ItemsControl.ContainerFromElement(doubleClickGrid,
                            adminDoubleClick.OriginalSource as DependencyObject));
                        doubleClickHandler.Invoke(doubleClickCatalog, [doubleClickGrid, adminDoubleClick]);
                        Assert.True(adminDoubleClick.Handled);
                        Assert.Same(editRow.Item, doubleClickGrid.SelectedItem);
                    }
                    finally { closeEditorTimer.Stop(); }
                    var dailySales = new DailySalesView();
                    Assert.Contains("#Inter", dailySales.FontFamily.Source);
                    var reportDate = Assert.IsType<DatePicker>(dailySales.FindName("DatePick"));
                    Assert.Contains("#Inter", reportDate.FontFamily.Source);
                    var previousDay = Assert.IsType<Button>(dailySales.FindName("PrevDayButton"));
                    var nextDay = Assert.IsType<Button>(dailySales.FindName("NextDayButton"));
                    Assert.Equal(DateTime.Today, reportDate.DisplayDateEnd);
                    Assert.Equal(DateTime.Today, reportDate.SelectedDate);
                    Assert.Equal(DatePickerFormat.Short, reportDate.SelectedDateFormat);
                    Assert.False(nextDay.IsEnabled);
                    previousDay.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(DateTime.Today.AddDays(-1), reportDate.SelectedDate);
                    Assert.True(nextDay.IsEnabled);
                    nextDay.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(DateTime.Today, reportDate.SelectedDate);
                    Assert.False(nextDay.IsEnabled);
                    reportDate.SelectedDate = DateTime.Today.AddDays(1);
                    Assert.Equal(DateTime.Today, reportDate.SelectedDate);
                    Assert.Null(dailySales.FindName("ReportDateHeading"));
                    Assert.Equal("₱0.00", Assert.IsType<TextBlock>(dailySales.FindName("AvgText")).Text);
                    Assert.Null(dailySales.FindName("SalesHelpText"));
                    Assert.Equal("0", Assert.IsType<TextBlock>(dailySales.FindName("VoidsText")).Text);
                    var reportLayout = Assert.IsType<Grid>(dailySales.Content);
                    Assert.Equal(4, reportLayout.RowDefinitions.Count);
                    var reconciliationPanel = Assert.IsType<Border>(dailySales.FindName("ReconciliationPanel"));
                    var salesPanel = Assert.IsType<Border>(dailySales.FindName("SalesPanel"));
                    Assert.Equal(new Thickness(1), salesPanel.BorderThickness);
                    Assert.Equal(new Thickness(8, 3, 10, 3), reconciliationPanel.Margin);
                    Assert.Equal(new Thickness(0), salesPanel.Margin);
                    Assert.Equal(new Thickness(0, 0, 0, 1),
                        Assert.IsType<Border>(dailySales.FindName("SalesHeaderDivider")).BorderThickness);
                    var salesHeader = Assert.IsType<Grid>(dailySales.FindName("SalesHeader"));
                    Assert.Same(salesHeader, reconciliationPanel.Parent);
                    var salesActions = Assert.IsType<WrapPanel>(dailySales.FindName("SalesActions"));
                    Assert.Equal(5, salesActions.Children.Count);
                    var salesGrid = Assert.IsType<DataGrid>(dailySales.FindName("SalesGrid"));
                    Assert.Contains("#Inter", salesGrid.FontFamily.Source);
                    dailySales.Measure(new Size(1200, 570));
                    dailySales.Arrange(new Rect(0, 0, 1200, 570));
                    dailySales.UpdateLayout();
                    Assert.Equal(28, previousDay.ActualWidth);
                    Assert.Equal(28, nextDay.ActualWidth);
                    Assert.InRange(reportDate.ActualWidth, 150, 162);
                    Assert.Same(Application.Current.FindResource("ReportDatePicker"), reportDate.Style);
                    Assert.Equal(12, reportDate.FontSize);
                    Assert.Equal(new Thickness(1), Assert.IsType<Border>(
                        reportDate.Template.FindName("PickerBorder", reportDate)).BorderThickness);
                    Assert.Equal(22, Assert.IsType<TextBlock>(dailySales.FindName("TotalSalesText")).FontSize);
                    Assert.Equal(18, Assert.IsType<TextBlock>(dailySales.FindName("NetSalesText")).FontSize);
                    var dateButton = Assert.IsType<Button>(
                        reportDate.Template.FindName("PART_Button", reportDate));
                    var dateTextBox = Assert.IsType<System.Windows.Controls.Primitives.DatePickerTextBox>(
                        reportDate.Template.FindName("PART_TextBox", reportDate));
                    Assert.Equal(10, dateTextBox.MaxLength); // Eight digits plus two slashes.
                    var datePopup = Assert.IsType<System.Windows.Controls.Primitives.Popup>(
                        reportDate.Template.FindName("PART_Popup", reportDate));
                    Assert.IsType<Calendar>(datePopup.Child);
                    Assert.IsType<ScrollViewer>(dateTextBox.Template.FindName("PART_ContentHost", dateTextBox));
                    var emptyDateWatermark = Assert.IsType<ContentControl>(
                        dateTextBox.Template.FindName("PART_Watermark", dateTextBox));
                    emptyDateWatermark.ApplyTemplate();
                    var datePlaceholder = Assert.IsType<TextBlock>(
                        VisualTreeHelper.GetChild(emptyDateWatermark, 0));
                    Assert.Equal("/ /", datePlaceholder.Text);
                    Assert.Equal(TextAlignment.Center, datePlaceholder.TextAlignment);
                    Assert.Equal(TextAlignment.Center, dateTextBox.TextAlignment);
                    reportDate.SelectedDate = null;
                    reportDate.UpdateLayout();
                    Assert.Equal(string.Empty, dateTextBox.Text);
                    Assert.Equal(1, emptyDateWatermark.Opacity);
                    reportDate.SelectedDate = DateTime.Today;
                    reportDate.UpdateLayout();
                    Assert.Equal(0, emptyDateWatermark.Opacity);
                    var typedDate = DateTime.Today.AddMonths(-2).Date;
                    dateTextBox.Text = typedDate.ToString("MMddyyyy");
                    Assert.Equal(typedDate, reportDate.SelectedDate);
                    Assert.Equal(typedDate.ToString("MM/dd/yyyy"), dateTextBox.Text);
                    Assert.False(ReportDatePickerBehavior.GetHasInputError(reportDate));
                    dateTextBox.Text = "41";
                    Assert.Equal("12/", dateTextBox.Text);
                    Assert.False(ReportDatePickerBehavior.GetHasInputError(reportDate));
                    Assert.Equal(typedDate, reportDate.SelectedDate);
                    dateTextBox.Text = "0532";
                    Assert.Equal("05/31/", dateTextBox.Text);
                    Assert.False(ReportDatePickerBehavior.GetHasInputError(reportDate));
                    dateTextBox.Text = "0555";
                    Assert.Equal("05/31/", dateTextBox.Text);
                    Assert.False(ReportDatePickerBehavior.GetHasInputError(reportDate));
                    dateTextBox.Text = "0431";
                    Assert.Equal("This month has only 30 days.",
                        ReportDatePickerBehavior.GetInputErrorMessage(reportDate));
                    dateTextBox.Text = "0230";
                    Assert.Equal("February has at most 29 days.",
                        ReportDatePickerBehavior.GetInputErrorMessage(reportDate));
                    dateTextBox.Text = "0229";
                    Assert.Equal("02/29/", dateTextBox.Text);
                    Assert.False(ReportDatePickerBehavior.GetHasInputError(reportDate));
                    dateTextBox.Text = "02292025";
                    Assert.Equal("February 29 is not valid in 2025.",
                        ReportDatePickerBehavior.GetInputErrorMessage(reportDate));
                    dateTextBox.Text = "01010000";
                    Assert.Equal("Year must be between 0001 and 9999.",
                        ReportDatePickerBehavior.GetInputErrorMessage(reportDate));
                    dateTextBox.Text = "0525";
                    Assert.False(ReportDatePickerBehavior.CommitDateInput(reportDate));
                    Assert.Equal("Complete the date as MM/DD/YYYY.",
                        ReportDatePickerBehavior.GetInputErrorMessage(reportDate));
                    Assert.Equal(typedDate, reportDate.SelectedDate);
                    dateTextBox.Text = string.Empty;
                    Assert.False(ReportDatePickerBehavior.CommitDateInput(reportDate));
                    Assert.Equal(typedDate, reportDate.SelectedDate);
                    Assert.Equal(typedDate.ToString("MM/dd/yyyy"), dateTextBox.Text);
                    Assert.Equal("A sales date is required.",
                        ReportDatePickerBehavior.GetInputErrorMessage(reportDate));
                    dateTextBox.Text = "02302025";
                    Assert.True(ReportDatePickerBehavior.GetHasInputError(reportDate));
                    Assert.Equal(typedDate, reportDate.SelectedDate);
                    Assert.Equal("February has at most 29 days.", dateTextBox.ToolTip);
                    var dateBorder = Assert.IsType<Border>(
                        reportDate.Template.FindName("PickerBorder", reportDate));
                    Assert.Equal(Assert.IsType<SolidColorBrush>(app.FindResource("UiDangerBrush")).Color,
                        Assert.IsType<SolidColorBrush>(dateBorder.BorderBrush).Color);
                    var dateWarning = Assert.IsType<System.Windows.Controls.Primitives.Popup>(
                        reportDate.Template.FindName("DateWarningPopup", reportDate));
                    Assert.Equal("February has at most 29 days.", dateWarning.Tag);
                    dateTextBox.Text = "22222222";
                    Assert.Equal("12/22/2222", dateTextBox.Text);
                    Assert.True(ReportDatePickerBehavior.GetHasInputError(reportDate));
                    Assert.Equal("That date is outside the allowed range.",
                        ReportDatePickerBehavior.GetInputErrorMessage(reportDate));
                    Assert.Equal(typedDate, reportDate.SelectedDate);
                    dateTextBox.Text = string.Empty;
                    foreach (var digit in "2222")
                        dateTextBox.AppendText(digit.ToString());
                    Assert.Equal("12/22/", dateTextBox.Text);
                    Assert.False(ReportDatePickerBehavior.GetHasInputError(reportDate));
                    foreach (var digit in "2222")
                        dateTextBox.AppendText(digit.ToString());
                    Assert.Equal("12/22/2222", dateTextBox.Text);
                    Assert.True(ReportDatePickerBehavior.GetHasInputError(reportDate));
                    Assert.Equal(typedDate, reportDate.SelectedDate);
                    dateTextBox.Text = "222222222";
                    Assert.Equal("12/22/2222", dateTextBox.Text);
                    Assert.Equal(typedDate, reportDate.SelectedDate);
                    dateTextBox.Text = DateTime.Today.AddDays(1).ToString("MMddyyyy");
                    Assert.True(ReportDatePickerBehavior.GetHasInputError(reportDate));
                    Assert.Equal(typedDate, reportDate.SelectedDate);
                    dateTextBox.Text = "02292024";
                    Assert.Equal(new DateTime(2024, 2, 29), reportDate.SelectedDate);
                    Assert.Equal("02/29/2024", dateTextBox.Text);
                    Assert.False(ReportDatePickerBehavior.GetHasInputError(reportDate));
                    dateTextBox.Text = "13552024";
                    Assert.Equal("12/31/2024", dateTextBox.Text);
                    Assert.Equal(new DateTime(2024, 12, 31), reportDate.SelectedDate);
                    dateTextBox.Text = string.Empty;
                    foreach (var digit in "05252026")
                        dateTextBox.AppendText(digit.ToString());
                    Assert.Equal(new DateTime(2026, 5, 25), reportDate.SelectedDate);
                    Assert.Equal("05/25/2026", dateTextBox.Text);
                    dateTextBox.Text = "052520266";
                    Assert.Equal("05/25/2026", dateTextBox.Text);
                    Assert.Equal(new DateTime(2026, 5, 25), reportDate.SelectedDate);
                    dateTextBox.AppendText("6");
                    Assert.Equal("05/25/2026", dateTextBox.Text);
                    reportDate.SelectedDate = DateTime.Today;
                    Assert.InRange(dateButton.ActualHeight, 24, reportDate.ActualHeight);
                    Assert.Equal(dateButton.ActualHeight, dateTextBox.ActualHeight);
                    dateButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    Assert.True(reportDate.IsDropDownOpen);
                    var selectedReportDate = DateTime.Today.AddDays(-1);
                    reportDate.SelectedDate = selectedReportDate;
                    dateTextBox.SelectAll();
                    Assert.True(dateTextBox.SelectionLength > 0);
                    typeof(DatePicker).GetMethod("OnCalendarClosed",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                        .Invoke(reportDate, new object[] { new RoutedEventArgs() });
                    Assert.Equal(0, dateTextBox.SelectionLength);
                    reportDate.IsDropDownOpen = false;
                    Assert.Equal(selectedReportDate, reportDate.SelectedDate);
                    Assert.True(salesGrid.Columns[2].Width.IsStar);
                    Assert.True(salesGrid.Columns[3].Width.IsStar);
                    Assert.True(salesGrid.Columns[4].Width.IsStar);
                    Assert.Equal(170, salesGrid.Columns[3].MinWidth);
                    Assert.True(salesGrid.ActualHeight > 250);
                    Assert.Equal(0, Grid.GetRow(salesActions));
                    Assert.Equal(HorizontalAlignment.Right, salesActions.HorizontalAlignment);
                    dailySales.Measure(new Size(800, 570));
                    dailySales.Arrange(new Rect(0, 0, 800, 570));
                    dailySales.UpdateLayout();
                    Assert.True(salesGrid.ActualWidth > 750);
                    Assert.True(salesGrid.ActualHeight > 250);
                    Assert.True(salesHeader.ActualWidth < 900);
                    Assert.Equal(1, Grid.GetRow(salesActions));
                    Assert.Equal(HorizontalAlignment.Left, salesActions.HorizontalAlignment);
                    var reprint = Assert.IsType<Button>(dailySales.FindName("ReprintReceiptButton"));
                    Assert.Contains("#Inter", reprint.FontFamily.Source);
                    Assert.Equal(FontWeights.Normal, reprint.FontWeight);
                    Assert.Equal(0, reprint.Padding.Top);
                    Assert.Equal(5, reprint.Margin.Top);
                    Assert.Equal(5, reprint.Margin.Bottom);
                    Assert.False(reprint.IsEnabled);
                    Assert.Equal("Print Report", Assert.IsType<Button>(dailySales.FindName("PrintButton")).Content);
                    var systemSale = new Sale { ReceiptType = ReceiptType.System, ReceiptNumber = "R000001" };
                    var manualSale = new Sale { ReceiptType = ReceiptType.Manual, ReceiptNumber = "OR-1" };
                    salesGrid.ItemsSource = new[] { new SaleRowVM(systemSale), new SaleRowVM(manualSale) };
                    salesGrid.SelectedIndex = 0;
                    Assert.True(reprint.IsEnabled);
                    salesGrid.SelectedIndex = 1;
                    Assert.False(reprint.IsEnabled);
                    salesGrid.SelectedIndex = 0;
                    systemSale.IsVoided = true;
                    salesGrid.SelectedIndex = 1;
                    salesGrid.SelectedIndex = 0;
                    Assert.False(reprint.IsEnabled);
                    var auditLog = new AuditLogView();
                    Assert.Contains("#Inter", auditLog.FontFamily.Source);
                    Assert.NotNull(auditLog.FindName("AuditGrid"));
                    var auditFrom = Assert.IsType<DatePicker>(auditLog.FindName("FromPick"));
                    var auditTo = Assert.IsType<DatePicker>(auditLog.FindName("ToPick"));
                    Assert.Equal(DateTime.Today, auditFrom.DisplayDateEnd);
                    Assert.Equal(DateTime.Today, auditTo.DisplayDateEnd);
                    Assert.False(ReportDatePickerBehavior.GetIsRequiredDate(auditFrom));
                    auditFrom.ApplyTemplate();
                    var auditFromText = Assert.IsType<System.Windows.Controls.Primitives.DatePickerTextBox>(
                        auditFrom.Template.FindName("PART_TextBox", auditFrom));
                    var originalAuditDate = auditFrom.SelectedDate;
                    auditFromText.Text = "12319999";
                    Assert.Equal("That date is outside the allowed range.",
                        ReportDatePickerBehavior.GetInputErrorMessage(auditFrom));
                    Assert.Equal(originalAuditDate, auditFrom.SelectedDate);
                    auditFrom.SelectedDate = null;
                    Assert.True(ReportDatePickerBehavior.CommitDateInput(auditFrom));
                    Assert.Null(auditFrom.SelectedDate);
                    var users = new UserManagementView();
                    Assert.NotNull(users.FindName("UsersGrid"));
                    foreach (var (view, gridName, actionNames) in new[]
                    {
                        ((UserControl)shift, "ShiftsGrid", new[] { "OpenShiftButton", "CashIOButton", "CloseShiftButton" }),
                        ((UserControl)users, "UsersGrid", new[] { "EditButton", "ToggleActiveButton", "AddButton" })
                    })
                    {
                        foreach (var (width, height) in new[] { (1200d, 570d), (700d, 430d) })
                        {
                            view.Measure(new Size(width, height));
                            view.Arrange(new Rect(0, 0, width, height));
                            view.UpdateLayout();
                            var table = Assert.IsType<DataGrid>(view.FindName(gridName));
                            Assert.True(table.ActualHeight >= table.ColumnHeaderHeight + table.RowHeight);
                            AssertInside(table, view, width, height);
                            foreach (var actionName in actionNames)
                            {
                                var action = Assert.IsType<Button>(view.FindName(actionName));
                                AssertInside(action, view, width, height);
                                Assert.True(action.TransformToAncestor(view).TransformBounds(
                                    new Rect(0, 0, action.ActualWidth, action.ActualHeight)).Right <= width + 1);
                            }
                        }
                    }
                    foreach (var grid in new[]
                    {
                        Assert.IsType<DataGrid>(shift.FindName("ShiftsGrid")),
                        Assert.IsType<DataGrid>(productCatalog.FindName("ProductsGrid")),
                        Assert.IsType<DataGrid>(dailySales.FindName("SalesGrid")),
                        Assert.IsType<DataGrid>(auditLog.FindName("AuditGrid")),
                        Assert.IsType<DataGrid>(users.FindName("UsersGrid"))
                    })
                    {
                        Assert.Same(sharedRowStyle, grid.RowStyle);
                        Assert.Same(sharedCellStyle, grid.CellStyle);
                        Assert.Equal(12, grid.FontSize);
                        grid.ApplyTemplate();
                        grid.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, grid));
                        var viewer = Assert.IsType<ScrollViewer>(grid.Template.FindName("DG_ScrollViewer", grid));
                        viewer.ApplyTemplate();
                        var scrollbar = Assert.IsType<System.Windows.Controls.Primitives.ScrollBar>(
                            viewer.Template.FindName("PART_VerticalScrollBar", viewer));
                        Assert.Equal(0, Grid.GetRow(scrollbar));
                        Assert.Equal(2, Grid.GetRowSpan(scrollbar));
                        Assert.All(grid.Columns.OfType<System.Windows.Controls.DataGridTextColumn>(),
                            column => Assert.NotNull(column.ElementStyle));
                    }
                    var settings = new SettingsView();
                    Assert.Null(settings.FindName("ShortcutsList"));
                    Assert.Null(settings.FindName("ShortcutsSection"));
                    var shortcuts = new ShortcutsView();
                    var shortcutsList = Assert.IsType<ItemsControl>(shortcuts.FindName("ShortcutsList"));
                    Assert.Same(KeyboardShortcutCatalog.Groups, shortcutsList.ItemsSource);
                    Assert.Contains(KeyboardShortcutCatalog.Groups.Single(group => group.Title == "POS / New Sale").Shortcuts,
                        shortcut => shortcut.Action == "Pay" && shortcut.Keys.SequenceEqual(new[] { "F2" }));
                    Assert.Contains(KeyboardShortcutCatalog.Groups.Single(group => group.Title == "Application").Shortcuts,
                        shortcut => shortcut.Action == "Lock session" && shortcut.Keys.SequenceEqual(new[] { "F12" }));
                    foreach (var width in new[] { 1200d, 700d })
                    {
                        shortcuts.Measure(new Size(width, 570));
                        shortcuts.Arrange(new Rect(0, 0, width, 570));
                        shortcuts.UpdateLayout();
                        var shortcutsScroller = Assert.IsType<ScrollViewer>(shortcuts.FindName("ShortcutsScroller"));
                        Assert.Equal(ScrollBarVisibility.Disabled, shortcutsScroller.HorizontalScrollBarVisibility);
                        Assert.True(shortcutsScroller.ScrollableHeight > 0);
                        Assert.True(shortcutsList.ActualWidth <= shortcutsScroller.ViewportWidth);
                        settings.Measure(new Size(width, 570));
                        settings.Arrange(new Rect(0, 0, width, 570));
                        settings.UpdateLayout();
                        var save = Assert.IsType<Button>(settings.FindName("SaveButton"));
                        var saveBounds = save.TransformToAncestor(settings).TransformBounds(
                            new Rect(0, 0, save.ActualWidth, save.ActualHeight));
                        Assert.InRange(saveBounds.Bottom, 520, 570);
                        Assert.True(saveBounds.Right <= width);
                        var scroller = Assert.IsType<ScrollViewer>(settings.FindName("SettingsScroller"));
                        Assert.Equal(ScrollBarVisibility.Disabled, scroller.HorizontalScrollBarVisibility);
                        if (width == 700)
                        {
                            Assert.True(scroller.ScrollableHeight > 0);
                            scroller.ScrollToBottom();
                            settings.UpdateLayout();
                            var restore = Assert.IsType<Button>(settings.FindName("RestoreButton"));
                            var restoreBounds = restore.TransformToAncestor(scroller).TransformBounds(
                                new Rect(0, 0, restore.ActualWidth, restore.ActualHeight));
                            Assert.True(restoreBounds.Top >= 0);
                            Assert.True(restoreBounds.Bottom <= scroller.ViewportHeight);
                        }
                    }
                    var backupFolder = Assert.IsType<TextBox>(settings.FindName("BackupCopyFolderBox"));
                    Assert.Equal("External drive or synced-folder path", HintAssist.GetHint(backupFolder));
                    var catalogSearch = Assert.IsType<TextBox>(productCatalog.FindName("SearchBox"));
                    Assert.Equal(catalogSearch.Tag, HintAssist.GetHint(catalogSearch));
                    Assert.Equal("Search barcode, product, or category", catalogSearch.Tag);
                    Assert.Contains("#Inter", productCatalog.FontFamily.Source);
                    Assert.Equal(28, catalogSearch.Height);
                    Assert.Equal(5, catalogSearch.Margin.Top);
                    Assert.Equal(5, catalogSearch.Margin.Bottom);
                    Assert.Same(Application.Current.FindResource("InputBox"), catalogSearch.Style);
                    var catalogGrid = Assert.IsType<DataGrid>(productCatalog.FindName("ProductsGrid"));
                    Assert.Contains("#Inter", catalogGrid.FontFamily.Source);
                    Assert.Equal(28, catalogGrid.RowHeight);
                    Assert.Equal(26, catalogGrid.ColumnHeaderHeight);
                    Assert.Equal("Product Code", catalogGrid.Columns[0].Header);
                    Assert.Equal("Product Description", catalogGrid.Columns[1].Header);
                    Assert.Equal("ACTIVE", new ProductRowVM(new Product { IsActive = true }).ActiveText);
                    Assert.Equal("DISABLED", new ProductRowVM(new Product { IsActive = false }).ActiveText);
                    var toolbar = Assert.IsType<Grid>(productCatalog.FindName("ToolbarGrid"));
                    var actions = Assert.IsType<WrapPanel>(productCatalog.FindName("ActionsPanel"));
                    Assert.Equal(1, Grid.GetColumn(actions));
                    Assert.Equal(0, Grid.GetRow(actions));
                    Assert.Equal(5, actions.Children.Count);
                    Assert.All(actions.Children.OfType<Button>(), button => Assert.Equal(28, button.Height));
                    Assert.All(actions.Children.OfType<Button>(), button =>
                    {
                        Assert.Equal(5, button.Margin.Top);
                        Assert.Equal(5, button.Margin.Bottom);
                    });
                    toolbar.Measure(new Size(700, 100));
                    toolbar.Arrange(new Rect(0, 0, 700, toolbar.DesiredSize.Height));
                    toolbar.UpdateLayout();
                    Assert.Equal(1, Grid.GetRow(actions));
                    Assert.Equal(0, Grid.GetColumn(actions));
                    foreach (var width in new[] { 1200d, 760d })
                    {
                        productCatalog.Measure(new Size(width, 570));
                        productCatalog.Arrange(new Rect(0, 0, width, 570));
                        productCatalog.UpdateLayout();
                        Assert.True(catalogGrid.ActualHeight >=
                            catalogGrid.ColumnHeaderHeight + catalogGrid.RowHeight);
                        if (width > 800)
                        {
                            var searchTop = catalogSearch.TransformToAncestor(toolbar)
                                .TransformBounds(new Rect(0, 0, catalogSearch.ActualWidth,
                                    catalogSearch.ActualHeight)).Top;
                            var button = Assert.IsType<Button>(actions.Children[0]);
                            var buttonTop = button.TransformToAncestor(toolbar)
                                .TransformBounds(new Rect(0, 0, button.ActualWidth,
                                    button.ActualHeight)).Top;
                            Assert.Equal(buttonTop, searchTop, 1);
                        }
                        Assert.True(actions.TransformToAncestor(productCatalog)
                            .TransformBounds(new Rect(0, 0, actions.ActualWidth, actions.ActualHeight)).Right
                            <= width + 1);
                    }
                    AssertTextInputFits(catalogSearch);
                    AssertTextInputFits(backupFolder);
                    AssertDateInputFits(Assert.IsType<DatePicker>(dailySales.FindName("DatePick")));
                    AssertDateInputFits(Assert.IsType<DatePicker>(auditLog.FindName("FromPick")));

                    var cashMovement = new CashMovementDialog();
                    AssertTextInputFits(Assert.IsType<TextBox>(cashMovement.FindName("AmountBox")));
                    cashMovement.Close();
                    new ShiftOpenDialog().Close();
                    var productEdit = new ProductEditDialog(null);
                    AssertTextInputFits(Assert.IsType<TextBox>(productEdit.FindName("NameBox")));
                    var barcodeField = Assert.IsType<TextBox>(productEdit.FindName("BarcodeBox"));
                    var productFields = Assert.IsType<StackPanel>(barcodeField.Parent);
                    Assert.True(productFields.Children.IndexOf(barcodeField) <
                                productFields.Children.IndexOf(Assert.IsType<TextBox>(productEdit.FindName("NameBox"))));
                    productEdit.Close();
                    new UserEditDialog(null).Close();
                    new PaymentDialog(100m, PaymentMethod.Cash).Close();
                    var quantityEditor = new QuantityEditDialog("TEST PRODUCT", 2m, 3m);
                    Assert.Equal("2", Assert.IsType<TextBox>(quantityEditor.FindName("QuantityBox")).Text);
                    Assert.Contains("3", Assert.IsType<TextBlock>(quantityEditor.FindName("StockText")).Text);
                    quantityEditor.Close();
                    var previewSale = new Sale { SaleNumber = 0, TotalAmount = 112m, ChangeAmount = 8m };
                    var printReportView = new DailySalesView();
                    var printSample = new Sale
                    {
                        SaleNumber = 999, ReceiptNumber = "OR-TEST", CustomerName = "PRINT CUSTOMER",
                        SaleDate = DateTime.Today.AddHours(13), GrossAmount = 112m,
                        TotalAmount = 112m, VatAmount = 12m, NetAmount = 100m
                    };
                    typeof(DailySalesView).GetField("_report", System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)!.SetValue(printReportView,
                            new DailySalesReport { Date = DateOnly.FromDateTime(DateTime.Today), Sales = [printSample] });
                    var printReportDocument = Assert.IsType<System.Windows.Documents.FlowDocument>(
                        typeof(DailySalesView).GetMethod("BuildReportDocument", System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic)!.Invoke(printReportView, null));
                    var printSalesTable = printReportDocument.Blocks.OfType<System.Windows.Documents.Table>().Last();
                    Assert.Equal(new Thickness(0), printReportDocument.Blocks.FirstBlock.Margin);
                    var previousBranchName = AppSettings.Get("BranchName");
                    var previousCompanyAddress = AppSettings.Get("CompanyAddress");
                    try
                    {
                        AppSettings.Set("BranchName", "REPORT TEST BRANCH");
                        AppSettings.Set("CompanyAddress", "REPORT TEST ADDRESS");
                        db.SaveChanges();
                        var branchDocument = Assert.IsType<System.Windows.Documents.FlowDocument>(
                            typeof(DailySalesView).GetMethod("BuildReportDocument", System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic)!.Invoke(printReportView, null));
                        Assert.Equal(new Thickness(0), branchDocument.Blocks.FirstBlock.Margin);
                        Assert.Equal(new Thickness(0), branchDocument.Blocks.FirstBlock.NextBlock.Margin);
                        Assert.Contains("REPORT TEST BRANCH", new System.Windows.Documents.TextRange(
                            branchDocument.Blocks.FirstBlock.NextBlock.ContentStart,
                            branchDocument.Blocks.FirstBlock.NextBlock.ContentEnd).Text);
                        var addressBlock = branchDocument.Blocks.FirstBlock.NextBlock.NextBlock;
                        Assert.Equal(new Thickness(0), addressBlock.Margin);
                        Assert.Contains("REPORT TEST ADDRESS", new System.Windows.Documents.TextRange(
                            addressBlock.ContentStart, addressBlock.ContentEnd).Text);
                        AppSettings.Set("BranchName", "   ");
                        db.SaveChanges();
                        var blankBranchDocument = Assert.IsType<System.Windows.Documents.FlowDocument>(
                            typeof(DailySalesView).GetMethod("BuildReportDocument", System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic)!.Invoke(printReportView, null));
                        Assert.Equal(new Thickness(0), blankBranchDocument.Blocks.FirstBlock.Margin);
                        Assert.Equal(new Thickness(0), blankBranchDocument.Blocks.FirstBlock.NextBlock.Margin);
                        Assert.Contains("REPORT TEST ADDRESS", new System.Windows.Documents.TextRange(
                            blankBranchDocument.Blocks.FirstBlock.NextBlock.ContentStart,
                            blankBranchDocument.Blocks.FirstBlock.NextBlock.ContentEnd).Text);
                        Assert.Contains("DAILY SALES REPORT", new System.Windows.Documents.TextRange(
                            blankBranchDocument.Blocks.FirstBlock.NextBlock.NextBlock.ContentStart,
                            blankBranchDocument.Blocks.FirstBlock.NextBlock.NextBlock.ContentEnd).Text);
                    }
                    finally
                    {
                        AppSettings.Set("BranchName", previousBranchName);
                        AppSettings.Set("CompanyAddress", previousCompanyAddress);
                        db.SaveChanges();
                    }
                    var headingBand = Assert.Single(printReportDocument.Blocks.OfType<System.Windows.Documents.Paragraph>(),
                        paragraph => new System.Windows.Documents.TextRange(paragraph.ContentStart, paragraph.ContentEnd)
                            .Text.Contains("DAILY SALES REPORT"));
                    Assert.Null(headingBand.Background);
                    Assert.Equal(new Thickness(0), headingBand.Padding);
                    Assert.Equal(FontWeights.Bold, Assert.IsType<System.Windows.Documents.Run>(headingBand.Inlines.FirstInline).FontWeight);
                    Assert.DoesNotContain("Generated", new System.Windows.Documents.TextRange(
                        headingBand.ContentStart, headingBand.ContentEnd).Text);
                    var signatureSection = Assert.IsType<System.Windows.Documents.Paragraph>(printReportDocument.Blocks.LastBlock);
                    Assert.True(signatureSection.KeepTogether);
                    var footerText = new System.Windows.Documents.TextRange(signatureSection.ContentStart, signatureSection.ContentEnd).Text;
                    Assert.Contains("Generated:", footerText);
                    Assert.Contains(admin.FullName, footerText);
                    Assert.Contains("Prepared by / Signature", footerText);
                    var signatureLine = Assert.IsType<Border>(signatureSection.Inlines
                        .OfType<System.Windows.Documents.InlineUIContainer>().Single().Child);
                    Assert.Equal(240, signatureLine.Width);
                    Assert.Equal(new Thickness(0, 0, 0, 2), signatureLine.BorderThickness);
                    string CellText(System.Windows.Documents.TableCell cell) =>
                        new System.Windows.Documents.TextRange(cell.ContentStart, cell.ContentEnd).Text.Trim();
                    Assert.Equal(new[] { "INVOICE", "TIME", "CUSTOMER", "PAYMENT", "GROSS", "DISC", "TOTAL", "VAT", "NET", "VOID" },
                        printSalesTable.RowGroups[0].Rows[0].Cells.Select(CellText));
                    Assert.Equal(10, printSalesTable.Columns.Count);
                    var printedCells = printSalesTable.RowGroups[0].Rows[1].Cells;
                    Assert.Equal("OR-TEST", CellText(printedCells[0]));
                    Assert.Equal("PRINT CUSTOMER", CellText(printedCells[2]));
                    Assert.Equal("CASH", CellText(printedCells[3]));
                    Assert.Equal(112m.ToString("N2"), CellText(printedCells[6]));
                    Assert.Equal(12m.ToString("N2"), CellText(printedCells[7]));
                    Assert.Equal(100m.ToString("N2"), CellText(printedCells[8]));
                    printReportDocument.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run("PDF export - second page / PHP 112.00 / VAT 12.00 / NET 100.00"))
                    { BreakPageBefore = true });
                    var pdfPreview = new ReportPrintPreviewWindow(printReportDocument);
                    var pdfPaperBox = Assert.IsType<ComboBox>(pdfPreview.FindName("PaperSizeBox"));
                    foreach (var paper in ReportPaperSize.Options)
                    {
                        pdfPaperBox.SelectedItem = paper;
                        Assert.Equal(10, printSalesTable.FontSize);
                        Assert.Equal(new Thickness(0, 0, 2, 2), printSalesTable.RowGroups[0].Rows[0].Cells[0].BorderThickness);
                        using var pdfStream = new System.IO.MemoryStream();
                        ReportPdfExporter.Export(printReportDocument, pdfStream);
                        pdfStream.Position = 0;
                        using var exported = PdfSharp.Pdf.IO.PdfReader.Open(pdfStream, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
                        Assert.Equal(2, exported.PageCount);
                        Assert.All(exported.Pages.Cast<PdfSharp.Pdf.PdfPage>(), page =>
                        {
                            Assert.InRange(Math.Abs(page.Width.Point - paper.Width * 72 / 96), 0, 0.01);
                            Assert.InRange(Math.Abs(page.Height.Point - paper.Height * 72 / 96), 0, 0.01);
                            Assert.NotNull(page.Elements.GetDictionary("/Resources")?.Elements.GetDictionary("/XObject"));
                        });
                        // Optional local visual QA artifacts; ordinary test runs write no files.
                        if (Environment.GetEnvironmentVariable("MYPOS_PDF_QA_DIR") is { Length: > 0 } qaDirectory)
                        {
                            System.IO.Directory.CreateDirectory(qaDirectory);
                            ReportPdfExporter.Save(printReportDocument,
                                System.IO.Path.Combine(qaDirectory, $"report-{pdfPaperBox.SelectedIndex}.pdf"));
                        }
                    }
                    pdfPreview.Close();
                    var branchId = db.Branches.First().Id;
                    var shiftService = new ShiftService(db);
                    if (!shiftService.HasOpenShift) shiftService.OpenShift(branchId, admin.Id, 100m);
                    var dialogs = new Window[]
                    {
                        new CashMovementDialog(), new ChangeInitialPasswordDialog(admin),
                        new HeldSalesDialog(branchId), new LockWindow(admin, 2),
                        new PaymentDialog(112m, PaymentMethod.Cash), new ProductEditDialog(null),
                        new QuantityEditDialog("TEST PRODUCT", 2m, 3m),
                        new ReceiptPreviewDialog(previewSale),
                        new ReceiveStockDialog(new Product { Name = "TEST PRODUCT", StockQty = 30 }),
                        new ReportPrintPreviewWindow(new System.Windows.Documents.FlowDocument()),
                        new SaleCompleteDialog(previewSale, false, false), new SaleDetailDialog(previewSale),
                        new ShiftCloseDialog(), new ShiftOpenDialog(), new UserEditDialog(null),
                        new VoidSaleDialog(previewSale)
                    };
                    foreach (var dialog in dialogs)
                    {
                        Assert.Equal(ResizeMode.NoResize, dialog.ResizeMode);
                        Assert.Equal(dialog is LockWindow ? WindowStyle.None : WindowStyle.SingleBorderWindow,
                            dialog.WindowStyle);
                        if (dialog is ReportPrintPreviewWindow)
                        {
                            var paperBox = Assert.IsType<ComboBox>(dialog.FindName("PaperSizeBox"));
                            Assert.Null(dialog.FindName("OrientationBox"));
                            var viewer = Assert.IsAssignableFrom<System.Windows.Controls.FlowDocumentPageViewer>(dialog.FindName("DocViewer"));
                            var printDocument = Assert.IsType<System.Windows.Documents.FlowDocument>(viewer.Document);
                            Assert.Equal(3, paperBox.Items.Count);
                            Assert.Equal(12, dialog.FontSize);
                            Assert.Equal(12, paperBox.FontSize);
                            Assert.Equal(new Thickness(1), paperBox.BorderThickness);
                            var table = new System.Windows.Documents.Table();
                            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(100) });
                            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(200) });
                            printDocument.Blocks.Add(table);
                            foreach (var paper in ReportPaperSize.Options)
                            {
                                paperBox.SelectedItem = paper;
                                Assert.Equal(paper.Width, printDocument.PageWidth);
                                Assert.Equal(paper.Height, printDocument.PageHeight);
                                Assert.True(printDocument.PageHeight > printDocument.PageWidth);
                                Assert.Equal(new Thickness(48), printDocument.PagePadding);
                                Assert.InRange(Math.Abs(table.Columns.Sum(column => column.Width.Value) -
                                    (printDocument.PageWidth - 96)), 0, 0.01);
                            }
                            Assert.Equal(816, ReportPaperSize.Options[0].Width);
                            Assert.Equal(1248, ReportPaperSize.Options[0].Height);
                            Assert.Equal(1056, ReportPaperSize.Options[1].Height);
                        }
                        if (dialog is LockWindow) Assert.Null(dialog.FindName("ErrorText"));
                        Assert.Contains("#Inter", dialog.FontFamily.Source);
                        var content = Assert.IsAssignableFrom<FrameworkElement>(dialog.Content);
                        var contentWidth = Math.Min(dialog.Width - 32, 1000);
                        content.Measure(new Size(contentWidth, 550));
                        content.Arrange(new Rect(0, 0, contentWidth, 550));
                        content.UpdateLayout();
                        if (dialog is ReportPrintPreviewWindow)
                        {
                            var viewer = Assert.IsAssignableFrom<FlowDocumentPageViewer>(dialog.FindName("DocViewer"));
                            var zoom = Assert.IsType<Slider>(dialog.FindName("ZoomSlider"));
                            Assert.Equal(100d, viewer.Zoom);
                            Assert.IsType<Button>(dialog.FindName("ZoomInButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Assert.Equal(110d, viewer.Zoom);
                            Assert.IsType<Button>(dialog.FindName("ZoomOutButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Assert.Equal(100d, viewer.Zoom);
                            zoom.Value = 150;
                            content.UpdateLayout();
                            Assert.Equal(150d, viewer.Zoom);
                            Assert.NotEmpty(viewer.PageViews);
                            Assert.All(viewer.PageViews, page =>
                            {
                                Assert.Equal(Stretch.None, page.Stretch);
                                var guide = Assert.IsType<Border>(page.Parent);
                                Assert.Equal("PaperGuideBorder", guide.Name);
                                Assert.Equal(new Thickness(1), guide.BorderThickness);
                                Assert.NotNull(guide.BorderBrush);
                                Assert.InRange(Math.Abs(guide.ActualWidth - page.ActualWidth - 2), 0, 0.1);
                                Assert.InRange(Math.Abs(guide.ActualHeight - page.ActualHeight - 2), 0, 0.1);
                                var document = Assert.IsType<System.Windows.Documents.FlowDocument>(viewer.Document);
                                Assert.InRange(Math.Abs(page.ActualWidth - document.PageWidth * 1.5), 0, 2);
                            });
                            var resetZoom = Assert.IsType<Button>(dialog.FindName("ResetZoomButton"));
                            Assert.Equal("Reset zoom", resetZoom.Content);
                            Assert.Equal(12, resetZoom.FontSize);
                            Assert.Equal(new Thickness(1), resetZoom.BorderThickness);
                            var zoomField = Assert.IsType<TextBox>(dialog.FindName("ZoomPercentBox"));
                            Assert.Equal(Visibility.Collapsed, TextFieldAssist.GetCharacterCounterVisibility(zoomField));
                            Assert.Equal("150%", zoomField.Text);
                            Assert.False(zoomField.IsReadOnly);
                            void CommitZoom(string input)
                            {
                                zoomField.SetCurrentValue(TextBox.TextProperty, input);
                                typeof(ReportPrintPreviewWindow).GetMethod("CommitZoomInput",
                                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                                    .Invoke(dialog, null);
                            }
                            CommitZoom("125");
                            Assert.Equal(125d, viewer.Zoom);
                            Assert.Equal("125%", zoomField.Text);
                            Assert.Equal(125d, zoom.Value);
                            CommitZoom("80%");
                            Assert.Equal(80d, viewer.Zoom);
                            CommitZoom("999");
                            Assert.Equal(300d, viewer.Zoom);
                            CommitZoom("1");
                            Assert.Equal(25d, viewer.Zoom);
                            CommitZoom("abc");
                            Assert.Equal("25%", zoomField.Text);
                            CommitZoom("");
                            Assert.Equal(25d, viewer.Zoom);
                            CommitZoom("100%");
                            Assert.Equal(100d, viewer.Zoom);
                            zoom.Value = 175;
                            resetZoom.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Assert.Equal(100d, viewer.Zoom);
                            Assert.Equal(100d, zoom.Value);
                            Assert.Equal("100%", zoomField.Text);
                        }
                        Assert.True(content.ActualWidth <= contentWidth + 1);
                        if (dialog is ProductEditDialog)
                        {
                            foreach (var name in new[] { "NameBox", "BarcodeBox" })
                                Assert.Equal(CharacterCasing.Upper, Assert.IsType<TextBox>(dialog.FindName(name)).CharacterCasing);
                            foreach (var name in new[] { "CategoryBox", "UnitBox" })
                            {
                                var box = Assert.IsType<ComboBox>(dialog.FindName(name));
                                box.ApplyTemplate();
                                var input = Assert.IsType<TextBox>(box.Template.FindName("PART_EditableTextBox", box));
                                Assert.Equal(CharacterCasing.Upper, input.CharacterCasing);
                            }
                        }
                        if (dialog is SaleDetailDialog or HeldSalesDialog)
                        {
                            var tableName = dialog is SaleDetailDialog ? "ItemsGrid" : "HeldGrid";
                            Assert.Equal(12, Assert.IsType<DataGrid>(dialog.FindName(tableName)).FontSize);
                        }
                        if (dialog is not LockWindow) dialog.Close();
                    }
                    if (Environment.GetEnvironmentVariable("MYPOS_REPORT_UI_QA_DIR") is { Length: > 0 } uiQaDirectory)
                    {
                        System.IO.Directory.CreateDirectory(uiQaDirectory);
                        void CaptureReport(FrameworkElement element, double width, double height, string name)
                        {
                            element.Measure(new Size(width, height));
                            element.Arrange(new Rect(0, 0, width, height));
                            element.UpdateLayout();
                            var image = new System.Windows.Media.Imaging.RenderTargetBitmap((int)width, (int)height,
                                96, 96, PixelFormats.Pbgra32);
                            image.Render(element);
                            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                            using var output = System.IO.File.Create(System.IO.Path.Combine(uiQaDirectory, name + ".png"));
                            encoder.Save(output);
                        }
                        foreach (var width in new[] { 1300d, 760d })
                        {
                            CaptureReport(dailySales, width, 570, $"daily-{width}");
                            CaptureReport(auditLog, width, 570, $"audit-{width}");
                        }
                        var qaPreview = new ReportPrintPreviewWindow(printReportDocument);
                        CaptureReport((FrameworkElement)qaPreview.Content, 918, 600, "preview");
                        qaPreview.Close();
                    }
                }
                finally
                {
                    App.CurrentUser = null;
                    dbProperty.SetValue(null, null);
                }

                app.Shutdown();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
            Assert.Fail(error.ToString());
    }

    private static void AssertTextInputFits(TextBox box)
    {
        var width = double.IsNaN(box.Width) ? 300 : box.Width;
        box.Measure(new Size(width, box.Height));
        box.Arrange(new Rect(0, 0, width, box.Height));
        box.ApplyTemplate();
        var content = Assert.IsAssignableFrom<FrameworkElement>(box.Template.FindName("PART_ContentHost", box));
        Assert.True(content.ActualHeight >= 12,
            $"{box.Name} has only {content.ActualHeight:0.#}px of editable height at a {box.Height:0.#}px field height.");
    }

    private static void AssertPasswordInputFits(PasswordBox box)
    {
        box.Measure(new Size(box.Width, box.Height));
        box.Arrange(new Rect(0, 0, box.Width, box.Height));
        box.ApplyTemplate();
        var content = Assert.IsAssignableFrom<FrameworkElement>(box.Template.FindName("PART_ContentHost", box));
        Assert.True(content.ActualHeight >= 12,
            $"{box.Name} has only {content.ActualHeight:0.#}px of editable height at a {box.Height:0.#}px field height.");
    }

    private static void AssertDateInputFits(DatePicker picker)
    {
        var width = double.IsNaN(picker.Width)
            ? Math.Max(picker.MinWidth, picker.ActualWidth)
            : picker.Width;
        picker.Measure(new Size(width, picker.Height));
        picker.Arrange(new Rect(0, 0, width, picker.Height));
        picker.ApplyTemplate();
        var content = Assert.IsAssignableFrom<FrameworkElement>(picker.Template.FindName("PART_TextBox", picker));
        Assert.True(content.ActualHeight >= 16,
            $"{picker.Name} has only {content.ActualHeight:0.#}px of date-entry height at a {picker.Height:0.#}px field height.");
    }

    private static void AssertPosLayoutFits(PosView pos, double width, double height)
    {
        pos.Measure(new Size(width, height));
        pos.Arrange(new Rect(0, 0, width, height));
        pos.UpdateLayout();
        Assert.Null(pos.FindName("PosScroller"));
        foreach (var gridName in new[] { "ProductsGrid", "CartGrid" })
        {
            var grid = Assert.IsType<DataGrid>(pos.FindName(gridName));
            Assert.Equal(ScrollBarVisibility.Auto, ScrollViewer.GetVerticalScrollBarVisibility(grid));
            Assert.Equal(ScrollBarVisibility.Disabled, ScrollViewer.GetHorizontalScrollBarVisibility(grid));
            Assert.True(grid.ActualHeight >= grid.ColumnHeaderHeight + grid.RowHeight,
                $"{gridName} cannot show a header and one item at {width:0}x{height:0}: " +
                $"{grid.ActualHeight:0.#}px available.");
        }
        var cart = Assert.IsType<Border>(pos.FindName("CartArea"));
        var productsPanel = Assert.IsType<Border>(pos.FindName("ProductsPanel"));
        Assert.Equal(0, Grid.GetRow(cart));
        Assert.Equal(1, Grid.GetColumn(cart));
        var actions = Assert.IsType<Grid>(pos.FindName("SaleActions"));
        var recall = Assert.IsType<Button>(pos.FindName("RecallButton"));
        var recallLabel = Assert.IsType<TextBlock>(pos.FindName("RecallLabel"));
        Assert.Contains("(F6)", recallLabel.Text);
        Assert.Null(recall.ToolTip);
        Assert.True(recallLabel.ActualWidth <= recall.ActualWidth + 1);
        Assert.True(recallLabel.ActualHeight <= recall.ActualHeight);
        AssertInside(actions, cart, width, height);
        AssertInside(Assert.IsType<Button>(pos.FindName("PayButton")), cart, width, height);
        AssertInside(Assert.IsType<DataGrid>(pos.FindName("ProductsGrid")), productsPanel, width, height);
        Assert.Equal(7, actions.ColumnDefinitions.Count);
        var smallWidth = actions.ColumnDefinitions[0].ActualWidth;
        Assert.True(smallWidth > 40, $"Sale actions are too narrow at {width:0}px.");
        Assert.Equal(smallWidth, actions.ColumnDefinitions[2].ActualWidth, 1);
        Assert.Equal(smallWidth, actions.ColumnDefinitions[4].ActualWidth, 1);
        Assert.Equal(smallWidth * 2, actions.ColumnDefinitions[6].ActualWidth, 1);
        Assert.Equal(actions.ActualWidth,
            actions.ColumnDefinitions.Sum(column => column.ActualWidth), 1);
        var customerFields = Assert.IsType<Grid>(pos.FindName("CustomerFields"));
        Assert.Equal(customerFields.ColumnDefinitions[0].ActualWidth,
            customerFields.ColumnDefinitions[1].ActualWidth, 1);
        var nameField = Assert.IsType<TextBox>(pos.FindName("CustomerNameBox"));
        var address = Assert.IsType<TextBox>(pos.FindName("AddressBox"));
        Assert.Equal(Grid.GetRow(nameField), Grid.GetRow(address));
        Assert.Equal(nameField.ActualWidth, address.ActualWidth, 1);
        Assert.Null(pos.FindName("DateTimeText"));
        var status = Assert.IsType<TextBlock>(pos.FindName("StatusText"));
        var statusBounds = status.TransformToAncestor(pos)
            .TransformBounds(new Rect(0, 0, status.ActualWidth, status.ActualHeight));
        Assert.True(statusBounds.Bottom <= height + 1 && statusBounds.Bottom >= height - 50,
            $"The scan status is not pinned to the bottom of the {width:0}x{height:0} POS viewport.");
        foreach (var name in new[] { "HeaderFields", "TotalCard", "OpenShiftButton", "PayButton" })
        {
            var element = Assert.IsAssignableFrom<FrameworkElement>(pos.FindName(name));
            var bounds = element.TransformToAncestor(pos)
                .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            Assert.True(bounds.Left >= -1 && bounds.Right <= width + 1 &&
                        bounds.Top >= -1 && bounds.Bottom <= height + 1,
                $"{name} falls outside the {width:0}x{height:0} POS viewport: {bounds}.");
        }
    }

    private static void AssertInside(FrameworkElement child, FrameworkElement parent,
        double width, double height)
    {
        var bounds = child.TransformToAncestor(parent)
            .TransformBounds(new Rect(0, 0, child.ActualWidth, child.ActualHeight));
        Assert.True(bounds.Top >= -1 && bounds.Bottom <= parent.ActualHeight + 1,
            $"{child.Name} is clipped by {parent.Name} at {width:0}x{height:0}: " +
            $"{bounds}, parent height {parent.ActualHeight:0.#}.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hwnd, out WindowRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hwnd, ref WindowPoint point);
}
