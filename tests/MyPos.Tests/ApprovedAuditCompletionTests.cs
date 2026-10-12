using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;
using MyPos.Core.Services;
using Xunit;
namespace MyPos.Tests;

public class ApprovedAuditCompletionTests
{
    [Fact]
    public void QRPh_is_a_separate_non_cash_method_without_reclassifying_wallet_payments()
    {
        using var db = Db(); var scope = Scope(db); var product = Product(db, "QR item", 112);
        var shifts = new ShiftService(db); var shift = shifts.OpenShift(scope.BranchId, scope.UserId, 0);
        var sales = new SaleService(db);
        Sale? qrSale = null;
        foreach (var method in new[] { PaymentMethod.Gcash, PaymentMethod.Maya, PaymentMethod.QRPh })
        {
            var sale = sales.PostSale(scope.BranchId, scope.UserId, new[] { new CartLine(product.Id, 1) },
                tendered: 112, method: method, reference: "ONLINE-REF");
            Assert.Equal(method, sale.Payments.Single().Method);
            if (method == PaymentMethod.QRPh) qrSale = sale;
        }
        Assert.Equal(3, (int)PaymentMethod.Gcash); Assert.Equal(4, (int)PaymentMethod.Maya);
        Assert.Equal(5, (int)PaymentMethod.Bank); Assert.Equal(6, (int)PaymentMethod.QRPh);
        Assert.Equal(0m, shifts.GetShiftTotals(shift.Id).CashSales);
        Assert.Equal(336m, shifts.GetShiftTotals(shift.Id).NonCashSales);
        var report = new ExpandedReportService(db).SalesSummary(scope with { Payment = PaymentMethod.QRPh });
        Assert.Contains("112.00", report.Summary);
        Assert.Contains("Payment: QRPh", report.PresentationScope);
        Assert.Single(report.Rows, r => r.Kind == "Payment" && r.Cells[0] == "QRPh" && r.Cells[4] == "112.00");
        sales.VoidSale(qrSale!.Id, scope.UserId, "QR cancellation");
        Assert.Equal(224m, shifts.GetShiftTotals(shift.Id).NonCashSales);
        Assert.Equal(0m, shifts.GetShiftTotals(shift.Id).CashSales);
    }

    private static MyPosDbContext Db()
    {
        var db = new MyPosDbContext(new DbContextOptionsBuilder<MyPosDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection(); DbInitializer.Initialize(db); return db;
    }
    private static Product Product(MyPosDbContext db, string name, decimal price, bool exempt = false, string unit = "pc")
    {
        var product = new Product { Name = name, Barcode = name, Category = "Food", Unit = unit,
            Price = price, CostPrice = 40, StockQty = 1000, IsVatExempt = exempt };
        db.Products.Add(product); db.SaveChanges(); return product;
    }
    private static ReportScope Scope(MyPosDbContext db) => new(db.Branches.Single().Id, db.Users.Single().Id,
        DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today));

