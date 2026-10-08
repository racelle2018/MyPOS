using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace MyPos.Desktop.Controls;

public static class ReportDatePickerBehavior
{
    private const string DisplayFormat = "MM/dd/yyyy";
    private static readonly string[] AcceptedFormats =
    {
        "MMddyyyy", "M/d/yyyy", "M/dd/yyyy", "MM/d/yyyy", DisplayFormat
    };
    private static readonly DependencyProperty IsFormattingProperty =
        DependencyProperty.RegisterAttached(
            "IsFormatting", typeof(bool), typeof(ReportDatePickerBehavior));

    public static readonly DependencyProperty ClearSelectionOnCalendarCloseProperty =
        DependencyProperty.RegisterAttached(
            "ClearSelectionOnCalendarClose",
            typeof(bool),
            typeof(ReportDatePickerBehavior),
            new PropertyMetadata(false, OnClearSelectionOnCalendarCloseChanged));

    public static bool GetClearSelectionOnCalendarClose(DatePicker picker) =>
        (bool)picker.GetValue(ClearSelectionOnCalendarCloseProperty);

    public static void SetClearSelectionOnCalendarClose(DatePicker picker, bool value) =>
        picker.SetValue(ClearSelectionOnCalendarCloseProperty, value);

    public static readonly DependencyProperty NormalizeDateInputProperty =
        DependencyProperty.RegisterAttached(
            "NormalizeDateInput",
            typeof(bool),
            typeof(ReportDatePickerBehavior),
            new PropertyMetadata(false, OnNormalizeDateInputChanged));

    public static bool GetNormalizeDateInput(DatePicker picker) =>
        (bool)picker.GetValue(NormalizeDateInputProperty);

    public static void SetNormalizeDateInput(DatePicker picker, bool value) =>
        picker.SetValue(NormalizeDateInputProperty, value);

    public static readonly DependencyProperty HasInputErrorProperty =
        DependencyProperty.RegisterAttached(
            "HasInputError",
            typeof(bool),
            typeof(ReportDatePickerBehavior),
            new PropertyMetadata(false));

    public static bool GetHasInputError(DatePicker picker) =>
        (bool)picker.GetValue(HasInputErrorProperty);

    public static readonly DependencyProperty InputErrorMessageProperty =
        DependencyProperty.RegisterAttached(
            "InputErrorMessage",
            typeof(string),
            typeof(ReportDatePickerBehavior),
            new PropertyMetadata(null));

    public static string? GetInputErrorMessage(DatePicker picker) =>
        (string?)picker.GetValue(InputErrorMessageProperty);

    public static readonly DependencyProperty IsRequiredDateProperty =
        DependencyProperty.RegisterAttached(
            "IsRequiredDate",
            typeof(bool),
            typeof(ReportDatePickerBehavior),
            new PropertyMetadata(false));

    public static bool GetIsRequiredDate(DatePicker picker) =>
        (bool)picker.GetValue(IsRequiredDateProperty);

    public static void SetIsRequiredDate(DatePicker picker, bool value) =>
        picker.SetValue(IsRequiredDateProperty, value);

    private static readonly DependencyProperty LastValidDateProperty =
        DependencyProperty.RegisterAttached(
            "LastValidDate", typeof(DateTime?), typeof(ReportDatePickerBehavior));
    private static readonly DependencyProperty LastWithinLimitTextProperty =
        DependencyProperty.RegisterAttached(
            "LastWithinLimitText", typeof(string), typeof(ReportDatePickerBehavior),
            new PropertyMetadata(string.Empty));

    private static void OnNormalizeDateInputChanged(
        DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not DatePicker picker) return;

