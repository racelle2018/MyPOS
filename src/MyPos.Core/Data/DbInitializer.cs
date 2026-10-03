using Microsoft.EntityFrameworkCore;
using MyPos.Core.Entities;
using System.Data;

namespace MyPos.Core.Data;

public static class DbInitializer
{
    public static void Initialize(MyPosDbContext db)
    {
        BaselineLegacyEnsureCreatedDatabase(db);
        db.Database.Migrate();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;"); // safer crash recovery

        if (!db.Branches.Any())
            db.Branches.Add(new Branch { Name = "Main Branch" });

        if (!db.Users.Any(user => user.Username == "admin"))
        {
            db.Users.Add(new User
            {
                Username = "admin",
                FullName = "Administrator",
                Role = UserRole.Admin,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123")
            });
        }

        if (!db.Accounts.Any()) db.Accounts.AddRange(new[]
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

        AddSettingIfMissing(db, "CompanyName", "My Store");
        AddSettingIfMissing(db, "ReceiptIssuanceEnabled", "false");
        AddSettingIfMissing(db, "VatRate", "0.12");

        db.SaveChanges();
    }

    private static void AddSettingIfMissing(MyPosDbContext db, string key, string value)
    {
        if (!db.Settings.Any(setting => setting.Key == key)
            && !db.ChangeTracker.Entries<Setting>().Any(entry => entry.Entity.Key == key))
            db.Settings.Add(new Setting { Key = key, Value = value });
    }

    private static void BaselineLegacyEnsureCreatedDatabase(MyPosDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        connection.Open();
        try
        {
            var hasAccounts = Exists(connection, "Accounts");
            if (!hasAccounts || HasMigration(connection)) return;

            // This database predates migrations. EnsureCreated already created its
            // tables, so baseline it instead of replaying the initial full-schema migration.
            if (!HasColumn(connection, "Users", "LastLoginAt"))
            {
                using var addColumn = connection.CreateCommand();
                addColumn.CommandText = "ALTER TABLE Users ADD COLUMN LastLoginAt TEXT NULL;";
                addColumn.ExecuteNonQuery();
            }

            using var createHistory = connection.CreateCommand();
            createHistory.CommandText = "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);";
            createHistory.ExecuteNonQuery();

            using var insertHistory = connection.CreateCommand();
            insertHistory.CommandText = "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20261002145942_AddUserLastLogin', '10.0.12');";
            insertHistory.ExecuteNonQuery();
        }
        finally
        {
            connection.Close();
        }
    }

    private static bool Exists(IDbConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name LIMIT 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        return command.ExecuteScalar() != null;
    }

    private static bool HasColumn(IDbConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool HasMigration(IDbConnection connection)
    {
        if (!Exists(connection, "__EFMigrationsHistory")) return false;
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @id LIMIT 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = "20261002145942_AddUserLastLogin";
        command.Parameters.Add(parameter);
        return command.ExecuteScalar() != null;
    }
}
