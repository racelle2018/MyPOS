using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Media;

namespace RetailForma.Desktop.Dialogs;

public enum NotificationKind { Success, Warning, Error, Information, Confirmation }
public sealed record NotificationRequest(string Message, string Title, NotificationKind Kind,
    MessageBoxButton Buttons = MessageBoxButton.OK, MessageBoxResult DefaultResult = MessageBoxResult.None,
    Window? Owner = null);

/// <summary>One presentation path for user-triggered action results and confirmations.</summary>
public static class ActionDialog
{
    internal static Func<NotificationRequest, MessageBoxResult>? Presenter { get; set; }

    public static MessageBoxResult Success(string message, string title = "Success", Window? owner = null) =>
        Present(new(message, title, NotificationKind.Success, Owner: owner));
    public static MessageBoxResult Warning(string message, string title = "Warning", Window? owner = null) =>
        Present(new(message, title, NotificationKind.Warning, Owner: owner));
    public static MessageBoxResult Error(string message, string title = "Error", Window? owner = null) =>
        Present(new(message, title, NotificationKind.Error, Owner: owner));
    public static MessageBoxResult Information(string message, string title = "Information", Window? owner = null) =>
        Present(new(message, title, NotificationKind.Information, Owner: owner));
    public static MessageBoxResult Show(string message, string title = RetailForma.Core.ProductBrand.Name,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information,
        MessageBoxResult defaultResult = MessageBoxResult.None) =>
        Show(null, message, title, buttons, image, defaultResult);
    public static MessageBoxResult Show(Window? owner, string message, string title,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information,
        MessageBoxResult defaultResult = MessageBoxResult.None) =>
        Present(new(message, title, image switch
        {
            MessageBoxImage.Warning => NotificationKind.Warning,
            MessageBoxImage.Error => NotificationKind.Error,
            MessageBoxImage.Question => NotificationKind.Confirmation,
            _ => NotificationKind.Information
        }, buttons, defaultResult, owner));

    private static MessageBoxResult Present(NotificationRequest request)
    {
        if (Presenter != null) return Presenter(request);
        var app = Application.Current;
        // Startup/fatal errors must remain reportable even if WPF theme loading failed.
        if (app?.TryFindResource("UiFontFamily") == null)
            return NativeFallback(request);
        var owner = request.Owner != null && IsAvailableOwner(request.Owner) ? request.Owner :
            app.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive && IsAvailableOwner(w))
            ?? app.Windows.Cast<Window>().LastOrDefault(IsAvailableOwner);
        try
        {
            var dialog = new NotificationDialog(request);
            if (owner != null) dialog.Owner = owner;
            var soundPlayed = false;
            dialog.Loaded += (_, _) =>
            {
                if (soundPlayed) return;
                soundPlayed = true;
                try { SoundFor(request.Kind).Play(); }
                catch (Exception ex) { Serilog.Log.Debug(ex, "Notification sound could not be played"); }
            };
            dialog.ShowDialog();
            return dialog.Result;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Notification dialog could not be displayed");
            return NativeFallback(request);
        }
    }

    // Windows supplies the sound scheme and volume; do not force a WAV file or override mute.
    internal static SystemSound SoundFor(NotificationKind kind) => kind switch
    {
        NotificationKind.Warning => SystemSounds.Exclamation,
        NotificationKind.Error => SystemSounds.Hand,
        NotificationKind.Confirmation => SystemSounds.Question,
        _ => SystemSounds.Asterisk
    };

    // Session lock hides native HWNDs without ending their WPF modal loops.
    // IsVisible alone still reports true for those hidden windows.
    private static bool IsAvailableOwner(Window window) => window.IsVisible &&
        new WindowInteropHelper(window).Handle is var handle && handle != IntPtr.Zero && IsWindowVisible(handle);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    private static MessageBoxResult NativeFallback(NotificationRequest request) => System.Windows.MessageBox.Show(
        request.Message, request.Title, request.Buttons, request.Kind switch
        {
            NotificationKind.Warning => MessageBoxImage.Warning,
            NotificationKind.Error => MessageBoxImage.Error,
            NotificationKind.Confirmation => MessageBoxImage.Question,
            _ => MessageBoxImage.Information
        }, request.DefaultResult != MessageBoxResult.None ? request.DefaultResult : request.Buttons switch
        {
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel or MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            _ => MessageBoxResult.OK
        });
}
