using System.Media;
using System.Reflection;
using MyPos.Desktop.Dialogs;
using Xunit;

namespace MyPos.Desktop.Tests;

public class NotificationSoundTests
{
    [Theory]
    [InlineData(NotificationKind.Success, "Asterisk")]
    [InlineData(NotificationKind.Information, "Asterisk")]
    [InlineData(NotificationKind.Warning, "Exclamation")]
    [InlineData(NotificationKind.Error, "Hand")]
    [InlineData(NotificationKind.Confirmation, "Question")]
    public void Notification_kind_uses_matching_windows_sound(NotificationKind kind, string expected)
    {
        var systemSound = typeof(SystemSounds).GetProperty(expected)!.GetValue(null);
        var selected = typeof(ActionDialog).GetMethod("SoundFor", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { kind });
        Assert.Same(systemSound, selected);
        // No Play call: automated tests must not produce audio or show a modal dialog.
    }
}
