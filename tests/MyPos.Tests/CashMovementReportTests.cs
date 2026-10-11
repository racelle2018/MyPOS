using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using Xunit;
namespace MyPos.Tests;

public class CashMovementReportTests
{
    private static MyPosDbContext Db()
    {
        var db = new MyPosDbContext(new DbContextOptionsBuilder<MyPosDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection(); DbInitializer.Initialize(db); return db;
    }
    private static User Cashier(MyPosDbContext db, string name)
    {
        var user = new User { Username = name, FullName = name, Role = UserRole.Cashier };
        db.Users.Add(user); db.SaveChanges(); return user;
    }

    [Fact]
    public void Printed_scope_omits_defaults_and_preserves_selected_movement_and_shift_restrictions()
    {
        var date = DateOnly.FromDateTime(DateTime.Today);
        var report = new CashMovementReport(Guid.NewGuid(), "Main branch", date, date, null, new());
        var defaults = report.DescribePresentation(false);
        Assert.StartsWith("Period:", defaults);
        Assert.DoesNotContain("Filters:", defaults);
        Assert.Contains("All movements", report.FilterLabel);
        Assert.Contains("Search: None", report.FilterLabel);
        var shift = Guid.NewGuid();
        var selected = report with { Type = CashMovementType.CashOut, Category = CashMovementCategory.BankDeposit,
            Search = "  deposit  ", ShiftId = shift };
        var text = selected.DescribePresentation(true);
        Assert.Contains("Branch: Main branch", text);
        Assert.Contains("Type: Cash out", text);
        Assert.Contains("Classification: Bank deposit", text);
        Assert.Contains("Search: deposit", text);
        Assert.Contains("Shift: " + shift, text);
        Assert.DoesNotContain("All accessible", text);
        Assert.Equal(defaults, (selected with { Type = null, Category = null, Search = "  ", ShiftId = null }).DescribePresentation(false));
        Assert.Contains("Branch: Main branch", report.DescribePresentation(true));
    }

    [Fact]
    public void Cash_out_breakdown_uses_only_matching_cash_out_classifications_and_keeps_csv_values()
    {
        using var db = Db(); var admin = db.Users.Single(); var branch = db.Branches.Single();
        var shifts = new ShiftService(db); shifts.OpenShift(branch.Id, admin.Id, 0);
        shifts.RecordCashMovement(admin.Id, CashMovementType.CashIn, 5000, "Replenishment");
        var output = shifts.RecordCashMovement(admin.Id, CashMovementType.CashOut, 1200, "Deposit");
        output.Category = CashMovementCategory.BankDeposit; db.SaveChanges();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var service = new CashMovementReportService(db);
        var report = service.GetReport(branch.Id, admin.Id, date, date);
        Assert.Equal("Cash-out breakdown: Bank deposit: ₱1,200.00", report.CashOutBreakdown);
        Assert.DoesNotContain("Unclassified", report.CashOutBreakdown);
        Assert.Contains("1200.00", report.ToCsv());
        Assert.Contains("BankDeposit", report.ToCsv());
        Assert.Equal("", service.GetReport(branch.Id, admin.Id, date, date, CashMovementType.CashIn).CashOutBreakdown);
        Assert.Equal(3800, report.NetMovement);
    }

    [Fact]
    public void Range_is_inclusive_and_filters_by_movement_date_branch_and_type_not_shift_date()
    {
        using var db = Db();
        var admin = db.Users.Single(); var branch = db.Branches.Single();
        var shifts = new ShiftService(db);
        var shift = shifts.OpenShift(branch.Id, admin.Id, 1000);
        shift.OpenedAt = DateTime.Today.AddDays(-10);
        var input = shifts.RecordCashMovement(admin.Id, CashMovementType.CashIn, 25, "Float replenishment");
        var output = shifts.RecordCashMovement(admin.Id, CashMovementType.CashOut, 10, "Withdrawal");
        input.MovementDate = DateTime.Today; output.MovementDate = DateTime.Today.AddDays(1).AddTicks(-1);
        var previous = shifts.RecordCashMovement(admin.Id, CashMovementType.CashIn, 500, "Previous day");
        previous.MovementDate = DateTime.Today.AddTicks(-1);
        var tomorrow = shifts.RecordCashMovement(admin.Id, CashMovementType.CashIn, 600, "Future fixture");
        tomorrow.MovementDate = DateTime.Today.AddDays(1);
        var otherBranch = new Branch { Name = "Other branch" };
        var otherShift = new CashShift { BranchId = otherBranch.Id, UserId = admin.Id, OpenedAt = DateTime.Today };
        db.Branches.Add(otherBranch); db.CashShifts.Add(otherShift);
        db.CashMovements.Add(new CashMovement { ShiftId = otherShift.Id, UserId = admin.Id,
            Type = CashMovementType.CashIn, Amount = 700, MovementDate = DateTime.Today });
        db.SaveChanges();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var service = new CashMovementReportService(db);
        var report = service.GetReport(branch.Id, admin.Id, date, date);
        Assert.Equal(2, report.Rows.Count); Assert.Equal(25, report.CashIn); Assert.Equal(10, report.CashOut);
        Assert.Equal(15, report.NetMovement); Assert.All(report.Rows, r => Assert.Equal(shift.Id, r.ShiftId));
        var filtered = service.GetReport(branch.Id, admin.Id, date, date, CashMovementType.CashOut);
        Assert.Single(filtered.Rows); Assert.Equal(0, filtered.CashIn); Assert.Equal(-10, filtered.NetMovement);
    }

    [Fact]
    public void Access_uses_shift_snapshot_and_is_rechecked_for_active_users()
    {
        using var db = Db();
        var admin = db.Users.Single(); var branch = db.Branches.Single();
        var one = Cashier(db, "one"); var two = Cashier(db, "two");
        var shifts = new ShiftService(db); var service = new CashMovementReportService(db);
        var shared = shifts.OpenShift(branch.Id, admin.Id, 0);
        shifts.RecordCashMovement(one.Id, CashMovementType.CashIn, 3, "Shared");
        shifts.CloseShift(admin.Id, 3, null);
        shifts.ConfigureDrawer(admin.Id, DrawerMode.Assigned, one.Id);
        var assigned = shifts.OpenShift(branch.Id, one.Id, 0);
        shifts.RecordCashMovement(one.Id, CashMovementType.CashIn, 7, "Assigned");
        shifts.ConfigureDrawer(admin.Id, DrawerMode.Assigned, two.Id); // Next shift only.
        var date = DateOnly.FromDateTime(DateTime.Today);
        Assert.Equal(10, service.GetReport(branch.Id, one.Id, date, date).CashIn);
        var other = service.GetReport(branch.Id, two.Id, date, date);
        Assert.Single(other.Rows); Assert.Equal(shared.Id, other.Rows[0].ShiftId);
        Assert.DoesNotContain(other.ToCsv(), "Assigned");
        Assert.Equal(10, service.GetReport(branch.Id, admin.Id, date, date).CashIn);
        two.IsActive = false; db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => service.GetReport(branch.Id, two.Id, date, date));
        Assert.Throws<InvalidOperationException>(() => service.GetReport(branch.Id, Guid.NewGuid(), date, date));
    }

