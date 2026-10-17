using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;

namespace RetailForma.Tests;
public sealed class AccountingPostingTests : IDisposable
{
    private readonly RetailFormaDbContext db;
    private readonly Guid admin, branch;
    public AccountingPostingTests()
    {
        db = new(new DbContextOptionsBuilder<RetailFormaDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection(); DbInitializer.Initialize(db); admin = db.Users.First().Id; branch = db.Branches.First().Id;
    }
    public void Dispose() => db.Dispose();
    private Product Product()
    {
        var product = new Product { Name = "APPROVED STOCK", Barcode = Guid.NewGuid().ToString("N") };
        db.Products.Add(product); db.SaveChanges(); return product;
    }
    private decimal Net(string code)
    {
        var accountId = db.Accounts.Single(account => account.Code == code).Id;
        return db.JournalLines.Where(line => line.AccountId == accountId).AsEnumerable().Sum(line => line.Debit - line.Credit);
    }
    [Fact]
    public void Opening_inventory_changes_inventory_not_cash_and_cannot_be_repeated()
    {
        var p = Product(); var service = new SaleService(db);
        var movement = service.ReceiveStock(p.Id, 3, 20.1234m, admin, "Verified opening trial balance", receiptKind: StockReceiptKind.OpeningBalance);
        Assert.Equal(MovementType.OpeningBalance, movement.Type);
        Assert.Equal(60.37m, Net("1200")); Assert.Equal(-60.37m, Net("3990")); Assert.Equal(0m, Net("1000"));
        Assert.Throws<InvalidOperationException>(() => service.ReceiveStock(p.Id, 3, 20, admin, "Second opening", receiptKind: StockReceiptKind.OpeningBalance));
        Assert.Equal(3m, db.Products.AsNoTracking().Single(p2 => p2.Id == p.Id).StockQty);
    }
    [Fact]
    public void Shortage_posts_carrying_cost_to_inventory_loss_and_surplus_needs_cause()
    {
        var p = Product(); var service = new SaleService(db);
        service.ReceiveStock(p.Id, 10, 12.3456m, admin);
        service.AdjustStock(p.Id, -2, admin, "Confirmed damaged goods");
        Assert.Equal(24.69m, Net("5010")); Assert.Equal(98.77m, Net("1200"));
        Assert.Throws<InvalidOperationException>(() => service.AdjustStock(p.Id, 2, admin, "Unexplained surplus"));
        service.AdjustStock(p.Id, 1, admin, "Verified opening reconciliation correction", "3990");
        Assert.Equal(-12.35m, Net("3990"));
        Assert.Equal(9m, db.Products.AsNoTracking().Single(p2 => p2.Id == p.Id).StockQty);
    }
    [Fact]
    public void Cash_classifications_use_assets_expense_and_proprietor_drawings_not_generic_equity()
    {
        var service = new ShiftService(db); var shift = service.OpenShift(branch, admin, 100, "1005");
        service.RecordCashMovement(admin, CashMovementType.CashOut, 10, "Company bank deposit", CashMovementCategory.BankDeposit,
            reference: "SLIP", destination: "Company bank", companyOwnedDestination: true);
        service.RecordCashMovement(admin, CashMovementType.CashOut, 5, "Stationery", CashMovementCategory.Expense, expenseCategory: "Office");
        Assert.Equal(10m, Net("1010")); Assert.Equal(5m, Net("6000")); Assert.Equal(-100m, Net("1005"));
        Assert.Throws<InvalidOperationException>(() => service.RecordCashMovement(admin, CashMovementType.CashOut, 3, "Unmapped"));
        Assert.Throws<InvalidOperationException>(() => service.RecordCashMovement(admin, CashMovementType.CashOut, 3, "Personal draw", CashMovementCategory.OwnerWithdrawal));
        db.Settings.Add(new Setting { Key = "CompanyLegalForm", Value = "SoleProprietor" }); db.SaveChanges();
        service.RecordCashMovement(admin, CashMovementType.CashOut, 3, "Personal draw", CashMovementCategory.OwnerWithdrawal);
        Assert.Equal(3m, Net("3100")); Assert.Equal(0m, Net("3000"));
        Assert.Equal(18m, service.GetShiftTotals(shift.Id).CashOut);
    }
    [Fact]
    public void Missing_counterpart_rolls_back_cash_movement_audit_and_journal()
    {
        var service = new ShiftService(db); service.OpenShift(branch, admin, 0);
        var count = db.AuditLogs.Count();
        Assert.Throws<InvalidOperationException>(() => service.RecordCashMovement(admin, CashMovementType.CashOut, 1, "Unknown counterpart",
            CashMovementCategory.Other, counterpartAccountCode: "NOT-AN-ACCOUNT"));
        Assert.Empty(db.CashMovements); Assert.Equal(count, db.AuditLogs.Count()); Assert.Empty(db.JournalEntries);
    }
    [Fact]
    public void Genuine_zero_cost_needs_owner_confirmation_and_missing_cost_never_creates_fake_value()
    {
        var p = Product(); var service = new SaleService(db);
        Assert.Throws<InvalidOperationException>(() => service.ReceiveStock(p.Id, 1, 0, admin));
        service.ReceiveStock(p.Id, 1, 0, admin, "Verified free item carrying value", zeroCostConfirmed: true);
        Assert.Throws<InvalidOperationException>(() => service.AdjustStock(p.Id, -1, admin, "Unreviewed zero cost"));
        service.AdjustStock(p.Id, -1, admin, "Reviewed zero carrying cost", zeroCostConfirmed: true);
        Assert.Empty(db.JournalEntries); Assert.Equal(0m, db.Products.AsNoTracking().Single(p2 => p2.Id == p.Id).StockQty);
    }
    [Fact]
    public void Denied_license_prevents_stock_mutation_and_keeps_read_only_recovery_available()
    {
        var p = Product(); db.BusinessAuthorization = () => throw new InvalidOperationException("Trial online validation unavailable");
        Assert.Throws<InvalidOperationException>(() => new SaleService(db).ReceiveStock(p.Id, 1, 10, admin));
        Assert.Equal(0m, db.Products.AsNoTracking().Single(p2 => p2.Id == p.Id).StockQty);
        Assert.Empty(db.InventoryMovements);
        db.AuditLogs.Add(new AuditLog { Action = "RecoveryHelp", Date = DateTime.Now }); db.SaveChanges();
        Assert.Contains(db.AuditLogs, log => log.Action == "RecoveryHelp");
    }
    [Fact]
    public void Validation_failure_at_save_does_not_leak_failed_posting_into_later_save()
    {
        var p = Product(); var checks = 0;
        db.BusinessAuthorization = () => { if (++checks == 2) throw new InvalidOperationException("Connection lost before commit"); };
        Assert.Throws<InvalidOperationException>(() => new SaleService(db).ReceiveStock(p.Id, 2, 10, admin));
        Assert.Equal(0m, p.StockQty);
        db.BusinessAuthorization = null;
        db.AuditLogs.Add(new AuditLog { Action = "RecoveryHelp" }); db.SaveChanges();
        Assert.Empty(db.InventoryMovements); Assert.Empty(db.JournalEntries);
        Assert.Equal(0m, db.Products.AsNoTracking().Single(p2 => p2.Id == p.Id).StockQty);
        new SaleService(db).ReceiveStock(p.Id, 1, 10, admin);
        Assert.Single(db.InventoryMovements); Assert.Single(db.JournalEntries);
    }
}
