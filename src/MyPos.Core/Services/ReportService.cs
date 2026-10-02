using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;

namespace MyPos.Core.Services;

public sealed class DailySalesReport
{
    public DateOnly Date { get; init; }
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
    private readonly MyPosDbContext _db;

    public ReportService(MyPosDbContext db) => _db = db;

    public DailySalesReport GetDailySales(DateTime date, Guid branchId)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        var sales = _db.Sales
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .Where(s => s.BranchId == branchId && s.SaleDate >= start && s.SaleDate < end)
            .OrderBy(s => s.SaleNumber)
            .ToList();

        return new DailySalesReport
        {
            Date = DateOnly.FromDateTime(start),
            Sales = sales
        };
    }
}
