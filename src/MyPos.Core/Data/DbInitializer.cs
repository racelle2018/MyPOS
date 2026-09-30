using Microsoft.EntityFrameworkCore;
using MyPos.Core.Entities;

namespace MyPos.Core.Data;

public static class DbInitializer
{
    public static void Initialize(MyPosDbContext db)
    {
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;"); // safer crash recovery

        if (db.Accounts.Any()) return;   // already seeded — idempotent

        db.Branches.Add(new Branch { Name = "Main Branch" });

        db.Users.Add(new User
        {
            Username = "admin",
            FullName = "Administrator",
            Role = UserRole.Admin,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123") // change on first login (later)
        });

        db.Accounts.AddRange(new[]
        {
            new Account { Code = "1000", Name = "Cash on Hand",       Type = AccountType.Asset,     IsSystem = true },
            new Account { Code = "1010", Name = "Cash in Bank",       Type = AccountType.Asset,     IsSystem = true },
            new Account { Code = "1200", Name = "Inventory",          Type = AccountType.Asset,     IsSystem = true },
            new Account { Code = "2000", Name = "VAT Payable",        Type = AccountType.Liability, IsSystem = true },
            new Account { Code = "3000", Name = "Owner's Equity",     Type = AccountType.Equity,    IsSystem = true },
            new Account { Code = "4000", Name = "Sales Revenue",      Type = AccountType.Revenue,   IsSystem = true },
            new Account { Code = "4100", Name = "Sales Discounts",    Type = AccountType.Revenue,   IsSystem = true },
            new Account { Code = "5000", Name = "Cost of Goods Sold", Type = AccountType.Expense,   IsSystem = true },
            new Account { Code = "6000", Name = "Operating Expenses", Type = AccountType.Expense,   IsSystem = true },
        });

        db.Settings.AddRange(new[]
        {
            new Setting { Key = "CompanyName",           Value = "My Store" },
            new Setting { Key = "ReceiptIssuanceEnabled", Value = "false" },   // Mode A default
            new Setting { Key = "VatRate",               Value = "0.12" },
        });

        db.SaveChanges();
    }
}