    [Theory]
    [InlineData(false, false, 7.01)]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 0)]
    public void Posted_line_money_reconciles_for_regular_senior_and_mixed_vat(bool exempt, bool senior, double discount)
    {
        using var db = Db(); var scope = Scope(db);
        var one = Product(db, "A", 112, exempt); var two = Product(db, "B", 71.23m, true, "kg");
        var sale = new SaleService(db).PostSale(scope.BranchId, scope.UserId,
            new[] { new CartLine(one.Id, 1.25m), new CartLine(two.Id, 0.75m) }, discount: (decimal)discount, tendered: 500,
            discountKind: senior ? DiscountKind.SeniorPwd : DiscountKind.Regular, seniorIdNumber: senior ? "ID1" : null);
        Assert.Equal(sale.GrossAmount, sale.Items.Sum(i => i.LineGross));
        Assert.Equal(sale.DiscountAmount, sale.Items.Sum(i => i.LineDiscount!.Value));
        Assert.Equal(sale.TotalAmount, sale.Items.Sum(i => i.LineTotal!.Value));
        Assert.Equal(sale.NetAmount, sale.Items.Sum(i => i.LineNet!.Value));
        Assert.Equal(sale.VatAmount, sale.Items.Sum(i => i.LineVat!.Value));
        Assert.All(sale.Items, i => Assert.Equal(i.LineTotal, i.LineNet + i.LineVat));
        var data = new ExpandedReportService(db).ProductSales(scope);
        Assert.Contains(sale.TotalAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), data.Summary);
        Assert.Contains("pc", data.Rows.SelectMany(r => r.Cells)); Assert.Contains("kg", data.Rows.SelectMany(r => r.Cells));
        Assert.DoesNotContain("Cost", data.Headers);
    }

    [Fact]
    public void Snapshot_reports_survive_catalog_edits_and_keep_distinct_product_identity()
    {
        using var db = Db(); var scope = Scope(db); var product = Product(db, "Old", 112);
        var other = Product(db, "Other", 25);
        other.Name = "Old"; other.Barcode = null; db.SaveChanges();
        var sale = new SaleService(db).PostSale(scope.BranchId, scope.UserId,
            new[] { new CartLine(product.Id, 1), new CartLine(other.Id, 2) }, tendered: 200);
        product.Name = "New"; product.Barcode = "New"; product.Category = "New category"; product.Unit = "kg";
        product.Price = 999; product.CostPrice = 800; product.IsVatExempt = true; db.SaveChanges();
        var report = new ExpandedReportService(db).ProductSales(scope);
        var details = report.Rows.Where(r => r.Kind == "Detail").ToList();
        Assert.Equal(2, details.Count); Assert.NotEqual(details[0].RecordId, details[1].RecordId);
        Assert.All(details, r => Assert.Equal("Food", r.Cells[0]));
        Assert.DoesNotContain("New category", report.ToCsv()); Assert.DoesNotContain("999.00", report.ToCsv());
        Assert.Equal(162, sale.TotalAmount);
    }

    [Fact]
    public void Legacy_line_values_remain_unknown_and_are_never_rebuilt_from_current_products()
    {
        using var db = Db(); var scope = Scope(db); var product = Product(db, "Legacy", 112);
        var sale = new Sale { BranchId = scope.BranchId, CashierId = scope.UserId, SaleDate = DateTime.Now, SaleNumber = 10,
            GrossAmount = 112, DiscountAmount = 12, TotalAmount = 100, NetAmount = 89.29m, VatAmount = 10.71m,
            Items = new() { new SaleItem { ProductId = product.Id, ProductName = "Legacy", Qty = 1, LineGross = 112 } } };
        db.Sales.Add(sale); db.SaveChanges();
        var report = new ExpandedReportService(db).ProductSales(scope);
        Assert.Contains("1 legacy", report.Summary); Assert.Contains("Invoice total PHP 100.00", report.Summary);
        Assert.Contains("Historical category unavailable", report.Rows[0].Cells);
        Assert.Contains("Unavailable", report.Rows[0].Cells); Assert.Null(sale.Items[0].CategorySnapshot);
        Assert.Null(sale.Items[0].LineTotal);
        Assert.Equal("PRODUCT SALES REPORT", report.Title);
        Assert.Contains("Known product sales (partial): ₱0.00", report.PresentationSummary);
        Assert.Contains("Invoice sales total: ₱100.00", report.PresentationSummary);
        Assert.Contains("1 historical sale-item record", report.Warning);
        Assert.Contains("Product totals are incomplete.", report.Warning);
        Assert.False(report.RepeatSummary);
        Assert.True(report.NoteAfterTable);
        var filtered = new ExpandedReportService(db).ProductSales(scope with { Search = "Legacy" });
        Assert.DoesNotContain("Invoice sales total", filtered.PresentationSummary);
        Assert.Contains("Search: Legacy", filtered.PresentationScope);
    }

    [Fact]
    public void Product_report_presentation_omits_defaults_but_keeps_full_csv_scope_and_selected_filters()
    {
        using var db = Db(); var scope = Scope(db);
        var product = Product(db, "Filtered product", 112);
        new SaleService(db).PostSale(scope.BranchId, scope.UserId, new[] { new CartLine(product.Id, 1) }, tendered: 112);
        var service = new ExpandedReportService(db);
        var report = service.ProductSales(scope);
        Assert.StartsWith("Period: ", report.PresentationScope);
        Assert.DoesNotContain("Filters:", report.PresentationScope);
        Assert.DoesNotContain("Branch:", report.PresentationScope);
        Assert.Contains("All cashiers", report.ToCsv());
        Assert.Contains("Search: None", report.ToCsv());
        Assert.Equal("", report.Warning);
        Assert.Contains("Product sales total: ₱112.00", report.PresentationSummary);
        Assert.DoesNotContain("partial", report.PresentationSummary);
        var filtered = service.ProductSales(scope with { CashierId = scope.UserId, Payment = PaymentMethod.Cash,
            Category = "Food", Search = "Filtered" }, categorySummary: true);
        Assert.Equal("CATEGORY SALES REPORT", filtered.Title);
        Assert.Contains("Cashier: " + db.Users.Single().FullName, filtered.PresentationScope);
        Assert.Contains("Payment: Cash", filtered.PresentationScope);
        Assert.Contains("Category: Food", filtered.PresentationScope);
        Assert.Contains("Search: Filtered", filtered.PresentationScope);
        Assert.DoesNotContain("All cashiers", filtered.PresentationScope);
        Assert.DoesNotContain("Invoice sales total", filtered.PresentationSummary);
        Assert.Contains("112.00", filtered.Rows.Single().Cells);
    }

    [Fact]
    public void Product_report_keeps_branch_identity_when_multiple_branches_exist_and_never_mixes_sales()
    {
        using var db = Db(); var scope = Scope(db);
        var product = Product(db, "Branch product", 112);
        new SaleService(db).PostSale(scope.BranchId, scope.UserId, new[] { new CartLine(product.Id, 1) }, tendered: 112);
        var other = new Branch { Name = "Other Branch" };
        db.Branches.Add(other); db.SaveChanges();
        var service = new ExpandedReportService(db);
        var report = service.ProductSales(scope);
        Assert.Contains("Branch: " + db.Branches.Single(b => b.Id == scope.BranchId).Name, report.PresentationScope);
        var otherReport = service.ProductSales(scope with { BranchId = other.Id });
        Assert.Contains("Branch: Other Branch", otherReport.PresentationScope);
        Assert.Empty(otherReport.Rows);
        Assert.Contains("Product sales total: ₱0.00", otherReport.PresentationSummary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Summary_and_void_print_scope_only_shows_applicable_selected_filters(bool voids)
    {
        using var db = Db(); var scope = Scope(db); var service = new ExpandedReportService(db);
        ReportTableData Build(ReportScope selected) => voids ? service.VoidEvents(selected) : service.SalesSummary(selected, monthly: true);
        var defaults = Build(scope);
        Assert.StartsWith(voids ? "Cancellation period:" : "Period:", defaults.PresentationScope);
        Assert.DoesNotContain("Filters:", defaults.PresentationScope);
        Assert.DoesNotContain("Branch:", defaults.PresentationScope);
        Assert.Contains("All cashiers", defaults.ToCsv());
        Assert.Contains("Search: None", defaults.ToCsv());
        if (!voids)
        {
            Assert.Contains("View: Monthly summary", defaults.PresentationScope);
            Assert.Equal("", defaults.PresentationNote);
            Assert.False(defaults.SummaryBeforeTable);
            Assert.True(defaults.RepeatSummary);
            Assert.Contains("Current-status sales", defaults.ToCsv());
        }
        var selectedScope = scope with { CashierId = scope.UserId, Payment = PaymentMethod.Cash,
            Search = "  receipt  ", Category = "Unused category" };
        var selected = Build(selectedScope);
        Assert.Contains("Cashier: " + db.Users.Single().FullName, selected.PresentationScope);
        Assert.Contains("Search: receipt", selected.PresentationScope);
        Assert.DoesNotContain("Category:", selected.PresentationScope);
        if (voids) Assert.DoesNotContain("Payment:", selected.PresentationScope);
        else Assert.Contains("Payment: Cash", selected.PresentationScope);
        var reset = Build(selectedScope with { CashierId = null, Payment = null, Search = "   ", Category = "" });
        Assert.Equal(defaults.PresentationScope, reset.PresentationScope);
        var other = new Branch { Name = "Second branch" };
        db.Branches.Add(other); db.SaveChanges();
        Assert.Contains("Branch: Second branch", Build(scope with { BranchId = other.Id }).PresentationScope);
    }

    [Fact]
    public void Summary_filters_and_void_events_use_their_declared_date_and_status_policy()
    {
        using var db = Db(); var scope = Scope(db); var product = Product(db, "Sale", 112);
        var service = new SaleService(db);
        var sale = service.PostSale(scope.BranchId, scope.UserId, new[] { new CartLine(product.Id, 1) }, tendered: 112,
            method: PaymentMethod.Gcash, receiptType: ReceiptType.Manual, manualReceiptNumber: "A001", customerName: "Buyer");
        var reports = new ExpandedReportService(db);
        Assert.Contains("112.00", reports.SalesSummary(scope with { Search = "buyer", Payment = PaymentMethod.Gcash }).Summary);
        Assert.Contains("0.00", reports.SalesSummary(scope with { Payment = PaymentMethod.Cash }).Summary);
        sale.SaleDate = DateTime.Today.AddDays(-1); db.SaveChanges();
        service.VoidSale(sale.Id, scope.UserId, "Approved");
        Assert.Single(reports.VoidEvents(scope).Rows);
        Assert.Empty(reports.ProductSales(scope).Rows);
        Assert.Contains("Cancellation-date", reports.VoidEvents(scope).Scope);
        var cashier = new User { Username = "cashier", Role = UserRole.Cashier, FullName = "Cashier" };
        db.Users.Add(cashier); db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => reports.VoidEvents(scope with { UserId = cashier.Id }));
        cashier.IsActive = false; db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => reports.SalesSummary(scope with { UserId = cashier.Id }));
        Assert.Throws<InvalidOperationException>(() => reports.ProductSales(scope with { To = scope.To.AddDays(1) }));
    }

    [Fact]
    public void Denomination_totals_are_validated_and_closing_identity_is_persisted()
    {
        using var db = Db(); var scope = Scope(db); var shifts = new ShiftService(db);
        var shift = shifts.OpenShift(scope.BranchId, scope.UserId, 1010.50m);
        var counts = new Dictionary<decimal, int> { [1000] = 1, [10] = 1, [0.25m] = 2 };
        Assert.Throws<InvalidOperationException>(() => shifts.CloseShift(scope.UserId, 999, null, counts));
        Assert.Null(shift.ClosedAt);
        Assert.Throws<InvalidOperationException>(() => shifts.CloseShift(scope.UserId, 0, null, new Dictionary<decimal, int> { [2] = 1 }));
        shifts.CloseShift(scope.UserId, 1010.50m, "Counted", counts);
        var report = new ShiftReportService(db).GetReport(shift.Id, scope.UserId);
        Assert.Equal(scope.UserId, report.Shift.ClosedById); Assert.Equal(3, report.Shift.DenominationCounts.Count);
        Assert.Equal(report.Shift.CountedDrawer, report.Shift.DenominationCounts.Sum(c => c.FaceValue * c.Count));
        Assert.Equal(0, report.Shift.Variance);
    }

    [Fact]
    public void Structured_movements_validate_references_and_approval_without_double_deducting_deposits()
    {
        using var db = Db(); var scope = Scope(db); var shifts = new ShiftService(db);
        var shift = shifts.OpenShift(scope.BranchId, scope.UserId, 1000);
        Assert.Throws<InvalidOperationException>(() => shifts.RecordCashMovement(scope.UserId, CashMovementType.CashOut, 100, "Deposit", CashMovementCategory.BankDeposit));
        Assert.Throws<InvalidOperationException>(() => shifts.RecordCashMovement(scope.UserId, CashMovementType.CashIn, 100, "Expense", CashMovementCategory.Expense, expenseCategory: "Supplies"));
        Assert.Throws<InvalidOperationException>(() => shifts.RecordCashMovement(scope.UserId, CashMovementType.CashOut, 10, "Expense", CashMovementCategory.Expense));
        var deposit = shifts.RecordCashMovement(scope.UserId, CashMovementType.CashOut, 100, "Drawer deposit",
            CashMovementCategory.BankDeposit, "SLIP1", "Bank");
        var expense = shifts.RecordCashMovement(scope.UserId, CashMovementType.CashOut, 30, "Supplies",
            CashMovementCategory.Expense, expenseCategory: "Office");
        var cashier = new User { Username = "cashier", Role = UserRole.Cashier };
        db.Users.Add(cashier); db.SaveChanges();
        Assert.Throws<InvalidOperationException>(() => shifts.ApproveCashMovement(deposit.Id, cashier.Id));
        shifts.ApproveCashMovement(deposit.Id, scope.UserId);
        Assert.Equal(scope.UserId, deposit.ApprovedById); Assert.NotNull(deposit.ApprovedAt);
        Assert.Throws<InvalidOperationException>(() => shifts.ApproveCashMovement(deposit.Id, scope.UserId));
        var movements = new CashMovementReportService(db).GetReport(scope.BranchId, scope.UserId, scope.From, scope.To);
        Assert.Contains("BankDeposit: PHP 100.00", movements.CategorySummary);
        Assert.Contains("Expense: PHP 30.00", movements.CategorySummary);
        Assert.Equal(870, new ShiftReportService(db).GetReport(shift.Id, scope.UserId).ExpectedDrawer);
    }

    [Fact]
    public void Rounding_residuals_never_make_line_totals_negative_and_all_discounts_still_reconcile()
    {
        var lines = Enumerable.Range(0, 6).Select(_ => new SaleCalculator.Line(0.01m, 1, 0, false)).ToArray();
        var calc = SaleCalculator.Compute(lines, 0.04m, 0.12m);
        Assert.All(calc.LineAmounts, line => Assert.InRange(line.Total, 0, line.Gross));
        Assert.Equal(calc.Total, calc.LineAmounts.Sum(l => l.Total)); Assert.Equal(calc.Discount, calc.LineAmounts.Sum(l => l.Discount));
        var zero = SaleCalculator.Compute(new[] { new SaleCalculator.Line(0, 1, 0, true) }, 0, 0.12m);
        Assert.Single(zero.LineAmounts); Assert.Equal(0, zero.Total);
    }
}
