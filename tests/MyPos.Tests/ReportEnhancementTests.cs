using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using Xunit;

namespace MyPos.Tests;

public class ReportEnhancementTests
{
    private static MyPosDbContext CreateDb()
    {
        var db = new MyPosDbContext(new DbContextOptionsBuilder<MyPosDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection(); DbInitializer.Initialize(db); return db;
    }

    private static User Cashier(MyPosDbContext db, string name)
    {
        var user = new User { Username = name, FullName = name, Role = UserRole.Cashier };
        db.Users.Add(user); db.SaveChanges(); return user;
    }

    private static Product Product(MyPosDbContext db, Guid admin)
    {
        var product = new Product { Name = "Item", Price = 112m };
        db.Products.Add(product); db.SaveChanges();
        new SaleService(db).ReceiveStock(product.Id, 10, 50m, admin); return product;
    }

    [Fact]
    public void Admin_assigns_drawer_and_other_cashier_cannot_sell_move_cash_close_or_override()
    {
        using var db = CreateDb();
        var admin = db.Users.Single(); var branch = db.Branches.Single();
        var cashier = Cashier(db, "assigned"); var other = Cashier(db, "other");
        var product = Product(db, admin.Id);
        var shifts = new ShiftService(db); var sales = new SaleService(db);
        shifts.ConfigureDrawer(admin.Id, DrawerMode.Assigned, cashier.Id);
        var shift = shifts.OpenShift(branch.Id, admin.Id, 100m);
        Assert.True(shifts.CanUseDrawer(shift, admin.Id)); Assert.True(shifts.CanUseDrawer(shift, cashier.Id));
        Assert.False(shifts.CanUseDrawer(shift, other.Id));
        Assert.Throws<InvalidOperationException>(() => sales.PostSale(branch.Id, other.Id, new[] { new CartLine(product.Id, 1) }, tendered: 112));
        Assert.Throws<InvalidOperationException>(() => shifts.RecordCashMovement(other.Id, CashMovementType.CashOut, 1, "Denied"));
        Assert.Throws<InvalidOperationException>(() => shifts.CloseShift(other.Id, 100, null));
        var sale = sales.PostSale(branch.Id, cashier.Id, new[] { new CartLine(product.Id, 1) }, tendered: 112);
        Assert.Equal(shift.Id, sale.CashShiftId);
        Assert.Throws<InvalidOperationException>(() => new ShiftReportService(db).GetReport(shift.Id, other.Id));
        shifts.CloseShift(cashier.Id, 212, null);
        Assert.Throws<InvalidOperationException>(() => shifts.ConfigureDrawer(other.Id, DrawerMode.Shared));
        Assert.Throws<InvalidOperationException>(() => shifts.OpenShift(branch.Id, other.Id, 0));
        var next = shifts.OpenShift(branch.Id, cashier.Id, 0);
        Assert.Equal(DrawerMode.Assigned, next.DrawerMode); Assert.Equal(cashier.Id, next.AssignedCashierId);
    }

    [Fact]
    public void Shared_drawer_accepts_multiple_cashiers_and_rejects_non_admin_voids()
    {
        using var db = CreateDb();
        var admin = db.Users.Single(); var branch = db.Branches.Single();
        var cashier = Cashier(db, "one"); var other = Cashier(db, "two");
        var product = Product(db, admin.Id);
        var shift = new ShiftService(db).OpenShift(branch.Id, admin.Id, 0);
        var sales = new SaleService(db);
        var sale = sales.PostSale(branch.Id, cashier.Id, new[] { new CartLine(product.Id, 1) }, tendered: 112);
        sales.PostSale(branch.Id, other.Id, new[] { new CartLine(product.Id, 1) }, tendered: 112, method: PaymentMethod.Gcash);
        Assert.Throws<InvalidOperationException>(() => sales.VoidSale(sale.Id, cashier.Id, "Denied"));
        Assert.False(sale.IsVoided);
        admin.IsActive = false; db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => sales.VoidSale(sale.Id, admin.Id, "Inactive approval"));
        admin.IsActive = true; db.SaveChanges();
        sales.VoidSale(sale.Id, admin.Id, "Approved cancellation");
        Assert.Equal(admin.Id, sale.VoidedById); Assert.Equal(shift.Id, sale.VoidCashShiftId);
        var report = new ShiftReportService(db).GetReport(shift.Id, cashier.Id);
        Assert.Equal(112, report.CurrentSales); Assert.Equal(112, report.PaymentTotal(PaymentMethod.Gcash));
        Assert.Equal(0, report.PaymentTotal(PaymentMethod.Cash)); Assert.Equal(1, report.VoidCount);
    }

    [Fact]
    public void Later_void_updates_current_report_without_rewriting_original_close_or_double_refund()
    {
        using var db = CreateDb();
        var admin = db.Users.Single(); var branch = db.Branches.Single(); var product = Product(db, admin.Id);
        var shifts = new ShiftService(db); var sales = new SaleService(db);
        var first = shifts.OpenShift(branch.Id, admin.Id, 100);
        var sale = sales.PostSale(branch.Id, admin.Id, new[] { new CartLine(product.Id, 1) }, tendered: 112);
        shifts.CloseShift(admin.Id, 212, null);
        var second = shifts.OpenShift(branch.Id, admin.Id, 200);
        sales.VoidSale(sale.Id, admin.Id, "Later approved refund");
        var report = new ShiftReportService(db).GetReport(first.Id, admin.Id);
        Assert.Equal(0, report.CurrentSales); Assert.Equal(1, report.LaterVoidCount);
        Assert.Equal(212, report.ExpectedDrawer); Assert.Equal(212, first.CountedDrawer); Assert.Equal(0, first.Variance);
        Assert.Equal(-112, shifts.GetShiftTotals(second.Id).CashSales);
        Assert.Equal(88, new ShiftReportService(db).GetReport(second.Id, admin.Id).ExpectedDrawer);
    }

