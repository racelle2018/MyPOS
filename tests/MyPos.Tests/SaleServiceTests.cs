using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using Xunit;

namespace MyPos.Tests;

public class SaleServiceTests
{
    private sealed class Setup : IDisposable
    {
        public MyPosDbContext Db = null!;
        public SaleService Svc = null!;
        public Guid BranchId;
        public Guid CashierId;

        public Setup()
        {
            var options = new DbContextOptionsBuilder<MyPosDbContext>()
                .UseSqlite("Data Source=:memory:").Options;
            Db = new MyPosDbContext(options);
            Db.Database.OpenConnection();
            DbInitializer.Initialize(Db);
            Svc = new SaleService(Db);
            BranchId = Db.Branches.First().Id;
            CashierId = Db.Users.First().Id;
        }

        public Guid AddProduct(string name, decimal price, decimal cost, decimal qty, bool vatExempt = false)
        {
            var p = new Product { Name = name, Barcode = Guid.NewGuid().ToString("N"),
                                  Price = price, IsVatExempt = vatExempt };
            Db.Products.Add(p);
            Db.SaveChanges();
            Svc.ReceiveStock(p.Id, qty, cost, CashierId);
            return p.Id;
        }

        public void Dispose() => Db.Dispose();
    }

    private static decimal NetDebit(MyPosDbContext db, string accountCode)
    {
        var accountId = db.Accounts.First(a => a.Code == accountCode).Id;
        // ToList() first: SQLite stores decimals as TEXT — always sum in memory
        return db.JournalLines.Where(l => l.AccountId == accountId).ToList()
            .Sum(l => l.Debit - l.Credit);
    }

    [Fact]
    public void Senior_discount_is_vat_exempt_and_twenty_percent_off_net()
    {
        using var s = new Setup();
        var id = s.AddProduct("Medicine", 112m, 50m, 10m);
        var sale = s.Svc.PostSale(s.BranchId, s.CashierId, new List<CartLine> { new(id, 1m) }, tendered: 100m,
            discountKind: DiscountKind.SeniorPwd, seniorIdNumber: "SC-12345");
        Assert.Equal(80m, sale.TotalAmount);
        Assert.Equal(80m, sale.NetAmount);
        Assert.Equal(0m, sale.VatAmount);
        Assert.Equal(32m, sale.DiscountAmount);
        Assert.Equal("SC-12345", sale.SeniorIdNumber);
    }

    [Fact]
    public void Senior_discount_rejects_stacking_and_missing_id()
    {
        using var s = new Setup();
        var id = s.AddProduct("Medicine", 112m, 50m, 10m);
        Assert.Throws<InvalidOperationException>(() => s.Svc.PostSale(s.BranchId, s.CashierId,
            new List<CartLine> { new(id, 1m) }, discount: 10m, tendered: 200m,
            discountKind: DiscountKind.SeniorPwd, seniorIdNumber: "SC-1"));
        Assert.Throws<InvalidOperationException>(() => s.Svc.PostSale(s.BranchId, s.CashierId,
            new List<CartLine> { new(id, 1m) }, tendered: 200m, discountKind: DiscountKind.SeniorPwd));
    }

    [Fact]
    public void Senior_discount_handles_vat_and_exempt_lines()
    {
        using var s = new Setup();
        var medicine = s.AddProduct("Medicine", 112m, 50m, 10m);
        var rice = s.AddProduct("Rice", 250m, 180m, 10m, vatExempt: true);
        var sale = s.Svc.PostSale(s.BranchId, s.CashierId,
            new List<CartLine> { new(medicine, 1m), new(rice, 1m) }, tendered: 300m,
            discountKind: DiscountKind.SeniorPwd, seniorIdNumber: "PWD-99");
        Assert.Equal(280m, sale.TotalAmount);
        Assert.Equal(0m, sale.VatAmount);
    }

    [Fact]
    public void Vat_rounds_net_and_derives_vat_by_subtraction()
    {
        using var s = new Setup();
        var id = s.AddProduct("Coffee", 100.00m, 50m, 10m);

        var sale = s.Svc.PostSale(s.BranchId, s.CashierId,
            new List<CartLine> { new(id, 1m) }, tendered: 500m);

        Assert.Equal(100.00m, sale.TotalAmount);
        Assert.Equal(89.29m, sale.NetAmount);   // 100 ÷ 1.12 = 89.2857… → 89.29
        Assert.Equal(10.71m, sale.VatAmount);   // 100 − 89.29 — never a drifting centavo
        Assert.Equal(400.00m, sale.ChangeAmount);
    }

    [Fact]
    public void Vat_exempt_items_carry_no_vat()
    {
        using var s = new Setup();
        var id = s.AddProduct("Rice", 100.00m, 60m, 10m, vatExempt: true);

        var sale = s.Svc.PostSale(s.BranchId, s.CashierId,
            new List<CartLine> { new(id, 2m) }, tendered: 200m);

        Assert.Equal(200.00m, sale.NetAmount);
        Assert.Equal(0.00m, sale.VatAmount);
    }

