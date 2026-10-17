using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
namespace RetailForma.Core.Services;

public sealed record ReportScope(Guid BranchId, Guid UserId, DateOnly From, DateOnly To,
    Guid? CashierId = null, PaymentMethod? Payment = null, string Search = "", string Category = "");
public sealed record ReportTableRow(string[] Cells, string RecordId = "", string Kind = "Detail");
public sealed record ReportTableData(string Title, string Scope, string Note, string Summary,
    string[] Headers, int[] NumericColumns, List<ReportTableRow> Rows)
{
    public string? PresentationScope { get; init; }
    public string? PresentationNote { get; init; }
    public string? PresentationSummary { get; init; }
    public string Warning { get; init; } = "";
    public bool NoteAfterTable { get; init; }
    public bool SummaryBeforeTable { get; init; } = true;
    public bool RepeatSummary { get; init; } = true;
    public string ToCsv()
    {
        var csv = new StringBuilder("Report,Scope,Note,Kind,RecordId," + string.Join(",", Headers.Select(ReportCsv.Text)) + "\r\n");
        foreach (var row in Rows)
            csv.AppendLine(string.Join(",", new[] { ReportCsv.Text(Title), ReportCsv.Text(Scope), ReportCsv.Text(Note),
                ReportCsv.Text(row.Kind), ReportCsv.Text(row.RecordId) }.Concat(row.Cells.Select((v, i) =>
                NumericColumns.Contains(i) && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                    ? v : ReportCsv.Text(v)))));
        csv.AppendLine(string.Join(",", new[] { ReportCsv.Text(Title), ReportCsv.Text(Scope), ReportCsv.Text(Note),
            ReportCsv.Text("Summary"), ReportCsv.Text("") }.Concat(Headers.Select((_, i) => ReportCsv.Text(i == 0 ? Summary : "")))));
        return csv.ToString();
    }
}

