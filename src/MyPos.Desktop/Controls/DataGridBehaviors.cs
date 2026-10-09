using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MyPos.Desktop.Controls;

/// <summary>
/// Attach to a DataGrid to clear its selection when the user clicks or tabs
/// anywhere outside the grid.
///
/// Exceptions (selection is kept):
///  - clicks on buttons (Edit / Receive Stock act on the selection)
///  - focus moving into a dialog (muted blue shows which row is being edited)
///  - clicks inside the grid itself (rows, headers, scrollbar)
///
/// Usage:  <DataGrid ctrl:DataGridBehaviors.DeselectOnOutsideClick="True" ... />
/// </summary>
public static class DataGridBehaviors
{
    public static readonly DependencyProperty ScrollBarSpansHeaderProperty =
        DependencyProperty.RegisterAttached("ScrollBarSpansHeader", typeof(bool),
            typeof(DataGridBehaviors), new PropertyMetadata(false, OnScrollBarSpansHeaderChanged));

    public static bool GetScrollBarSpansHeader(DataGrid grid)
        => (bool)grid.GetValue(ScrollBarSpansHeaderProperty);

    public static void SetScrollBarSpansHeader(DataGrid grid, bool value)
        => grid.SetValue(ScrollBarSpansHeaderProperty, value);

    private static void OnScrollBarSpansHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        grid.Loaded -= AlignHeaderScrollBar;
        if ((bool)e.NewValue) grid.Loaded += AlignHeaderScrollBar;
        if (grid.IsLoaded) AlignHeaderScrollBar(grid, new RoutedEventArgs());
    }

    private static void AlignHeaderScrollBar(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        grid.ApplyTemplate();
        if (grid.Template?.FindName("DG_ScrollViewer", grid) is not ScrollViewer viewer) return;
        viewer.ApplyTemplate();
        if (viewer.Template?.FindName("PART_VerticalScrollBar", viewer) is not ScrollBar scrollbar) return;

        // Material's header corner is unused. Span it without touching the
        // content viewport, native scroll bindings, or row virtualization.
        var spansHeader = GetScrollBarSpansHeader(grid);
        Grid.SetRow(scrollbar, spansHeader ? 0 : 1);
        Grid.SetRowSpan(scrollbar, spansHeader ? 2 : 1);
    }

    public static readonly DependencyProperty DeselectOnOutsideClickProperty =
        DependencyProperty.RegisterAttached(
            "DeselectOnOutsideClick",
            typeof(bool),
            typeof(DataGridBehaviors),
            new PropertyMetadata(false, OnChanged));

    public static bool GetDeselectOnOutsideClick(DataGrid grid)
        => (bool)grid.GetValue(DeselectOnOutsideClickProperty);

    public static void SetDeselectOnOutsideClick(DataGrid grid, bool value)
        => grid.SetValue(DeselectOnOutsideClickProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;

        if ((bool)e.NewValue)
        {
            grid.LostKeyboardFocus += Grid_LostKeyboardFocus;
            grid.Loaded += Grid_Loaded;
            grid.Unloaded += Grid_Unloaded;
        }
        else
        {
            grid.LostKeyboardFocus -= Grid_LostKeyboardFocus;
            grid.Loaded -= Grid_Loaded;
            grid.Unloaded -= Grid_Unloaded;
            Unhook(grid);
        }
    }

    // ---------- Keyboard: focus moved to another control (click or Tab) ----------

    private static void Grid_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var grid = (DataGrid)sender;
        if (GetKeepSelection(grid)) return;

        // Focus moved within the grid itself (cell to cell) — keep selection
        if (grid.IsKeyboardFocusWithin) return;

        // No new focus target, or focus jumped to another window (a dialog) — keep selection
        if (e.NewFocus is not DependencyObject target) return;
        if (!ReferenceEquals(Window.GetWindow(target), Window.GetWindow(grid))) return;

        // Buttons may act on the selection — keep it
        if (IsOnButton(target)) return;

        grid.UnselectAll();
    }

    // ---------- Mouse: clicks on non-focusable areas (sidebar, window background) ----------

    // The grid itself can't hear clicks outside it, so once loaded we listen on
    // its window. PreviewMouseDown tunnels through everything before any control
    // reacts, so we see every click first.

    private static void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        var grid = (DataGrid)sender;
        Unhook(grid);   // safety: Loaded can fire again if the grid is re-hosted

        if (Window.GetWindow(grid) is not { } window) return;

        void OnWindowPreviewMouseDown(object s, MouseButtonEventArgs me)
        {
            if (me.OriginalSource is not DependencyObject src) return;
            if (IsInside(src, grid)) return;   // clicked this grid (row/header/scrollbar)
            if (IsOnButton(src)) return;       // clicked a button — it may act on the selection
            grid.UnselectAll();                // clicked anywhere else — clear
        }

        window.PreviewMouseDown += OnWindowPreviewMouseDown;
        grid.SetValue(HookProperty, new Hook { Window = window, Handler = OnWindowPreviewMouseDown });
    }

    private static void Grid_Unloaded(object sender, RoutedEventArgs e) => Unhook((DataGrid)sender);

    private static void Unhook(DataGrid grid)
    {
        if (grid.GetValue(HookProperty) is Hook hook)
        {
            if (hook.Window != null && hook.Handler != null)
                hook.Window.PreviewMouseDown -= hook.Handler;
            grid.SetValue(HookProperty, null);
        }
    }

    // ---------- Tree helpers ----------

    private static bool IsInside(DependencyObject node, DataGrid grid)
    {
        for (var current = node; current != null; current = Parent(current))
            if (ReferenceEquals(current, grid)) return true;
        return false;
    }

    private static bool IsOnButton(DependencyObject node)
    {
        for (var current = node; current != null; current = Parent(current))
            if (current is ButtonBase) return true;
        return false;
    }

    private static DependencyObject? Parent(DependencyObject node)
        => VisualTreeHelper.GetParent(node) ?? (node as FrameworkElement)?.Parent;

    public static readonly DependencyProperty KeepSelectionProperty =
        DependencyProperty.RegisterAttached("KeepSelection", typeof(bool),
            typeof(DataGridBehaviors), new PropertyMetadata(false));

    public static bool GetKeepSelection(DataGrid grid)
        => (bool)grid.GetValue(KeepSelectionProperty);

    public static void SetKeepSelection(DataGrid grid, bool value)
        => grid.SetValue(KeepSelectionProperty, value);

    private sealed class Hook
    {
        public Window? Window;
        public MouseButtonEventHandler? Handler;
    }

    private static readonly DependencyProperty HookProperty =
        DependencyProperty.RegisterAttached("Hook", typeof(Hook), typeof(DataGridBehaviors),
            new PropertyMetadata(null));
}
