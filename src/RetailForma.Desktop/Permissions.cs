using System.Windows;
using RetailForma.Core.Entities;

namespace RetailForma.Desktop;

/// <summary>
/// Central role checks. Rule for the whole app: hiding buttons is cosmetic —
/// every privileged action must call one of these.
/// </summary>
public static class Permissions
{
    public static bool IsAdmin => UserAccess.IsAdministrator(App.CurrentUser);
    public static bool IsSystemAdministrator => App.CurrentUser is { IsActive: true, Role: UserRole.SystemAdministrator };

    /// <summary>
    /// Returns true for admins. Otherwise logs the denial to the audit trail,
    /// warns the user, and returns false.
    /// </summary>
    public static bool RequireAdmin(string action)
    {
        if (IsAdmin) return true;

        // The owner will want to know who tried what — monitoring feature
        App.Service.ClientEvent("PermissionDenied", $"Attempted: {action}");

        RetailForma.Desktop.Dialogs.ActionDialog.Show($"Only administrators can {action}.", "RetailForma",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }
}