    [Fact]
    public void Invalid_ranges_types_and_branches_are_rejected_and_empty_reports_have_zero_totals()
    {
        using var db = Db(); var admin = db.Users.Single(); var branch = db.Branches.Single();
        var date = DateOnly.FromDateTime(DateTime.Today); var service = new CashMovementReportService(db);
        Assert.Throws<InvalidOperationException>(() => service.GetReport(branch.Id, admin.Id, date, date.AddDays(-1)));
        Assert.Throws<InvalidOperationException>(() => service.GetReport(branch.Id, admin.Id, date, date.AddDays(1)));
        Assert.Throws<InvalidOperationException>(() => service.GetReport(branch.Id, admin.Id, date, date, (CashMovementType)99));
        Assert.Throws<InvalidOperationException>(() => service.GetReport(Guid.NewGuid(), admin.Id, date, date));
        var report = service.GetReport(branch.Id, admin.Id, date, date);
        Assert.Empty(report.Rows); Assert.Equal(0, report.CashIn); Assert.Equal(0, report.CashOut); Assert.Equal(0, report.NetMovement);
    }

    [Fact]
    public void Refresh_reads_database_changes_and_csv_is_invariant_and_formula_safe()
    {
        using var db = Db(); var admin = db.Users.Single(); var branch = db.Branches.Single();
        var shifts = new ShiftService(db); shifts.OpenShift(branch.Id, admin.Id, 0);
        var entry = shifts.RecordCashMovement(admin.Id, CashMovementType.CashIn, 1, "=SUM(1,2)");
        var amount = 13080.25m;
        db.Database.ExecuteSqlInterpolated($"UPDATE CashMovements SET Amount = {amount} WHERE Id = {entry.Id}");
        var service = new CashMovementReportService(db); var date = DateOnly.FromDateTime(DateTime.Today);
        var report = service.GetReport(branch.Id, admin.Id, date, date);
        Assert.Equal(1, entry.Amount); Assert.Equal(amount, report.CashIn);
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(new System.IO.StringReader(report.ToCsv()));
            parser.SetDelimiters(",");
            Assert.Equal(20, parser.ReadFields()!.Length);
            var fields = parser.ReadFields()!;
            Assert.Equal(20, fields.Length); Assert.Equal("'=SUM(1,2)", fields[9]);
            Assert.Equal("13080.25", fields[12]); Assert.Equal(entry.ShiftId.ToString(), fields[5]);
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
