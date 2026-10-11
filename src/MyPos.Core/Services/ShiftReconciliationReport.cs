using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;

namespace MyPos.Core.Services;

public sealed record ShiftReconciliationReport(CashShift Shift, List<Sale> Sales,
    List<CashMovement> Movements, ShiftService.ShiftTotals DrawerTotals)
{
    public IEnumerable<Sale> ActiveSales => Sales.Where(s => !s.IsVoided);
    public decimal CurrentSales => ActiveSales.Sum(s => s.TotalAmount);
    public decimal CurrentNet => ActiveSales.Sum(s => s.NetAmount);
    public decimal CurrentVat => ActiveSales.Sum(s => s.VatAmount);
    public int TransactionCount => ActiveSales.Count();
    public int VoidCount => Sales.Count(s => s.IsVoided);
    public decimal VoidTotal => Sales.Where(s => s.IsVoided).Sum(s => s.TotalAmount);
    public int LaterVoidCount => Sales.Count(s => s.IsVoided && Shift.ClosedAt.HasValue && s.VoidedAt > Shift.ClosedAt);
    public int LegacySalesCount => Sales.Count(s => s.CashShiftId == null);
    public decimal PaymentTotal(PaymentMethod method) => ActiveSales.SelectMany(s => s.Payments)
        .Where(p => p.Method == method).Sum(p => p.Amount);
    public decimal ExpectedDrawer => Shift.ClosedAt.HasValue ? Shift.ExpectedDrawer :
        Shift.OpeningFloat + DrawerTotals.CashSales + DrawerTotals.CashIn - DrawerTotals.CashOut;
}

public sealed class ShiftReportService(MyPosDbContext db)
{
    public ShiftReconciliationReport GetReport(Guid shiftId, Guid userId)
    {
        var shift = db.CashShifts.AsNoTracking().Include(s => s.Movements).Include(s => s.DenominationCounts).First(s => s.Id == shiftId);
        var shifts = new ShiftService(db);
        shifts.RequireDrawerAccess(shift, userId);
        var end = shift.ClosedAt ?? DateTime.Now;
        var sales = db.Sales.AsNoTracking().Include(s => s.Payments).Where(s => s.CashShiftId == shiftId ||
            (s.CashShiftId == null && s.BranchId == shift.BranchId &&
                s.SaleDate >= shift.OpenedAt && s.SaleDate <= end))
            .OrderBy(s => s.SaleDate).ToList();
        return new(shift, sales, shift.Movements.OrderBy(m => m.MovementDate).ToList(), shifts.GetShiftTotals(shiftId));
    }
}
