using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RetailForma.ServiceApplication;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.EncryptedStorage;

namespace RetailForma.EncryptedStorage.Tests;

public sealed class ApplicationOperationsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "RetailForma-service-test-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    private readonly ApplicationOperations operations;
    private const string Password = "Individual test password 2026!";
    private readonly Guid owner = Guid.NewGuid(), cashier = Guid.NewGuid();
    private string DatabasePath => Path.Combine(directory, "isolated.db");
    private bool licensed = true;
    public ApplicationOperationsTests()
    {
        Directory.CreateDirectory(directory);
        using (SqlCipherConnections.CreateNew(DatabasePath, key)) { }
        using (var db = Open())
        {
            DbInitializer.Initialize(db); // Actual EF migrations on the Community native provider.
            db.Users.AddRange(new User { Id = owner, Username = "owner", FullName = "Owner", Role = UserRole.Admin, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) },
                new User { Id = cashier, Username = "cashier", FullName = "Cashier", Role = UserRole.Cashier, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) });
            db.Products.Add(new Product { Name = "TEST PRODUCT", CostPrice = 4, Price = 10, StockQty = 10 });
            db.Settings.Add(new Setting { Key = "BackupRecoverySecret", Value = "MUST NOT LEAVE THE SERVICE" });
            db.SaveChanges();
        }
        operations = new(Open, () => { if (!licensed) throw new InvalidOperationException("Test license rejected."); });
    }
    private RetailFormaDbContext Open() => EncryptedDbContexts.OpenExisting(DatabasePath, key);
    private object? Call(ApplicationSession session, Operation operation, object? input = null) =>
        operations.Execute(session, operation, WireJson.Element(input ?? new { }));
    private ApplicationSession Login(string username)
    {
        var session = new ApplicationSession("TEST-WINDOWS-IDENTITY");
        Call(session, Operation.Login, new LoginInput(username, Password)); return session;
    }

    [Fact]
    public void EncryptedEfMigrationsAndServiceLoginDoNotExposeHashesOrBackupSecrets()
    {
        var session = Login("cashier");
        var user = Assert.IsType<ClientUser>(Call(session, Operation.CurrentUser));
        Assert.Equal(cashier, user.Id);
        var payload = JsonSerializer.Serialize(Call(session, Operation.Staff));
        Assert.DoesNotContain("PasswordHash", payload);
        Assert.DoesNotContain("$2", payload);
        var settings = Assert.IsType<Dictionary<string, string>>(Call(session, Operation.Settings));
        Assert.DoesNotContain("BackupRecoverySecret", settings.Keys);
        var products = Assert.IsType<List<Product>>(Call(session, Operation.Products));
        Assert.All(products, p => Assert.Equal(0, p.CostPrice));
        var ownerProducts = Assert.IsType<List<Product>>(Call(Login("owner"), Operation.Products));
        Assert.Contains(ownerProducts, p => p.CostPrice == 4);
    }

    [Fact]
    public void CashierCannotForgeActorRoleSettingsOrProductWrites()
    {
        var session = Login("cashier");
        Assert.Throws<UnauthorizedAccessException>(() => Call(session, Operation.SaveProduct,
            new ProductInput(null, "FORGED", null, "", "PC", 12, false)));
        Assert.Throws<UnauthorizedAccessException>(() => Call(session, Operation.SaveSettings,
            new SettingsInput(new() { ["BackupRecoverySecret"] = "forged" })));
        Assert.Throws<UnauthorizedAccessException>(() => Call(session, Operation.AuditLog,
            new AuditInput(DateTime.Today, DateTime.Today.AddDays(1))));
        Assert.Throws<UnauthorizedAccessException>(() => Call(session, (Operation)9999));
        var adminSession = Login("owner");
        Assert.Throws<JsonException>(() => operations.Execute(adminSession, Operation.ActivateProduct,
            WireJson.Element(new { Id = Guid.NewGuid(), Active = false, UserId = owner, Role = 3 })));
        using var db = Open(); Assert.False(db.Products.Any(p => p.Name == "FORGED"));
        Assert.Contains(db.AuditLogs, a => a.Action == "PermissionDenied" && a.UserId == cashier);
    }

    [Fact]
    public void LockLogoutDisablePasswordChangeAndLicenseAreEnforcedInTheService()
    {
        var session = Login("owner");
        Call(session, Operation.Lock);
        Assert.Throws<UnauthorizedAccessException>(() => Call(session, Operation.Products));
        Call(session, Operation.Unlock, new PasswordInput(Password));
        licensed = false;
        Assert.Throws<InvalidOperationException>(() => Call(session, Operation.SaveProduct,
            new ProductInput(null, "UNLICENSED", null, "", "PC", 12, false)));
        Call(session, Operation.Products); // Reports/recovery/read access does not depend on paid/trial posting status.
        var second = Login("owner");
        Call(session, Operation.ChangePassword, new ChangePasswordInput(Password, Password + " changed", Password + " changed"));
        Assert.Throws<UnauthorizedAccessException>(() => Call(second, Operation.Products));
        Call(session, Operation.Logout); Assert.Throws<UnauthorizedAccessException>(() => Call(session, Operation.Products));
        var disabled = Login("cashier");
        using (var db = Open()) { db.Users.Find(cashier)!.IsActive = false; db.SaveChanges(); }
        Assert.Throws<UnauthorizedAccessException>(() => Call(disabled, Operation.Products));
    }

    [Fact]
    public void InitialPasswordMustChangeAndAuthenticationIsRateLimitedByWindowsIdentity()
    {
        var initial = new ApplicationSession("TEST-INITIAL");
        Assert.True(Assert.IsType<ClientUser>(Call(initial, Operation.Login, new LoginInput("admin", "admin123"))).MustChangePassword);
        Assert.Throws<UnauthorizedAccessException>(() => Call(initial, Operation.Products));
        Call(initial, Operation.ChangePassword, new ChangePasswordInput("admin123", Password, Password));
        Call(initial, Operation.Products);
        var attacker = new ApplicationSession("TEST-ATTACKER");
        for (var i = 0; i < 5; i++)
            Assert.Throws<UnauthorizedAccessException>(() => Call(attacker, Operation.Login, new LoginInput("owner", "wrong")));
        var blocked = Assert.Throws<UnauthorizedAccessException>(() => Call(attacker, Operation.Login, new LoginInput("owner", Password)));
        Assert.Contains("one minute", blocked.Message);
    }

    public void Dispose() { CryptographicOperations.ZeroMemory(key); Directory.Delete(directory, true); }
}
