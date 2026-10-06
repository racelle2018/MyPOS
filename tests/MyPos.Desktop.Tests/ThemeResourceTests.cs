using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Shell;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MaterialDesignThemes.Wpf;
using MyPos.Core.Data;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using MyPos.Desktop;
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
                Assert.NotNull(app.TryFindResource("PageTitle"));
                Assert.NotNull(app.TryFindResource("FieldHint"));
                Assert.NotNull(app.TryFindResource(typeof(TextBox)));
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
                    Assert.Equal(WindowState.Maximized, mainWindow.WindowState);
                    Assert.Equal(480, mainWindow.MinHeight);
                    Assert.Equal(WindowStyle.None, mainWindow.WindowStyle);
                    Assert.Equal(ResizeMode.CanResize, mainWindow.ResizeMode);
                    Assert.NotNull(WindowChrome.GetWindowChrome(mainWindow));
                    Assert.NotNull(mainWindow.FindName("MinimizeWindowButton"));
                    Assert.NotNull(mainWindow.FindName("CloseWindowButton"));
                    var maximizeButton = Assert.IsType<Button>(mainWindow.FindName("MaximizeWindowButton"));
                    maximizeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(WindowState.Normal, mainWindow.WindowState);
                    maximizeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(WindowState.Maximized, mainWindow.WindowState);
                    mainWindow.Opacity = 0;
                    mainWindow.Show();
                    mainWindow.UpdateLayout();
                    var hwnd = new WindowInteropHelper(mainWindow).Handle;
                    Assert.True(GetWindowRect(hwnd, out var windowRect),
                        $"GetWindowRect failed for hwnd={hwnd}; last error={Marshal.GetLastWin32Error()}.");
                    var dpi = VisualTreeHelper.GetDpi(mainWindow).DpiScaleY;
                    Assert.True(windowRect.Bottom <= SystemParameters.WorkArea.Bottom * dpi + 1,
                        $"The maximized window ends at {windowRect.Bottom}px, " +
                        $"below the taskbar-safe work area ({SystemParameters.WorkArea.Bottom * dpi:0.#}px).");
                    maximizeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(WindowState.Normal, mainWindow.WindowState);
                    maximizeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(WindowState.Maximized, mainWindow.WindowState);
                    Assert.True(GetWindowRect(hwnd, out windowRect));
                    Assert.True(windowRect.Bottom <= SystemParameters.WorkArea.Bottom * dpi + 1,
                        "Restoring and maximizing again must not cover the taskbar.");
                    mainWindow.Close();
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
                        ("CustomerNameBox", "Customer Name"),
                        ("AddressBox", "Customer Address")
                    })
                    {
                        var field = Assert.IsType<TextBox>(pos.FindName(fieldName));
                        Assert.Equal(42, field.Height);
                        Assert.True(field.Margin.Top >= 0);
                        Assert.Equal(expectedHint, HintAssist.GetHint(field));
                        Assert.True(HintAssist.GetIsFloating(field));
                        Assert.Equal(CharacterCasing.Upper, field.CharacterCasing);
                        AssertTextInputFits(field);
                        var fieldHint = Assert.IsType<SmartHint>(field.Template.FindName("Hint", field));
                        fieldHint.ApplyTemplate();
                        Assert.True(fieldHint.UseFloating);
                        Assert.Equal(Colors.White,
                            Assert.IsType<SolidColorBrush>(HintAssist.GetBackground(field)).Color);
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
                    Assert.Equal("PN/ SKU", Assert.IsType<DataGrid>(pos.FindName("ProductsGrid")).Columns[0].Header);
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
                    discountDetail.Text = "10";
                    discountKind.SelectedIndex = 2;
                    Assert.True(discountDetail.IsEnabled);
                    Assert.Equal("", discountDetail.Text);
                    Assert.Equal("SC/PWD ID *", discountLabel.Text);
                    discountKind.SelectedIndex = 0;
                    Assert.False(discountDetail.IsEnabled);

                    var productCatalog = new ProductCatalogView();
                    Assert.NotNull(productCatalog.FindName("ProductsGrid"));
                    var dailySales = new DailySalesView();
                    Assert.NotNull(dailySales.FindName("SalesGrid"));
                    var auditLog = new AuditLogView();
                    Assert.NotNull(auditLog.FindName("AuditGrid"));
                    var users = new UserManagementView();
                    Assert.NotNull(users.FindName("UsersGrid"));
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
                        Assert.All(grid.Columns.OfType<System.Windows.Controls.DataGridTextColumn>(),
                            column => Assert.NotNull(column.ElementStyle));
                    }
                    var settings = new SettingsView();
                    var backupFolder = Assert.IsType<TextBox>(settings.FindName("BackupCopyFolderBox"));
                    Assert.Equal("External drive or synced-folder path", HintAssist.GetHint(backupFolder));
                    var catalogSearch = Assert.IsType<TextBox>(productCatalog.FindName("SearchBox"));
                    Assert.Equal(catalogSearch.Tag, HintAssist.GetHint(catalogSearch));
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
                    productEdit.Close();
                    new UserEditDialog(null).Close();
                    new PaymentDialog(100m, PaymentMethod.Cash).Close();
                    var quantityEditor = new QuantityEditDialog("TEST PRODUCT", 2m, 3m);
                    Assert.Equal("2", Assert.IsType<TextBox>(quantityEditor.FindName("QuantityBox")).Text);
                    Assert.Contains("3", Assert.IsType<TextBlock>(quantityEditor.FindName("StockText")).Text);
                    quantityEditor.Close();
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
        picker.Measure(new Size(picker.Width, picker.Height));
        picker.Arrange(new Rect(0, 0, picker.Width, picker.Height));
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
        var status = Assert.IsType<TextBlock>(pos.FindName("DateTimeText"));
        var statusBounds = status.TransformToAncestor(pos)
            .TransformBounds(new Rect(0, 0, status.ActualWidth, status.ActualHeight));
        Assert.True(statusBounds.Bottom <= height + 1 && statusBounds.Bottom >= height - 50,
            $"The status clock is not pinned to the bottom of the {width:0}x{height:0} POS viewport.");
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out WindowRect rect);
}
