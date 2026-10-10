using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using Xunit;

namespace MyPos.Tests;

public class ShiftServiceTests
{
    [Fact]
    public void Close_counts_net_cash_once_and_posts_balanced_shortage()
    {
        using var db = CreateDb();
        var branch = db.Branches.Single();
        var user = db.Users.Single();
        var productId = AddProduct(db, branch.Id, user.Id);
        var shifts = new ShiftService(db);
        var sales = new SaleService(db);

        var shift = shifts.OpenShift(branch.Id, user.Id, 100m);
        var voidedSale = sales.PostSale(branch.Id, user.Id, new[] { new CartLine(productId, 1m) }, tendered: 50m);
        sales.VoidSale(voidedSale.Id, user.Id, "Mistake");
        sales.PostSale(branch.Id, user.Id, new[] { new CartLine(productId, 1m) }, tendered: 50m);
        sales.PostSale(branch.Id, user.Id, new[] { new CartLine(productId, 1m) }, tendered: 50m, method: PaymentMethod.Card);
        var cashIn = shifts.RecordCashMovement(user.Id, CashMovementType.CashIn, 20m, " Float added ");
        Assert.Equal("Float added", cashIn.Reason);
        shifts.RecordCashMovement(user.Id, CashMovementType.CashOut, 10m, "Safe drop");

        var totals = shifts.GetShiftTotals(shift.Id);
        Assert.Equal(50m, totals.CashSales);
        Assert.Equal(50m, totals.VoidedCashSales);
        Assert.Equal(50m, totals.NonCashSales);
        Assert.Equal(2, totals.TransactionCount);

        var closed = shifts.CloseShift(user.Id, 155m, "Counted");
        Assert.Equal(160m, closed.ExpectedDrawer);
        Assert.Equal(-5m, closed.Variance);
        var entry = db.JournalEntries.Include(e => e.Lines)
            .Single(e => e.SourceType == "ShiftClose" && e.SourceId == shift.Id);
        Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void Closed_shift_totals_do_not_change_after_later_sales_or_void()
    {
        using var db = CreateDb();
        var branch = db.Branches.Single();
        var user = db.Users.Single();
        var productId = AddProduct(db, branch.Id, user.Id);
        var shifts = new ShiftService(db);
        var sales = new SaleService(db);

        var first = shifts.OpenShift(branch.Id, user.Id, 0m);
        var firstSale = sales.PostSale(branch.Id, user.Id, new[] { new CartLine(productId, 1m) }, tendered: 50m);
        shifts.CloseShift(user.Id, 50m, null);
        var second = shifts.OpenShift(branch.Id, user.Id, 0m);
        sales.PostSale(branch.Id, user.Id, new[] { new CartLine(productId, 1m) }, tendered: 50m);
        sales.VoidSale(firstSale.Id, user.Id, "Later return");

        var firstTotals = shifts.GetShiftTotals(first.Id);
        var secondTotals = shifts.GetShiftTotals(second.Id);
        Assert.Equal(50m, firstTotals.CashSales);
        Assert.Equal(0m, firstTotals.VoidedCashSales);
        Assert.Equal(0m, secondTotals.CashSales);
        Assert.Equal(50m, secondTotals.VoidedCashSales);
    }

    private static MyPosDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MyPosDbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        var db = new MyPosDbContext(options);
        db.Database.OpenConnection();
        DbInitializer.Initialize(db);
        return db;
    }

    private static Guid AddProduct(MyPosDbContext db, Guid branchId, Guid userId)
    {
        var product = new Product { Name = "Test item", Barcode = Guid.NewGuid().ToString("N"), Price = 50m };
        db.Products.Add(product);
        db.SaveChanges();
        new SaleService(db).ReceiveStock(product.Id, 10m, 20m, userId);
        return product.Id;
    }
}
