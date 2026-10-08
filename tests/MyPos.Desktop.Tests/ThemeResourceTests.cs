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

                    var firstProduct = new Product { Barcode = "TEST-FIRST", Name = "FIRST TEST PRODUCT", Price = 10m };
                    db.Products.Add(firstProduct);
                    db.SaveChanges();
                    var productCatalog = new ProductCatalogView();
                    var selectedProductGrid = Assert.IsType<DataGrid>(productCatalog.FindName("ProductsGrid"));
                    Assert.Equal(firstProduct.Id, Assert.IsType<ProductRowVM>(selectedProductGrid.SelectedItem).Product.Id);
                    Assert.True(Assert.IsType<Button>(productCatalog.FindName("ReceiveStockButton")).IsEnabled);
                    Assert.True(Assert.IsType<Button>(productCatalog.FindName("EditButton")).IsEnabled);
                    Assert.True(Assert.IsType<Button>(productCatalog.FindName("DeactivateButton")).IsEnabled);
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
                        Assert.All(grid.Columns.OfType<System.Windows.Controls.DataGridTextColumn>(),
                            column => Assert.NotNull(column.ElementStyle));
                    }
                    var settings = new SettingsView();
                    foreach (var width in new[] { 1200d, 700d })
                    {
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
                    Assert.Same(Application.Current.FindResource("UpperBox"), catalogSearch.Style);
                    var catalogGrid = Assert.IsType<DataGrid>(productCatalog.FindName("ProductsGrid"));
                    Assert.Contains("#Inter", catalogGrid.FontFamily.Source);
                    Assert.Equal(28, catalogGrid.RowHeight);
                    Assert.Equal(26, catalogGrid.ColumnHeaderHeight);
                    Assert.Equal("Barcode", catalogGrid.Columns[0].Header);
                    Assert.Equal("Product Description", catalogGrid.Columns[1].Header);
                    Assert.Equal("ACTIVE", new ProductRowVM(new Product { IsActive = true }).ActiveText);
                    Assert.Equal("DISABLED", new ProductRowVM(new Product { IsActive = false }).ActiveText);
                    var toolbar = Assert.IsType<Grid>(productCatalog.FindName("ToolbarGrid"));
                    var actions = Assert.IsType<WrapPanel>(productCatalog.FindName("ActionsPanel"));
                    Assert.Equal(1, Grid.GetColumn(actions));
                    Assert.Equal(0, Grid.GetRow(actions));
                    Assert.Equal(4, actions.Children.Count);
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
