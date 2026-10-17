using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace RetailForma.Desktop.Dialogs;

public partial class NotificationDialog : Window
{
    public MessageBoxResult Result { get; private set; }
    public NotificationDialog(NotificationRequest request)
    {
        InitializeComponent();
        Title = request.Title;
        MaxWidth = Math.Max(220, SystemParameters.WorkArea.Width - 32);
        Width = Math.Min(Width, MaxWidth);
        MaxHeight = Math.Max(160, SystemParameters.WorkArea.Height - 32);
        MessageScroller.MaxHeight = Math.Max(60, MaxHeight - 150);
        MessageText.Text = request.Message;
        KindText.Text = request.Kind.ToString();
        StatusIcon.Kind = request.Kind switch
        {
            NotificationKind.Success => PackIconKind.CheckCircleOutline,
            NotificationKind.Warning => PackIconKind.AlertOutline,
            NotificationKind.Error => PackIconKind.CloseCircleOutline,
            NotificationKind.Confirmation => PackIconKind.HelpCircleOutline,
            _ => PackIconKind.InformationOutline
        };
        StatusIcon.Foreground = request.Kind switch
        {
            NotificationKind.Success => Brushes.ForestGreen,
            NotificationKind.Warning => (Brush)FindResource("UiWarningBrush"),
            NotificationKind.Error => (Brush)FindResource("UiDangerBrush"),
            _ => (Brush)FindResource("UiAccentBrush")
        };
        Result = request.Buttons switch
        {
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel or MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            _ => MessageBoxResult.OK
        };
        var choices = request.Buttons switch
        {
            MessageBoxButton.YesNo => new[] { MessageBoxResult.Yes, MessageBoxResult.No },
            MessageBoxButton.YesNoCancel => new[] { MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel },
            MessageBoxButton.OKCancel => new[] { MessageBoxResult.OK, MessageBoxResult.Cancel },
            _ => new[] { MessageBoxResult.OK }
        };
        // Confirmations default to the non-destructive answer unless explicitly supplied.
        var preferred = request.DefaultResult == MessageBoxResult.None ? Result : request.DefaultResult;
        foreach (var choice in choices)
        {
            var button = new Button
            {
                Content = choice.ToString(), MinWidth = 80, Margin = new Thickness(5, 5, 0, 5),
                IsDefault = choice == preferred, IsCancel = choice == Result
            };
            button.Click += (_, _) => { Result = choice; Close(); };
            if (choice == preferred) Loaded += (_, _) => button.Focus();
            ButtonsPanel.Children.Add(button);
        }
    }
}