        picker.RemoveHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextChanged));
        picker.RemoveHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
        picker.RemoveHandler(UIElement.PreviewLostKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnPreviewLostKeyboardFocus));
        picker.RemoveHandler(UIElement.LostFocusEvent, new RoutedEventHandler(OnLostFocus));
        picker.SelectedDateChanged -= OnSelectedDateChanged;
        picker.Loaded -= OnLoaded;
        if (!(bool)args.NewValue) return;

        picker.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextChanged));
        picker.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
        picker.AddHandler(UIElement.PreviewLostKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnPreviewLostKeyboardFocus));
        picker.AddHandler(UIElement.LostFocusEvent, new RoutedEventHandler(OnLostFocus));
        picker.SelectedDateChanged += OnSelectedDateChanged;
        picker.Loaded += OnLoaded;
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        var picker = (DatePicker)sender;
        if (picker.SelectedDate is DateTime date)
            picker.SetValue(LastValidDateProperty, date);
        FormatSelectedDate(picker);
    }

    private static void OnSelectedDateChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (sender is not DatePicker picker) return;
        if (picker.SelectedDate is DateTime date)
            picker.SetValue(LastValidDateProperty, date);
        FormatSelectedDate(picker);
    }

    private static void OnLostFocus(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is DatePickerTextBox)
            FormatSelectedDate((DatePicker)sender);
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (sender is DatePicker picker && args.OriginalSource is DatePickerTextBox)
        {
            if (args.Key == Key.Enter)
            {
                CommitDateInput(picker);
                args.Handled = true;
                return;
            }
            if (args.Key == Key.Escape)
            {
                RestoreSelectedDate(picker);
                args.Handled = true;
                return;
            }
        }

        if (args.Key != Key.Back || sender is not DatePicker ||
            args.OriginalSource is not DatePickerTextBox textBox ||
            textBox.SelectionLength != 0 || textBox.CaretIndex < 2 ||
            textBox.Text[textBox.CaretIndex - 1] != '/')
            return;

        // Remove the digit before an auto-inserted slash as well as the slash.
        var start = textBox.CaretIndex - 2;
        textBox.Select(start, 2);
        textBox.SelectedText = string.Empty;
        textBox.CaretIndex = Math.Min(start, textBox.Text.Length);
        args.Handled = true;
    }

    private static void OnPreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
    {
        if (sender is not DatePicker picker || args.OriginalSource is not DatePickerTextBox textBox ||
            picker.IsDropDownOpen)
            return;

        var input = textBox.Text;
        var selectedDate = picker.SelectedDate;
        if (CommitDateInput(picker)) return;
        var message = GetInputErrorMessage(picker);
        var restoreRequiredDate = input.Length == 0 && GetIsRequiredDate(picker);

        // WPF parses the text on LostFocus and may replace an invalid value.
        // Restore the draft after its handler so it stays visible for correction.
        picker.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (picker.IsDropDownOpen) return;
            if (restoreRequiredDate)
            {
                picker.SelectedDate = (DateTime?)picker.GetValue(LastValidDateProperty) ?? DateTime.Today;
                FormatSelectedDate(picker);
                SetInputError(picker, textBox, message);
                return;
            }
            if (picker.SelectedDate != selectedDate)
                picker.SelectedDate = selectedDate;
            picker.SetValue(IsFormattingProperty, true);
            try
            {
                textBox.Text = input;
            }
            finally
            {
                picker.SetValue(IsFormattingProperty, false);
            }
            SetInputError(picker, textBox, message);
        }));
    }

    public static bool CommitDateInput(DatePicker picker)
    {
        if (picker.Template.FindName("PART_TextBox", picker) is not DatePickerTextBox textBox)
            return false;

        var input = textBox.Text.Trim();
        var error = ValidateInput(picker, input, true, out var date);
        if (error != null)
        {
            if (input.Length == 0 && GetIsRequiredDate(picker))
            {
                picker.SelectedDate = (DateTime?)picker.GetValue(LastValidDateProperty) ?? DateTime.Today;
                FormatSelectedDate(picker);
            }
            SetInputError(picker, textBox, error);
            return false;
        }

        if (date is DateTime validDate)
        {
            if (picker.SelectedDate?.Date != validDate.Date)
                picker.SelectedDate = validDate;
            FormatSelectedDate(picker);
        }
        else if (picker.SelectedDate != null)
        {
            picker.SelectedDate = null;
        }
        SetInputError(picker, textBox, null);
        return true;
    }

    private static void RestoreSelectedDate(DatePicker picker)
    {
        if (picker.Template.FindName("PART_TextBox", picker) is not DatePickerTextBox textBox)
            return;
        if (picker.SelectedDate is null && GetIsRequiredDate(picker))
            picker.SelectedDate = (DateTime?)picker.GetValue(LastValidDateProperty) ?? DateTime.Today;
        if (picker.SelectedDate is DateTime)
            FormatSelectedDate(picker);
        else
            textBox.Text = string.Empty;
        SetInputError(picker, textBox, null);
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs args)
    {
        if (sender is not DatePicker picker || args.OriginalSource is not DatePickerTextBox textBox)
            return;
        if ((bool)picker.GetValue(IsFormattingProperty)) return;

        var input = textBox.Text.Trim();
        if (input.Length == 0)
        {
            picker.SetValue(LastWithinLimitTextProperty, string.Empty);
            SetInputError(picker, textBox, null);
            return;
        }

        var digits = new string(input.Where(c => c is >= '0' and <= '9').ToArray());
        if (digits.Length > 8)
        {
            var previous = (string)picker.GetValue(LastWithinLimitTextProperty);
            picker.SetValue(IsFormattingProperty, true);
            try
            {
                textBox.Text = previous;
                textBox.CaretIndex = textBox.Text.Length;
            }
            finally
            {
                picker.SetValue(IsFormattingProperty, false);
            }
            return;
        }
        if (input.Any(c => c is not (>= '0' and <= '9') and not '/'))
        {
            picker.SetValue(LastWithinLimitTextProperty, textBox.Text);
            SetInputError(picker, textBox, "Use digits in MM/DD/YYYY format.");
            return;
        }

        var parts = input.Split('/');
        var abbreviatedDate = parts.Length == 3 && parts[2].Length == 4 &&
                              (parts[0].Length == 1 || parts[1].Length == 1);
        if (!abbreviatedDate)
        {
            digits = ClampEnteredDigits(digits);
            var masked = FormatPartialDigits(digits);
            if (textBox.Text != masked)
            {
                var digitsBeforeCaret = textBox.Text[..Math.Min(textBox.CaretIndex, textBox.Text.Length)]
                    .Count(c => c is >= '0' and <= '9');
                picker.SetValue(IsFormattingProperty, true);
                try
                {
                    textBox.Text = masked;
                    textBox.CaretIndex = CaretAfterDigits(masked, digitsBeforeCaret);
                }
                finally
                {
                    picker.SetValue(IsFormattingProperty, false);
                }
            }
            input = masked;
        }
        picker.SetValue(LastWithinLimitTextProperty, textBox.Text);

        var error = ValidateInput(picker, input, false, out var date);
        SetInputError(picker, textBox, error);
        if (error != null || date is not DateTime validDate || abbreviatedDate) return;

        if (picker.SelectedDate?.Date != validDate.Date)
            picker.SelectedDate = validDate;
        FormatSelectedDate(picker);
        textBox.Select(textBox.Text.Length, 0);
    }

    private static string? ValidateInput(DatePicker picker, string input, bool requireComplete,
        out DateTime? date)
    {
        date = null;
        if (input.Length == 0)
            return requireComplete && GetIsRequiredDate(picker) ? "A sales date is required." : null;
        if (input.Any(c => c is not (>= '0' and <= '9') and not '/'))
            return "Use digits in MM/DD/YYYY format.";

        var digits = new string(input.Where(c => c is >= '0' and <= '9').ToArray());
        if (digits.Length > 8) return "A date needs eight digits: MMDDYYYY.";

        var parts = input.Split('/');
        var abbreviatedDate = parts.Length == 3 && parts[2].Length == 4 &&
                              (parts[0].Length == 1 || parts[1].Length == 1);
        if (!abbreviatedDate)
        {
            var prefixError = ValidatePrefix(digits);
            if (prefixError != null) return prefixError;
            if (digits.Length < 8)
                return requireComplete ? "Complete the date as MM/DD/YYYY." : null;
        }

        if (!DateTime.TryParseExact(input, AcceptedFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            return "Enter a valid date in MM/DD/YYYY format.";
        if ((picker.DisplayDateStart is DateTime start && parsed.Date < start.Date) ||
            (picker.DisplayDateEnd is DateTime end && parsed.Date > end.Date) ||
            picker.BlackoutDates.Contains(parsed))
            return "That date is outside the allowed range.";

        date = parsed;
        return null;
    }

    private static string? ValidatePrefix(string digits)
    {
        if (digits.Length < 2) return null;

        var month = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        if (month is < 1 or > 12) return "Month must be 01–12.";
        if (digits.Length < 4) return null;

        var day = int.Parse(digits.Substring(2, 2), CultureInfo.InvariantCulture);
        if (day is < 1 or > 31) return "Day must be 01–31.";
        if (month is 4 or 6 or 9 or 11 && day == 31)
            return "This month has only 30 days.";
        if (month == 2 && day > 29)
            return "February has at most 29 days.";
        if (digits.Length < 8) return null;

        var year = int.Parse(digits[4..], CultureInfo.InvariantCulture);
        if (year == 0) return "Year must be between 0001 and 9999.";
        if (month == 2 && day == 29 && !DateTime.IsLeapYear(year))
            return $"February 29 is not valid in {year}.";
        return null;
    }

    private static string FormatPartialDigits(string digits)
    {
        if (digits.Length < 2) return digits;
        if (digits.Length < 4) return digits[..2] + "/" + digits[2..];
        return digits[..2] + "/" + digits.Substring(2, 2) + "/" + digits[4..];
    }

    private static string ClampEnteredDigits(string digits)
    {
        var characters = digits.ToCharArray();
        if (characters.Length >= 2 &&
            int.Parse(digits[..2], CultureInfo.InvariantCulture) > 12)
        {
            characters[0] = '1';
            characters[1] = '2';
        }
        if (characters.Length >= 4 &&
            int.Parse(digits.Substring(2, 2), CultureInfo.InvariantCulture) > 31)
        {
            characters[2] = '3';
            characters[3] = '1';
        }
        return new string(characters);
    }

    private static int CaretAfterDigits(string text, int digitCount)
    {
        if (digitCount == 0) return 0;

        var seen = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '/') continue;
            if (++seen != digitCount) continue;

            var caret = index + 1;
            while (caret < text.Length && text[caret] == '/') caret++;
            return caret;
        }
        return text.Length;
    }

    private static void SetInputError(DatePicker picker, DatePickerTextBox textBox, string? message)
    {
        picker.SetValue(InputErrorMessageProperty, message);
        picker.SetValue(HasInputErrorProperty, message != null);
        textBox.ToolTip = message;
    }

    private static void FormatSelectedDate(DatePicker picker)
    {
        if (picker.SelectedDate is not DateTime date ||
            picker.Template.FindName("PART_TextBox", picker) is not DatePickerTextBox textBox)
            return;
        if ((bool)picker.GetValue(IsFormattingProperty)) return;

        var formatted = date.ToString(DisplayFormat, CultureInfo.InvariantCulture);
        picker.SetValue(IsFormattingProperty, true);
        try
        {
            if (textBox.Text != formatted)
                textBox.Text = formatted;
            picker.SetValue(LastWithinLimitTextProperty, formatted);
            SetInputError(picker, textBox, null);
        }
        finally
        {
            picker.SetValue(IsFormattingProperty, false);
        }
    }

    private static void OnClearSelectionOnCalendarCloseChanged(
        DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not DatePicker picker) return;

        picker.CalendarClosed -= OnCalendarClosed;
        if ((bool)args.NewValue)
            picker.CalendarClosed += OnCalendarClosed;
    }

    private static void OnCalendarClosed(object? sender, RoutedEventArgs args)
    {
        if (sender is not DatePicker picker) return;
        if (GetNormalizeDateInput(picker)) FormatSelectedDate(picker);
        if (picker.Template.FindName("PART_TextBox", picker) is DatePickerTextBox textBox)
            textBox.Select(textBox.Text.Length, 0);
    }
}