public sealed class ExpandedReportService(RetailFormaDbContext db)
{
    public void Validate(ReportScope scope, bool adminOnly = false)
    {
        var user = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == scope.UserId && u.IsActive);
        if (user == null || adminOnly && !UserAccess.IsAdministrator(user))
            throw new InvalidOperationException(adminOnly ? "An active administrator is required." : "An active user is required.");
        if (scope.From > scope.To || scope.To > DateOnly.FromDateTime(RetailForma.Core.BusinessClock.Today))
            throw new InvalidOperationException("Choose a valid date range ending today or earlier.");
        if (!db.Branches.Any(b => b.Id == scope.BranchId)) throw new InvalidOperationException("Branch not found.");
        if (scope.Payment.HasValue && !Enum.IsDefined(scope.Payment.Value)) throw new InvalidOperationException("Invalid payment filter.");
        if (scope.CashierId.HasValue && !db.Users.Any(u => u.Id == scope.CashierId)) throw new InvalidOperationException("Cashier not found.");
    }
    public List<Sale> Sales(ReportScope scope)
    {
        Validate(scope);
        var start = scope.From.ToDateTime(TimeOnly.MinValue); var end = scope.To.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var query = db.Sales.AsNoTracking().Include(s => s.Items).Include(s => s.Payments)
            .Where(s => s.BranchId == scope.BranchId && s.SaleDate >= start && s.SaleDate < end);
        if (scope.CashierId.HasValue) query = query.Where(s => s.CashierId == scope.CashierId);
        if (scope.Payment.HasValue) query = query.Where(s => s.Payments.Any(p => p.Method == scope.Payment));
        return query.OrderBy(s => s.SaleDate).ThenBy(s => s.SaleNumber).ToList();
    }
    public string Describe(ReportScope scope)
    {
        var branch = db.Branches.AsNoTracking().First(b => b.Id == scope.BranchId).Name;
        var cashier = scope.CashierId.HasValue ? db.Users.AsNoTracking().First(u => u.Id == scope.CashierId).FullName : "All cashiers";
        return $"{branch} | {scope.From:yyyy-MM-dd} to {scope.To:yyyy-MM-dd} | {cashier} | " +
            $"{scope.Payment?.ToString() ?? "All payments"} | Category: {(scope.Category.Length == 0 ? "All" : scope.Category == "__UNCATEGORIZED" ? "Uncategorized" : scope.Category)} | Search: {(scope.Search.Length == 0 ? "None" : scope.Search)}";
    }
    private static string N(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    private string DescribePresentation(ReportScope scope, bool payment = true, bool category = false,
        string periodLabel = "Period") => ReportPresentationScope.Format(scope.From, scope.To,
        new (string, string?)[]
        {
            ("Branch", db.Branches.Count() > 1 ? db.Branches.AsNoTracking().First(b => b.Id == scope.BranchId).Name : null),
            ("Cashier", scope.CashierId.HasValue ? db.Users.AsNoTracking().First(u => u.Id == scope.CashierId).FullName : null),
            ("Payment", payment ? scope.Payment?.ToString() : null),
            ("Category", category ? (scope.Category == "__UNCATEGORIZED" ? "Uncategorized" : scope.Category) : null),
            ("Search", scope.Search)
        }, periodLabel);

    public ReportTableData SalesSummary(ReportScope scope, bool monthly = false)
    {
        var sales = Sales(scope);
        var term = scope.Search.Trim();
        if (term.Length > 0) sales = sales.Where(s => (s.ReceiptNumber ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (s.CustomerName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        var active = sales.Where(s => !s.IsVoided).ToList();
        var rows = new List<ReportTableRow>();
        foreach (var group in sales.GroupBy(s => monthly ? s.SaleDate.ToString("yyyy-MM", CultureInfo.InvariantCulture) :
            s.SaleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).OrderBy(g => g.Key))
        {
            var valid = group.Where(s => !s.IsVoided).ToList();
            rows.Add(new(new[] { group.Key, I(valid.Count), N(valid.Sum(s => s.GrossAmount)), N(valid.Sum(s => s.DiscountAmount)),
                N(valid.Sum(s => s.TotalAmount)), N(valid.Sum(s => s.VatAmount)), N(valid.Sum(s => s.NetAmount)),
                N(valid.Count == 0 ? 0 : Math.Round(valid.Sum(s => s.TotalAmount) / valid.Count, 2)), I(group.Count(s => s.IsVoided)) }));
        }
        foreach (var method in Enum.GetValues<PaymentMethod>())
            rows.Add(new(new[] { method.ToString(), "", "", "", N(active.SelectMany(s => s.Payments).Where(p => p.Method == method).Sum(p => p.Amount)), "", "", "", "" }, Kind: "Payment"));
        return new("SALES SUMMARY", Describe(scope) + (monthly ? " | Monthly" : " | Daily"),
            "Current-status sales; approved voids excluded from sales totals. Net and average sale are not profit. Payment filter selects whole invoices.",
            $"{active.Count} transactions | Total PHP {N(active.Sum(s => s.TotalAmount))} | VAT PHP {N(active.Sum(s => s.VatAmount))} | Net PHP {N(active.Sum(s => s.NetAmount))} | Voids {sales.Count(s => s.IsVoided)}",
            new[] { monthly ? "Month / payment" : "Date / payment", "Transactions", "Gross", "Discount", "Total", "VAT", "Net", "Avg. sale", "Voids" },
            new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, rows)
        {
            PresentationScope = DescribePresentation(scope) + (monthly ? "\nView: Monthly summary" : "\nView: Daily summary"),
            PresentationNote = "",
            SummaryBeforeTable = false
        };
    }

    public ReportTableData ProductSales(ReportScope scope, bool categorySummary = false)
    {
        var sales = Sales(scope with { Search = "", Category = "" }).Where(s => !s.IsVoided).ToList();
        const string unknown = "Historical category unavailable";
        var items = sales.SelectMany(s => s.Items).Where(i =>
            (scope.Category.Length == 0 || (i.CategorySnapshot ?? unknown) == (scope.Category == "__UNCATEGORIZED" ? "" : scope.Category)) &&
            (scope.Search.Length == 0 || i.ProductName.Contains(scope.Search, StringComparison.OrdinalIgnoreCase) ||
             (i.Barcode ?? "").Contains(scope.Search, StringComparison.OrdinalIgnoreCase))).ToList();
        bool Complete(IEnumerable<SaleItem> group) => group.All(i => i.LineTotal.HasValue && i.LineDiscount.HasValue);
        string Total(IEnumerable<SaleItem> group) => Complete(group) ? N(group.Sum(i => i.LineTotal!.Value)) : "Unavailable";
        string Discount(IEnumerable<SaleItem> group) => Complete(group) ? N(group.Sum(i => i.LineDiscount!.Value)) : "Unavailable";
        var rows = new List<ReportTableRow>();
        foreach (var category in items.GroupBy(i => i.CategorySnapshot ?? unknown).OrderBy(g => g.Key))
        {
            if (!categorySummary)
            {
                foreach (var group in category.GroupBy(i => new { i.ProductId, i.ProductName, i.Barcode, i.UnitSnapshot, i.TaxTreatmentSnapshot })
                    .OrderBy(g => g.Key.ProductName))
                    rows.Add(new(new[] { category.Key.Length == 0 ? "Uncategorized" : category.Key, group.Key.Barcode ?? group.Key.ProductId.ToString("N")[..8],
                        group.Key.ProductName, group.Key.UnitSnapshot ?? "Unknown", N(group.Sum(i => i.Qty)),
                        N(group.Sum(i => i.LineGross)), Discount(group), Total(group) }, group.Key.ProductId.ToString()));
                rows.Add(new(new[] { category.Key.Length == 0 ? "Uncategorized" : category.Key, "", "Category subtotal", "", "",
                    N(category.Sum(i => i.LineGross)), Discount(category), Total(category) }, Kind: "Subtotal"));
            }
            else
                rows.Add(new(new[] { category.Key.Length == 0 ? "Uncategorized" : category.Key,
                    string.Join("; ", category.GroupBy(i => i.UnitSnapshot ?? "Unknown").Select(g => $"{g.Key}: {N(g.Sum(i => i.Qty))}")),
                    N(category.Sum(i => i.LineGross)), Discount(category), Total(category) }, Kind: "Category"));
        }
        var known = items.Where(i => i.LineTotal.HasValue).Sum(i => i.LineTotal!.Value);
        var missing = items.Count(i => !i.LineTotal.HasValue);
        var authoritative = scope.Category.Length == 0 && scope.Search.Length == 0
            ? $" | Invoice total PHP {N(sales.Sum(s => s.TotalAmount))}" : "";
        var summary = $"{(missing > 0 ? "Known product sales (partial)" : "Product sales total")}: ₱{known.ToString("N2", CultureInfo.InvariantCulture)}";
        if (authoritative.Length > 0) summary += $"\nInvoice sales total: ₱{sales.Sum(s => s.TotalAmount).ToString("N2", CultureInfo.InvariantCulture)}";
        return new(categorySummary ? "CATEGORY SALES REPORT" : "PRODUCT SALES REPORT", Describe(scope) + (categorySummary ? " | Category summary" : " | Product detail"),
            "Voided sales are excluded. Quantities are grouped by unit. Product sales amounts do not represent profit.",
            $"Known line total PHP {N(known)} | {missing} legacy line(s) without final amounts{authoritative}",
            categorySummary ? new[] { "Category", "Quantities by unit", "Gross", "Discount", "Sales total" } :
                new[] { "Category", "Barcode / code", "Product", "Unit", "Qty", "Gross", "Discount", "Sales total" },
            categorySummary ? new[] { 2, 3, 4 } : new[] { 4, 5, 6, 7 }, rows)
        {
            PresentationScope = DescribePresentation(scope, category: true),
            PresentationSummary = summary,
            Warning = missing > 0 ? $"Final amounts are unavailable for {missing} historical sale-item record{(missing == 1 ? "" : "s")}. Product totals are incomplete." : "",
            NoteAfterTable = true,
            RepeatSummary = false
        };
    }

    public ReportTableData VoidEvents(ReportScope scope)
    {
        Validate(scope, adminOnly: true);
        var start = scope.From.ToDateTime(TimeOnly.MinValue); var end = scope.To.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var sales = db.Sales.AsNoTracking().Where(s => s.BranchId == scope.BranchId && s.IsVoided &&
            s.VoidedAt >= start && s.VoidedAt < end).OrderBy(s => s.VoidedAt).ToList();
        if (scope.CashierId.HasValue) sales = sales.Where(s => s.CashierId == scope.CashierId).ToList();
        if (scope.Search.Length > 0) sales = sales.Where(s => (s.ReceiptNumber ?? "").Contains(scope.Search, StringComparison.OrdinalIgnoreCase) ||
            (s.CustomerName ?? "").Contains(scope.Search, StringComparison.OrdinalIgnoreCase) ||
            (s.VoidReason ?? "").Contains(scope.Search, StringComparison.OrdinalIgnoreCase)).ToList();
        var users = db.Users.AsNoTracking().ToDictionary(u => u.Id, u => u.FullName);
        return new("VOID EVENTS", Describe(scope) + " | Cancellation-date basis",
            "Lists approvals in the cancellation date range, including sales from earlier days. Amount is original sale total, not profit or a new sale.",
            $"{sales.Count} void(s) | Original sale amounts PHP {N(sales.Sum(s => s.TotalAmount))}",
            new[] { "Voided at", "Invoice", "Original sale date", "Customer", "Original cashier", "Amount", "Approved by", "Reason" }, new[] { 5 },
            sales.Select(s => new ReportTableRow(new[] { s.VoidedAt!.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                s.ReceiptNumber ?? "Missing", s.SaleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), s.CustomerName ?? "WALK-IN",
                users.GetValueOrDefault(s.CashierId ?? Guid.Empty, "Unknown"), N(s.TotalAmount),
                users.GetValueOrDefault(s.VoidedById ?? Guid.Empty, "Unknown"), s.VoidReason ?? "" }, s.Id.ToString())).ToList())
        {
            PresentationScope = DescribePresentation(scope, payment: false, periodLabel: "Cancellation period"),
            SummaryBeforeTable = false,
            NoteAfterTable = true,
            PresentationNote = "Voids are grouped by approval date, including earlier sales. Amounts are original sale totals, not new sales or profit.",
            PresentationSummary = $"Voided invoices: {sales.Count} | Original sale amounts: ₱{sales.Sum(s => s.TotalAmount).ToString("N2", CultureInfo.InvariantCulture)}"
        };
    }
}
