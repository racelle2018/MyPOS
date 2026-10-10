using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using MyPos.Desktop.Controls;
using Xunit;

namespace MyPos.Desktop.Tests;

public class DataGridBehaviorsTests
{
    [Fact]
    public void Clicking_inline_text_clears_outside_selection_but_preserves_button_selection()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var grid = new DataGrid();
                grid.Items.Add("Selected sale");
                var outsideRun = new Run("No sales recorded for this date");
                var outsideText = new TextBlock(new Span(outsideRun));
                var buttonRun = new Run("Reprint receipt");
                var button = new Button { Content = new TextBlock(new Span(buttonRun)) };
                var panel = new StackPanel();
                panel.Children.Add(grid);
                panel.Children.Add(outsideText);
                panel.Children.Add(button);
                window = new Window { Content = panel };
                DataGridBehaviors.SetDeselectOnOutsideClick(grid, true);

                // Attach the real window mouse handler without showing a UI.
                typeof(DataGridBehaviors).GetMethod("Grid_Loaded",
                    BindingFlags.NonPublic | BindingFlags.Static)!
                    .Invoke(null, new object[] { grid, new RoutedEventArgs() });

                void Click(DependencyObject source)
                {
                    window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = Mouse.PreviewMouseDownEvent,
                        Source = source
                    });
                }

                grid.SelectedIndex = 0;
                Click(buttonRun);
                Assert.Equal(0, grid.SelectedIndex);

                Click(outsideRun);
                Assert.Equal(-1, grid.SelectedIndex);

                grid.SelectedIndex = 0;
                Click(grid);
                Assert.Equal(0, grid.SelectedIndex);
            }
            catch (Exception ex) { error = ex; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
