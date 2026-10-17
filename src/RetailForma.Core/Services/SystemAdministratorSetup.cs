using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;

namespace RetailForma.Core.Services;

/// <summary>Initial provisioning only. The elevated local setup tool is the caller, not the Owner UI.</summary>
public static class SystemAdministratorSetup
{
    public static User CreateFirst(RetailFormaDbContext db, string username, string fullName, string password, string installationOperator)
    {
        username = username.Trim(); fullName = fullName.Trim();
        if (username.Length < 3 || !username.All(char.IsLetterOrDigit))
            throw new ArgumentException("Username must contain at least three letters/numbers only.");
        if (fullName.Length == 0 || string.IsNullOrWhiteSpace(installationOperator))
            throw new ArgumentException("Individual name and installation operator are required.");
        if (password.Length is < 16 or > 256 || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Use an individual password/passphrase of 16–256 characters.");
        using var transaction = db.Database.BeginTransaction();
        if (db.Users.Any(u => u.Role == UserRole.SystemAdministrator))
            throw new InvalidOperationException("A System Administrator already exists. This tool cannot reset, replace or promote accounts.");
        if (db.Users.AsNoTracking().AsEnumerable().Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Username already exists. Existing accounts are never promoted.");
        var user = new User { Username = username, FullName = fullName, Role = UserRole.SystemAdministrator,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12), IsActive = true };
        db.Users.Add(user);
        db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, UserId = user.Id, Action = "SystemAdministratorProvisioned",
            EntityName = "User", EntityId = user.Id,
            Details = $"Initial individual account created by local installation operator {installationOperator}; no existing account promoted." });
        db.SaveChanges();
        transaction.Commit();
        return user;
    }
}