    [Fact]
    public void Empty_shift_report_is_available_and_invalid_assignment_is_rejected()
    {
        using var db = CreateDb(); var admin = db.Users.Single(); var branch = db.Branches.Single();
        var shifts = new ShiftService(db);
        Assert.Throws<InvalidOperationException>(() => shifts.ConfigureDrawer(admin.Id, DrawerMode.Assigned, Guid.NewGuid()));
        var shift = shifts.OpenShift(branch.Id, admin.Id, 50);
        var report = new ShiftReportService(db).GetReport(shift.Id, admin.Id);
        Assert.Empty(report.Sales); Assert.Equal(50, report.ExpectedDrawer);
    }

    [Fact]
    public void Policy_changes_only_affect_next_shift_and_validate_admin_and_cashier()
    {
        using var db = CreateDb();
        var admin = db.Users.Single(); var branch = db.Branches.Single();
        var one = Cashier(db, "one"); var two = Cashier(db, "two");
        var shifts = new ShiftService(db);
        shifts.ConfigureDrawer(admin.Id, DrawerMode.Assigned, one.Id);
        var first = shifts.OpenShift(branch.Id, one.Id, 100);
        shifts.ConfigureDrawer(admin.Id, DrawerMode.Assigned, two.Id);
        Assert.Equal(one.Id, first.AssignedCashierId);
        Assert.True(shifts.CanUseDrawer(first, one.Id)); Assert.False(shifts.CanUseDrawer(first, two.Id));
        Assert.Throws<InvalidOperationException>(() => shifts.ConfigureDrawer(one.Id, DrawerMode.Shared));
        Assert.Throws<InvalidOperationException>(() => shifts.ConfigureDrawer(admin.Id, (DrawerMode)99));
        Assert.Throws<InvalidOperationException>(() => shifts.ConfigureDrawer(admin.Id, DrawerMode.Assigned, admin.Id));
        shifts.CloseShift(one.Id, 100, null);
        Assert.Throws<InvalidOperationException>(() => shifts.OpenShift(branch.Id, one.Id, 0));
        var next = shifts.OpenShift(branch.Id, two.Id, 50);
        Assert.Equal(two.Id, next.AssignedCashierId);
        shifts.ConfigureDrawer(admin.Id, DrawerMode.Shared);
        Assert.Equal(DrawerMode.Assigned, next.DrawerMode);
        shifts.CloseShift(two.Id, 50, null);
        var shared = shifts.OpenShift(branch.Id, one.Id, 0);
        Assert.Equal(DrawerMode.Shared, shared.DrawerMode); Assert.Null(shared.AssignedCashierId);
        shifts.CloseShift(one.Id, 0, null);
        two.IsActive = false; db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => shifts.ConfigureDrawer(admin.Id, DrawerMode.Assigned, two.Id));
        admin.IsActive = false; db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => shifts.ConfigureDrawer(admin.Id, DrawerMode.Shared));
    }

    [Fact]
    public void Sales_csv_uses_full_date_branch_and_invariant_ungrouped_numbers_and_escapes_text()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var report = new DailySalesReport { Date = new DateOnly(2026, 10, 9), Sales = new() {
                new Sale { SaleDate = new DateTime(2026, 10, 9, 13, 2, 3), ReceiptNumber = "A\"1",
                    CustomerName = "Name, customer", TotalAmount = 13080m, Payments = new() { new Payment { Method = PaymentMethod.Cash } } }
            } };
            var csv = ReportCsv.DailySales(report, "Main");
            Assert.Contains("2026-10-09,13:02:03,\"Main\"", csv);
            Assert.Contains("\"A\"\"1\"", csv); Assert.Contains("\"Name, customer\"", csv);
            Assert.Contains(",13080.00,", csv); Assert.DoesNotContain("13,080", csv);
            using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(new System.IO.StringReader(csv));
            parser.SetDelimiters(",");
            Assert.Equal(14, parser.ReadFields()!.Length);
            var fields = parser.ReadFields()!;
            Assert.Equal(14, fields.Length); Assert.Equal("13080.00", fields[10]);
            Assert.Equal("\"'=SUM(1,2)\"", ReportCsv.Text("=SUM(1,2)"));
            Assert.Equal("-12.00", ReportCsv.Money(-12));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void Reports_refresh_void_status_even_when_context_has_an_older_tracked_sale()
    {
        using var db = CreateDb(); var admin = db.Users.Single(); var branch = db.Branches.Single();
        var product = Product(db, admin.Id);
        var shift = new ShiftService(db).OpenShift(branch.Id, admin.Id, 0);
        var sale = new SaleService(db).PostSale(branch.Id, admin.Id,
            new[] { new CartLine(product.Id, 1) }, tendered: 112);
        var voidedAt = DateTime.Now;
        db.Database.ExecuteSqlInterpolated($"UPDATE Sales SET IsVoided = {true}, VoidedAt = {voidedAt}, VoidCashShiftId = {shift.Id} WHERE Id = {sale.Id}");
        Assert.False(sale.IsVoided); // Simulate the stale context after an external update.
        Assert.Equal(0, new ReportService(db).GetDailySales(sale.SaleDate, branch.Id).TransactionCount);
        var report = new ShiftReportService(db).GetReport(shift.Id, admin.Id);
        Assert.Equal(0, report.CurrentSales); Assert.Equal(0, report.DrawerTotals.CashSales);
    }
}
