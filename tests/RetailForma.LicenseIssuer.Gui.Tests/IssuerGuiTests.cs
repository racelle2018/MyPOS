using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetailForma.LicenseIssuer.Gui;
using RetailForma.Desktop.Dialogs;
using Xunit;

namespace RetailForma.LicenseIssuer.Gui.Tests;

public class IssuerGuiTests
{
    [Fact]
    public void Standalone_theme_masked_dialog_and_all_pages_fit_with_fixed_actions()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            App? app = null;
            try
            {
                app = new App();
                typeof(App).GetProperty("IsolatedTestHost", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, true);
                app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Assert.Equal(new Thickness(1), app.FindResource("UiBorderThickness"));
                var font = Assert.IsType<FontFamily>(app.FindResource("UiFontFamily")); Assert.Contains("#Inter", font.Source);
                Assert.True(new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var glyph));
                Assert.Contains("Inter", glyph.FamilyNames.Values);
                var window = new MainWindow(); var root = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                Assert.Equal("RetailForma License Generator", window.Title);
                Assert.NotNull(window.Icon);
                Assert.NotNull(Assert.IsType<Image>(window.FindName("RetailFormaLogo")).Source);
                Assert.Equal(FontWeights.Normal, Assert.IsType<TextBox>(window.FindName("EntitlementBox")).FontWeight);
                NotificationRequest? shown = null;
                var presenter = typeof(ActionDialog).GetProperty("Presenter", BindingFlags.NonPublic | BindingFlags.Static)!;
                presenter.SetValue(null, (Func<NotificationRequest, MessageBoxResult>)(message => { shown = message; return MessageBoxResult.OK; }));
                try
                {
                    Assert.IsType<Button>(window.FindName("GenerateButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.NotNull(shown); Assert.Equal(NotificationKind.Warning, shown.Kind);
                }
                finally { presenter.SetValue(null, null); }
                var pages = Assert.IsType<TabControl>(window.FindName("Pages"));
                var history = Assert.IsType<DataGrid>(window.FindName("HistoryGrid"));
                Assert.True(RetailForma.Desktop.Controls.DataGridBehaviors.GetScrollBarSpansHeader(history));
                history.ItemsSource = Enumerable.Range(1, 60).Select(index => new { Issued = "2026-10-13 4:00 PM", Company = "TEST COMPANY " + index, Action = "Issued", EntitlementId = Guid.NewGuid() }).ToList();
                foreach (var size in new[] { new Size(740, 600), new Size(580, 370), new Size(1346, 700) })
                    for (var tab = 0; tab < 4; tab++)
                    {
                        pages.SelectedIndex = tab;
                        root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
                        if (tab == 2)
                        {
                            history.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                            history.UpdateLayout();
                            var viewer = Assert.IsType<ScrollViewer>(history.Template.FindName("DG_ScrollViewer", history));
                            viewer.ApplyTemplate();
                            var scrollbar = Assert.IsType<System.Windows.Controls.Primitives.ScrollBar>(viewer.Template.FindName("PART_VerticalScrollBar", viewer));
                            Assert.Equal(0, Grid.GetRow(scrollbar)); Assert.Equal(2, Grid.GetRowSpan(scrollbar));
                        }
                        if (tab < 2)
                        {
                            var button = Assert.IsType<Button>(window.FindName(tab == 0 ? "GenerateButton" : "CreateKeysButton"));
                            var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
                            Assert.InRange(bounds.Right, 0, size.Width); Assert.InRange(bounds.Bottom, 0, size.Height);
                            var scroller = Assert.IsType<ScrollViewer>(window.FindName(tab == 0 ? "GenerateScroller" : "KeysScroller"));
                            Assert.Equal(ScrollBarVisibility.Disabled, scroller.HorizontalScrollBarVisibility);
                            if (tab == 0 && size.Height == 370) Assert.True(scroller.ScrollableHeight > 0);
                        }
                        if (tab == 3)
                        {
                            foreach (var name in new[] { "AuthorizeRecoveryButton", "RecoverBackupButton" })
                            {
                                var action = Assert.IsType<Button>(window.FindName(name));
                                var bounds = action.TransformToAncestor(root).TransformBounds(new Rect(action.RenderSize));
                                Assert.InRange(bounds.Right, 0, size.Width); Assert.InRange(bounds.Bottom, 0, size.Height);
                            }
                            Assert.Equal(ScrollBarVisibility.Disabled, Assert.IsType<ScrollViewer>(window.FindName("RecoveryScroller")).HorizontalScrollBarVisibility);
                        }
                        Capture(root, $"issuer-tab{tab}-{size.Width}x{size.Height}", size);
                    }
                var replace = Assert.IsType<CheckBox>(window.FindName("ReplaceBox")); replace.IsChecked = true;
                Assert.False(Assert.IsType<Button>(window.FindName("NewIdButton")).IsEnabled);
                Assert.Equal("", Assert.IsType<TextBox>(window.FindName("EntitlementBox")).Text);
                window.Close();
                foreach (var creating in new[] { true, false })
                {
                    var dialog = new PassphraseDialog(creating);
                    Assert.Equal(ResizeMode.NoResize, dialog.ResizeMode);
                    Assert.IsType<PasswordBox>(dialog.FindName("PasswordInput"));
                    Assert.Equal(creating ? Visibility.Visible : Visibility.Collapsed, Assert.IsType<StackPanel>(dialog.FindName("ConfirmationPanel")).Visibility);
                    var body = Assert.IsAssignableFrom<FrameworkElement>(dialog.Content); var size = new Size(348, creating ? 240 : 200);
                    body.Measure(size); body.Arrange(new Rect(size)); body.UpdateLayout(); Capture(body, "issuer-passphrase-" + creating, size);
                    dialog.Close();
                }
                var notification = new NotificationDialog(new NotificationRequest("Confirm fixture issuance?", "License Generator", NotificationKind.Confirmation,
                    MessageBoxButton.YesNo, MessageBoxResult.No));
                Assert.Equal(ResizeMode.NoResize, notification.ResizeMode); Assert.Equal(MessageBoxResult.No, notification.Result);
                var choices = Assert.IsType<StackPanel>(notification.FindName("ButtonsPanel"));
                Assert.True(choices.Children.OfType<Button>().Single(button => Equals(button.Content, "No")).IsDefault);
                var notificationBody = Assert.IsAssignableFrom<FrameworkElement>(notification.Content); var notificationSize = new Size(388, 200);
                notificationBody.Measure(notificationSize); notificationBody.Arrange(new Rect(notificationSize)); notificationBody.UpdateLayout();
                Capture(notificationBody, "issuer-confirmation", notificationSize); notification.Close();
            }
            catch (Exception ex) { error = ex; }
            finally { app?.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "GUI fixture did not finish.");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    private static void Capture(FrameworkElement element, string name, Size size)
    {
        if (Environment.GetEnvironmentVariable("MYPOS_ISSUER_QA_DIR") is not { Length: > 0 } folder) return;
        System.IO.Directory.CreateDirectory(folder);
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(System.IO.Path.Combine(folder, name + ".png")); encoder.Save(stream);
    }
}
