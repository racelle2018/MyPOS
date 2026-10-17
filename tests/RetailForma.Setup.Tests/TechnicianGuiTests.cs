using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetailForma.Setup;
using RetailForma.Desktop.Dialogs;
using Xunit;

namespace RetailForma.Setup.Tests;

public class TechnicianGuiTests
{
    [Fact]
    public void Standalone_form_has_branding_masked_inputs_fixed_actions_and_one_dip_chrome()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            App? app = null;
            try
            {
                app = new App { IsolatedTestHost = true }; app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Assert.Equal(new Thickness(1), app.FindResource("UiBorderThickness"));
                var font = Assert.IsType<FontFamily>(app.FindResource("UiFontFamily"));
                Assert.True(new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var glyph));
                Assert.Contains("Inter", glyph.FamilyNames.Values);
                var window = new MainWindow(); var body = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                Assert.Equal("RetailForma Technician Setup", window.Title); Assert.NotNull(window.Icon);
                Assert.NotNull(Assert.IsType<Image>(window.FindName("RetailFormaLogo")).Source);
                Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
                Assert.Equal("1.2.3", Assert.IsType<TextBlock>(window.FindName("VersionText")).Text);
                var password = Assert.IsType<PasswordBox>(window.FindName("PasswordBox"));
                Assert.Equal(new Thickness(1), password.BorderThickness);
                NotificationRequest? shown = null;
                ActionDialog.Presenter = message => { shown = message; return MessageBoxResult.OK; };
                Assert.IsType<Button>(window.FindName("CreateButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(NotificationKind.Warning, shown!.Kind);
                var tabs = Assert.IsType<TabControl>(window.FindName("ModeTabs"));
                Assert.Equal(3, tabs.Items.Count);
                for (var tab = 0; tab < tabs.Items.Count; tab++)
                foreach (var size in new[] { new Size(520, 520), new Size(480, 330) })
                {
                    tabs.SelectedIndex = tab;
                    body.Measure(size); body.Arrange(new Rect(size)); body.UpdateLayout();
                    var action = Assert.IsType<Button>(window.FindName(tab == 0 ? "CreateButton" : tab == 1 ? "ResetButton" : "ImportButton"));
                    Assert.Equal(Visibility.Visible, action.Visibility);
                    var location = action.TranslatePoint(new Point(), body);
                    Assert.True(location.Y + action.ActualHeight <= size.Height);
                    var directory = Environment.GetEnvironmentVariable("RETAILFORMA_TECHNICIAN_QA_DIR");
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                        var image = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); image.Render(body);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                        using var file = File.Create(Path.Combine(directory, $"technician-tab{tab}-{size.Width}-{size.Height}.png")); encoder.Save(file);
                    }
                }
                var recoveryPassword = Assert.IsType<PasswordBox>(window.FindName("RecoveryPasswordBox"));
                recoveryPassword.Password = "temporary recovery test-only password";
                password.Password = "temporary test-only password"; window.Close(); Assert.Equal("", password.Password); Assert.Equal("", recoveryPassword.Password);
            }
            catch (Exception ex) { failure = ex; }
            finally { ActionDialog.Presenter = null; app?.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
    }
}
