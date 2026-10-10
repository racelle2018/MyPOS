using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;

namespace MyPos.Core.Services;

public static class ShiftAccountCodes
{
    public const string CashOnHand = "1000";
    public const string CashOverShort = "5100";
    public const string OwnerDraws = "3000";
}

public sealed class ShiftService
{
    private readonly MyPosDbContext _db;
    public ShiftService(MyPosDbContext db) => _db = db;

    public CashShift? GetOpenShift() => _db.CashShifts.FirstOrDefault(s => s.ClosedAt == null);
    public bool HasOpenShift => GetOpenShift() != null;

    public CashShift OpenShift(Guid branchId, Guid userId, decimal openingFloat)
    {
        if (openingFloat < 0) throw new InvalidOperationException("Opening float cannot be negative.");
        if (HasOpenShift) throw new InvalidOperationException("A shift is already open. Close it before opening a new one.");
        var shift = new CashShift { BranchId = branchId, UserId = userId, OpenedAt = DateTime.Now, OpeningFloat = openingFloat };
        _db.CashShifts.Add(shift);
        _db.SaveChanges();
        return shift;
    }

    public CashMovement RecordCashMovement(Guid userId, CashMovementType type, decimal amount, string reason)
    {
        if (amount <= 0) throw new InvalidOperationException("Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A reason is required.");
        var shift = GetOpenShift() ?? throw new InvalidOperationException("No open shift.");
        var now = DateTime.Now;
        var movement = new CashMovement { ShiftId = shift.Id, Type = type, Amount = amount, Reason = reason.Trim(), UserId = userId, MovementDate = now };
        _db.CashMovements.Add(movement);
        if (type == CashMovementType.CashOut)
            Post(now, shift.BranchId, "CashOut", movement.Id, $"CASH OUT: {movement.Reason}", (ShiftAccountCodes.OwnerDraws, amount, 0), (ShiftAccountCodes.CashOnHand, 0, amount));
        else
            Post(now, shift.BranchId, "CashIn", movement.Id, $"CASH IN: {movement.Reason}", (ShiftAccountCodes.CashOnHand, amount, 0), (ShiftAccountCodes.OwnerDraws, 0, amount));
        _db.SaveChanges();
        return movement;
    }

    public sealed record ShiftTotals(decimal CashSales, decimal VoidedCashSales, decimal NonCashSales, decimal CashIn, decimal CashOut, int TransactionCount);

    public ShiftTotals GetShiftTotals(Guid shiftId)
    {
        var shift = _db.CashShifts.Include(s => s.Movements).First(s => s.Id == shiftId);
        var end = shift.ClosedAt ?? DateTime.Now;
        var sales = _db.Sales.Include(s => s.Payments)
            .Where(s => s.BranchId == shift.BranchId && s.SaleDate >= shift.OpenedAt && s.SaleDate <= end)
            .ToList();
        decimal cashSales = 0, nonCash = 0;
        var count = 0;
        foreach (var sale in sales)
        {
            var isCash = sale.Payments.Any(p => p.Method == PaymentMethod.Cash);
            var voidedDuringShift = sale.IsVoided && sale.VoidedAt <= end;
            if (voidedDuringShift) continue;
            count++;
            if (isCash) cashSales += sale.Payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
            else nonCash += sale.TotalAmount;
        }
        var voidedCashSales = _db.Sales.Include(s => s.Payments)
            .Where(s => s.BranchId == shift.BranchId && s.VoidedAt >= shift.OpenedAt && s.VoidedAt <= end)
            .ToList()
            .Where(s => s.Payments.Any(p => p.Method == PaymentMethod.Cash))
            .ToList();
        var voidedCash = voidedCashSales.Sum(s => s.TotalAmount);

        // Same-shift voids were already excluded above. A refund for a sale from
        // an earlier shift reduces the cash actually present in this drawer.
        cashSales -= voidedCashSales
            .Where(s => s.SaleDate < shift.OpenedAt)
            .Sum(s => s.TotalAmount);
        var cashIn = shift.Movements.Where(m => m.Type == CashMovementType.CashIn).Sum(m => m.Amount);
        var cashOut = shift.Movements.Where(m => m.Type == CashMovementType.CashOut).Sum(m => m.Amount);
        return new ShiftTotals(cashSales, voidedCash, nonCash, cashIn, cashOut, count);
    }

    public CashShift CloseShift(Guid userId, decimal countedDrawer, string? notes)
    {
        if (countedDrawer < 0) throw new InvalidOperationException("Counted drawer cannot be negative.");
        var shift = GetOpenShift() ?? throw new InvalidOperationException("No open shift.");
        var totals = GetShiftTotals(shift.Id);
        var expected = shift.OpeningFloat + totals.CashSales + totals.CashIn - totals.CashOut;
        var variance = Math.Round(countedDrawer - expected, 2, MidpointRounding.AwayFromZero);
        var now = DateTime.Now;
        shift.ClosedAt = now; shift.CountedDrawer = countedDrawer; shift.ExpectedDrawer = expected; shift.Variance = variance; shift.Notes = notes?.Trim();
        using var transaction = _db.Database.BeginTransaction();
        if (variance > 0) Post(now, shift.BranchId, "ShiftClose", shift.Id, $"Z-READING: VARIANCE +{variance:N2}", (ShiftAccountCodes.CashOnHand, variance, 0), (ShiftAccountCodes.CashOverShort, 0, variance));
        else if (variance < 0) Post(now, shift.BranchId, "ShiftClose", shift.Id, $"Z-READING: VARIANCE {variance:N2}", (ShiftAccountCodes.CashOverShort, -variance, 0), (ShiftAccountCodes.CashOnHand, 0, -variance));
        _db.AuditLogs.Add(new AuditLog { Date = now, UserId = userId, Action = "ShiftClose", EntityName = "CashShift", EntityId = shift.Id, Details = $"EXPECTED {expected:N2} COUNTED {countedDrawer:N2} VARIANCE {variance:+0.00;-0.00}" });
        _db.SaveChanges();
        transaction.Commit();
        return shift;
    }

    private void Post(DateTime date, Guid branchId, string sourceType, Guid sourceId, string description, params (string code, decimal debit, decimal credit)[] lines)
    {
        var entry = new JournalEntry { EntryDate = date, BranchId = branchId, SourceType = sourceType, SourceId = sourceId, Description = description };
        foreach (var (code, debit, credit) in lines)
            if (debit != 0 || credit != 0) entry.Lines.Add(new JournalLine { AccountId = _db.Accounts.First(a => a.Code == code).Id, Debit = debit, Credit = credit });
        _db.JournalEntries.Add(entry);
    }
}
