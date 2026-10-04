using System.Threading;
using System.Windows;
using System.Windows.Controls;
using MyPos.Desktop;
using Xunit;

namespace MyPos.Desktop.Tests;

public class ThemeResourceTests
{
    [Fact]
    public void App_theme_loads_mahapps_resources_and_mypos_overrides()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();

                Assert.NotNull(app.TryFindResource("MahApps.Styles.Button"));
                Assert.NotNull(app.TryFindResource("MyPosButton"));
                Assert.NotNull(app.TryFindResource("PrimaryButton"));
                Assert.NotNull(app.TryFindResource(typeof(TextBox)));

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
            throw error;
    }
}
