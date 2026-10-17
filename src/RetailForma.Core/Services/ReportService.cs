using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;

namespace RetailForma.Core.Services;

public sealed class DailySalesReport
{
    public DateOnly Date { get; init; }
    public string? ScopeDescription { get; init; }
    public List<Sale> Sales { get; init; } = new();
    public IEnumerable<Sale> ActiveSales => Sales.Where(s => !s.IsVoided);
    public int TransactionCount => ActiveSales.Count();
    public decimal Gross => ActiveSales.Sum(s => s.GrossAmount);
    public decimal Discounts => ActiveSales.Sum(s => s.DiscountAmount);
    public decimal NetSales => ActiveSales.Sum(s => s.NetAmount);
    public decimal Vat => ActiveSales.Sum(s => s.VatAmount);
    public decimal TotalSales => ActiveSales.Sum(s => s.TotalAmount);
    public decimal AverageSale => TransactionCount == 0 ? 0 : Math.Round(TotalSales / TransactionCount, 2);
    public int VoidCount => Sales.Count(s => s.IsVoided);
    public decimal VoidTotal => Sales.Where(s => s.IsVoided).Sum(s => s.TotalAmount);
    public int SystemReceipts => ActiveSales.Count(s => s.ReceiptType == ReceiptType.System);
    public int ManualReceipts => ActiveSales.Count(s => s.ReceiptType == ReceiptType.Manual);
    public int MissingReceipts => ActiveSales.Count(s => s.ReceiptType == ReceiptType.None);
}

public sealed class ReportService
{
    private readonly RetailFormaDbContext _db;

    public ReportService(RetailFormaDbContext db) => _db = db;

    public DailySalesReport GetDailySales(DateTime date, Guid branchId, Guid? cashierId = null, PaymentMethod? payment = null, string search = "")
    {
        var start = date.Date;
        var end = start.AddDays(1);
        var sales = _db.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .Where(s => s.BranchId == branchId && s.SaleDate >= start && s.SaleDate < end)
            .OrderBy(s => s.SaleNumber)
            .ToList();
        if (cashierId.HasValue) sales = sales.Where(s => s.CashierId == cashierId).ToList();
        if (payment.HasValue) sales = sales.Where(s => s.Payments.Any(p => p.Method == payment)).ToList();
        if (!string.IsNullOrWhiteSpace(search)) sales = sales.Where(s => (s.ReceiptNumber ?? "").Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
            (s.CustomerName ?? "").Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        return new DailySalesReport
        {
            Date = DateOnly.FromDateTime(start),
            Sales = sales
        };
    }
}
