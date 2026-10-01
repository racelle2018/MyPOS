using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MyPos.Desktop.Controls;

/// <summary>
/// Turns an editable ComboBox into a search box: typing filters the dropdown
/// list and opens it (auto-suggest). Attach again to refresh the choice list
/// without duplicating handlers. onTextChanged fires on every keystroke —
/// hook your live filtering there.
/// </summary>
public static class ComboBoxSearch
{
    public static void Attach(ComboBox box, List<string> choices, Action<string>? onTextChanged = null)
    {
        // Already attached? Swap in fresh choices and refilter — no new handlers.
        if (box.Tag is State state)
        {
            state.Choices = choices;
            state.OnTextChanged ??= onTextChanged;
            ApplyFilter(box, state);
            return;
        }

        state = new State { Choices = choices, OnTextChanged = onTextChanged };
        box.Tag = state;
        box.ItemsSource = choices;

        box.ApplyTemplate();
        if (box.Template.FindName("PART_EditableTextBox", box) is not TextBox tb) return;

        // No blue selection block inside the box
        tb.SelectionBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
        tb.GotKeyboardFocus += (_, _) => tb.Dispatcher.BeginInvoke(() =>
        {
            if (tb.Text.Length > 0 && tb.SelectionLength == tb.Text.Length)
                tb.Select(tb.Text.Length, 0);
        });
        box.DropDownClosed += (_, _) => tb.Dispatcher.BeginInvoke(() =>
        {
            if (tb.Text.Length > 0 && tb.SelectionLength == tb.Text.Length)
                tb.Select(tb.Text.Length, 0);
        });

        tb.PreviewTextInput += (_, _) => box.IsDropDownOpen = true;
        tb.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Back or Key.Delete) box.IsDropDownOpen = true;
        };

        tb.TextChanged += (_, _) =>
        {
            if (state.Busy) return;
            state.Busy = true;
            ApplyFilter(box, state);
            state.Busy = false;
            state.OnTextChanged?.Invoke(tb.Text);
        };
    }

    private static void ApplyFilter(ComboBox box, State state)
    {
        box.ApplyTemplate();
        var tb = box.Template.FindName("PART_EditableTextBox", box) as TextBox;
        var text = tb?.Text ?? box.Text;
        var caret = tb?.CaretIndex ?? 0;

        box.ItemsSource = string.IsNullOrWhiteSpace(text)
            ? state.Choices
            : state.Choices.Where(c => c.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

        if (tb != null)
        {
            if (tb.Text != text) tb.Text = text;
            tb.CaretIndex = caret;
        }
    }

    private sealed class State
    {
        public List<string> Choices = null!;
        public Action<string>? OnTextChanged;
        public bool Busy;
    }
}