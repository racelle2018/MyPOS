using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;
using Xunit;

namespace RetailForma.Tests;

public class ShiftServiceTests
{
    [Fact]
    public void Counting_minimum_is_persistent_admin_only_and_preserves_saved_history()
    {
        using var db = CreateDb(); var admin = db.Users.Single(); var service = new ShiftService(db);
        Assert.Equal(0.25m, service.GetMinimumCountingDenomination());
        Assert.DoesNotContain(0.10m, service.GetCountingDenominations());
        Assert.Contains(0.25m, service.GetCountingDenominations());
        var cashier = new User { Username = "count-cashier", FullName = "Cashier", Role = UserRole.Cashier };
        db.Users.Add(cashier); db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => service.ConfigureCountingMinimum(cashier.Id, 1));
        Assert.Throws<InvalidOperationException>(() => service.ConfigureCountingMinimum(admin.Id, 0.20m));
        service.ConfigureCountingMinimum(admin.Id, 0.01m);
        var shift = service.OpenShift(db.Branches.Single().Id, admin.Id, 0.10m, floatCounterpartCode: "1005");
        service.CloseShift(admin.Id, 0.10m, null, new Dictionary<decimal, int> { [0.10m] = 1 });
        service.ConfigureCountingMinimum(admin.Id, 1m);
        Assert.Equal(1m, new ShiftService(db).GetMinimumCountingDenomination());
        Assert.All(service.GetCountingDenominations(), d => Assert.True(d >= 1m));
        Assert.Equal(0.10m, db.DenominationCounts.Single(c => c.ShiftId == shift.Id).FaceValue);
        Assert.Equal("1.00", db.Settings.Single(s => s.Key == ShiftService.MinimumCountingDenominationKey).Value);
        Assert.Equal(2, db.AuditLogs.Count(a => a.Action == "CashCountingSettings"));
        admin.IsActive = false; db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => service.ConfigureCountingMinimum(admin.Id, 0.25m));
    }

    [Fact]
    public void Counting_rechecks_changed_minimum_without_rounding_manual_cash_totals()
    {
        using var db = CreateDb(); var admin = db.Users.Single(); var service = new ShiftService(db);
        var shift = service.OpenShift(db.Branches.Single().Id, admin.Id, 0.25m, floatCounterpartCode: "1005");
        service.ConfigureCountingMinimum(admin.Id, 1m);
        Assert.Throws<InvalidOperationException>(() => service.CloseShift(admin.Id, 0.25m, null,
            new Dictionary<decimal, int> { [0.25m] = 1 }));
        Assert.Null(shift.ClosedAt);
        service.CloseShift(admin.Id, 0.25m, null);
        Assert.Equal(0.25m, shift.CountedDrawer);
        Assert.Equal(0m, shift.Variance);
    }

    [Fact]
    public void Closing_requires_review_of_fresh_totals_and_preserves_shift_on_rejection()
    {
        using var db = CreateDb();
        var user = db.Users.Single();
        var service = new ShiftService(db);
        var shift = service.OpenShift(db.Branches.Single().Id, user.Id, 100m, floatCounterpartCode: "1005");
        var reviewed = service.GetShiftTotals(shift.Id);
        service.RecordCashMovement(user.Id, CashMovementType.CashIn, 10m, "Added float", category: CashMovementCategory.FloatTransfer, companyOwnedDestination: true);
        Assert.Throws<InvalidOperationException>(() => service.CloseShift(user.Id, 100m, null,
            shiftId: shift.Id, reviewedTotals: reviewed));
        Assert.Null(shift.ClosedAt);
        Assert.Empty(db.AuditLogs.Where(a => a.Action == "ShiftClose"));
        var fresh = service.GetShiftTotals(shift.Id);
        service.CloseShift(user.Id, 110m, null, shiftId: shift.Id, reviewedTotals: fresh);
        Assert.Equal(110m, shift.ExpectedDrawer);
        Assert.Equal(0m, shift.Variance);
    }

    [Fact]
    public void Stale_closing_screen_cannot_close_a_replacement_shift()
    {
        using var db = CreateDb();
        var user = db.Users.Single();
        var service = new ShiftService(db);
        var first = service.OpenShift(db.Branches.Single().Id, user.Id, 100m, floatCounterpartCode: "1005");
        service.CloseShift(user.Id, 100m, null, shiftId: first.Id);
        var second = service.OpenShift(db.Branches.Single().Id, user.Id, 50m, floatCounterpartCode: "1005");
        Assert.Throws<InvalidOperationException>(() => service.CloseShift(user.Id, 100m, null, shiftId: first.Id));
        Assert.Null(second.ClosedAt);
        Assert.Throws<InvalidOperationException>(() => service.CloseShift(user.Id, 50.001m, null, shiftId: second.Id));
        Assert.Null(second.ClosedAt);
        Assert.Single(db.AuditLogs.Where(a => a.Action == "ShiftClose"));
    }

    [Fact]
    public void Close_counts_net_cash_once_and_posts_balanced_shortage()
    {
        using var db = CreateDb();
        var branch = db.Branches.Single();
        var user = db.Users.Single();
        var productId = AddProduct(db, branch.Id, user.Id);
        var shifts = new ShiftService(db);
        var sales = new SaleService(db);

        var shift = shifts.OpenShift(branch.Id, user.Id, 100m, floatCounterpartCode: "1005");
        var voidedSale = sales.PostSale(branch.Id, user.Id, new[] { new CartLine(productId, 1m) }, tendered: 50m);
        sales.VoidSale(voidedSale.Id, user.Id, "Mistake");
        sales.PostSale(branch.Id, user.Id, new[] { new CartLine(productId, 1m) }, tendered: 50m);
        sales.PostSale(branch.Id, user.Id, new[] { new CartLine(productId, 1m) }, tendered: 50m, method: PaymentMethod.Card);
        var cashIn = shifts.RecordCashMovement(user.Id, CashMovementType.CashIn, 20m, " Float added ", category: CashMovementCategory.FloatTransfer, companyOwnedDestination: true);
        Assert.Equal("Float added", cashIn.Reason);
        shifts.RecordCashMovement(user.Id, CashMovementType.CashOut, 10m, "Safe drop", category: CashMovementCategory.FloatTransfer, companyOwnedDestination: true);

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

        var first = shifts.OpenShift(branch.Id, user.Id, 0m, floatCounterpartCode: "1005");
        var firstSale = sales.PostSale(branch.Id, user.Id, new[] { new CartLine(productId, 1m) }, tendered: 50m);
        shifts.CloseShift(user.Id, 50m, null);
        var second = shifts.OpenShift(branch.Id, user.Id, 0m, floatCounterpartCode: "1005");
        sales.PostSale(branch.Id, user.Id, new[] { new CartLine(productId, 1m) }, tendered: 50m);
        sales.VoidSale(firstSale.Id, user.Id, "Later return");

        var firstTotals = shifts.GetShiftTotals(first.Id);
        var secondTotals = shifts.GetShiftTotals(second.Id);
        Assert.Equal(50m, firstTotals.CashSales);
        Assert.Equal(0m, firstTotals.VoidedCashSales);
        Assert.Equal(0m, secondTotals.CashSales);
        Assert.Equal(50m, secondTotals.VoidedCashSales);
    }

    private static RetailFormaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<RetailFormaDbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        var db = new RetailFormaDbContext(options);
        db.Database.OpenConnection();
        DbInitializer.Initialize(db);
        return db;
    }

    private static Guid AddProduct(RetailFormaDbContext db, Guid branchId, Guid userId)
    {
        var product = new Product { Name = "Test item", Barcode = Guid.NewGuid().ToString("N"), Price = 50m };
        db.Products.Add(product);
        db.SaveChanges();
        new SaleService(db).ReceiveStock(product.Id, 10m, 20m, userId);
        return product.Id;
    }
}
