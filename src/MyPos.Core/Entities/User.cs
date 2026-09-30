namespace MyPos.Core.Entities;

public class User : EntityBase
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum UserRole { Admin = 1, Cashier = 2 }