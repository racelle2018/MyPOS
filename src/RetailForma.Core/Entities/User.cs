namespace RetailForma.Core.Entities;

public class User : EntityBase
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}

// Preserve persisted role numbers. Admin is displayed as Owner, not migrated.
public enum UserRole { Admin = 1, Cashier = 2, SystemAdministrator = 3 }

public static class UserAccess
{
    public static readonly UserRole[] AdministratorRoles = [UserRole.Admin, UserRole.SystemAdministrator];
    public static bool IsAdministrator(User? user) => user is { IsActive: true } && AdministratorRoles.Contains(user.Role);
    public static bool CanManageUser(User? actor, User? target) => IsAdministrator(actor) &&
        (target?.Role != UserRole.SystemAdministrator || actor!.Role == UserRole.SystemAdministrator);
    public static string RoleName(UserRole role) => role switch
    {
        UserRole.Admin => "Owner",
        UserRole.Cashier => "Cashier",
        UserRole.SystemAdministrator => "System Administrator",
        _ => "Unknown"
    };
}
