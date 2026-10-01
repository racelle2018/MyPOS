using System.Windows;
using MyPos.Core.Entities;

namespace MyPos.Desktop;

/// <summary>
/// Central role checks. Rule for the whole app: hiding buttons is cosmetic —
/// every privileged action must call one of these.
/// </summary>
public static class Permissions
{
    public static bool IsAdmin => App.CurrentUser?.Role == UserRole.Admin;

    /// <summary>
    /// Returns true for admins. Otherwise logs the denial to the audit trail,
    /// warns the user, and returns false.
    /// </summary>
    public static bool RequireAdmin(string action)
    {
        if (IsAdmin) return true;

        // The owner will want to know who tried what — monitoring feature
        App.Db.AuditLogs.Add(new AuditLog
        {
            Date = DateTime.Now,
            UserId = App.CurrentUser?.Id,
            Action = "PermissionDenied",
            EntityName = "Permission",
            Details = $"Attempted: {action}"
        });
        App.Db.SaveChanges();

        MessageBox.Show($"Only administrators can {action}.", "MyPos",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }
}