    [Fact]
    public void Sale_posts_a_balanced_double_entry_with_correct_accounts()
    {
        using var s = new Setup();
        var id = s.AddProduct("Coffee", 112.00m, 50m, 10m);

        var sale = s.Svc.PostSale(s.BranchId, s.CashierId,
            new List<CartLine> { new(id, 2m) }, tendered: 300m);      // total 224.00

        var entry = s.Db.JournalEntries.Include(e => e.Lines)
            .Single(e => e.SourceType == "Sale" && e.SourceId == sale.Id);
        var accounts = s.Db.Accounts.ToDictionary(a => a.Code);

        decimal Dr(string c) => entry.Lines.Where(l => l.AccountId == accounts[c].Id).Sum(l => l.Debit);
        decimal Cr(string c) => entry.Lines.Where(l => l.AccountId == accounts[c].Id).Sum(l => l.Credit);

        Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit)); // balances
        Assert.Equal(224.00m, Dr(AccountCodes.CashOnHand));
        Assert.Equal(200.00m, Cr(AccountCodes.SalesRevenue));   // 224 ÷ 1.12
        Assert.Equal(24.00m, Cr(AccountCodes.VatPayable));
        Assert.Equal(100.00m, Dr(AccountCodes.Cogs));           // 2 × 50
        Assert.Equal(100.00m, Cr(AccountCodes.Inventory));
    }

    [Fact]
    public void Sale_decrements_stock_and_writes_a_movement()
    {
        using var s = new Setup();
        var id = s.AddProduct("Coffee", 100m, 50m, 10m);

        var sale = s.Svc.PostSale(s.BranchId, s.CashierId,
            new List<CartLine> { new(id, 3m) }, tendered: 300m);

        Assert.Equal(7m, s.Db.Products.Find(id)!.StockQty);

        var m = s.Db.InventoryMovements.Single(x => x.SourceType == "Sale");
        Assert.Equal(MovementType.Sale, m.Type);
        Assert.Equal(3m, m.Qty);
        Assert.Equal(sale.Id, m.SourceId);
    }

    [Fact]
    public void Sale_numbers_are_gapless_even_across_voids()
    {
        using var s = new Setup();
        var id = s.AddProduct("Coffee", 100m, 50m, 10m);

        var s1 = s.Svc.PostSale(s.BranchId, s.CashierId, new List<CartLine> { new(id, 1m) }, tendered: 100m);
        var s2 = s.Svc.PostSale(s.BranchId, s.CashierId, new List<CartLine> { new(id, 1m) }, tendered: 100m);
        s.Svc.VoidSale(s2.Id, s.CashierId, "Wrong amount");
        var s3 = s.Svc.PostSale(s.BranchId, s.CashierId, new List<CartLine> { new(id, 1m) }, tendered: 100m);

        Assert.Equal(1, s1.SaleNumber);
        Assert.Equal(2, s2.SaleNumber);
        Assert.Equal(3, s3.SaleNumber);   // voided #2 is never reused
    }

    [Fact]
    public void Void_reverses_the_ledger_and_restores_stock()
    {
        using var s = new Setup();
        var id = s.AddProduct("Coffee", 112m, 50m, 10m);

        var sale = s.Svc.PostSale(s.BranchId, s.CashierId,
            new List<CartLine> { new(id, 2m) }, tendered: 300m);
        Assert.Equal(8m, s.Db.Products.Find(id)!.StockQty);

        s.Svc.VoidSale(sale.Id, s.CashierId, "Customer returned items");

        Assert.True(s.Db.Sales.Find(sale.Id)!.IsVoided);
        Assert.Equal(10m, s.Db.Products.Find(id)!.StockQty);   // stock restored

        // The sale's accounts now net to zero (the purchase entry never touched these)
        Assert.Equal(0m, NetDebit(s.Db, AccountCodes.VatPayable));
        Assert.Equal(0m, NetDebit(s.Db, AccountCodes.SalesRevenue));
        Assert.Equal(0m, NetDebit(s.Db, AccountCodes.Cogs));

        Assert.Throws<InvalidOperationException>(() => s.Svc.VoidSale(sale.Id, s.CashierId, "double void"));
    }

    [Fact]
    public void Discount_reduces_the_vat_base_and_allocates_exactly()
    {
        using var s = new Setup();
        var a = s.AddProduct("Item A", 33.33m, 10m, 10m);
        var b = s.AddProduct("Item B", 66.67m, 20m, 10m);

        var sale = s.Svc.PostSale(s.BranchId, s.CashierId,
            new List<CartLine> { new(a, 1m), new(b, 1m) }, discount: 10.00m, tendered: 500m);

        Assert.Equal(100.00m, sale.GrossAmount);
        Assert.Equal(10.00m, sale.DiscountAmount);
        Assert.Equal(90.00m, sale.TotalAmount);
        Assert.Equal(80.36m, sale.NetAmount);   // 30 → 26.79  +  60 → 53.57
        Assert.Equal(9.64m, sale.VatAmount);    // 90 − 80.36
        Assert.Equal(410.00m, sale.ChangeAmount);
    }

    [Fact]
    public void Failed_sale_leaves_no_trace()
    {
        using var s = new Setup();
        var id = s.AddProduct("Coffee", 100m, 50m, 10m);

        Assert.Throws<InvalidOperationException>(() => s.Svc.PostSale(
            s.BranchId, s.CashierId,
            new List<CartLine> { new(id, 99m) },      // more than on-hand stock
            tendered: 9900m));

        Assert.Equal(10m, s.Db.Products.Find(id)!.StockQty);  // rollback proved
        Assert.Empty(s.Db.Sales);
        Assert.Empty(s.Db.JournalEntries.Where(e => e.SourceType == "Sale").ToList());
    }
}
