using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;

namespace RetailForma.Core.Services;

public static class ShiftAccountCodes
{
    public const string CashOnHand = "1000";
    public const string CashOverShort = "5100";
    public const string OwnerDraws = "3000";
}

public sealed class ShiftService
{
    private readonly RetailFormaDbContext _db;
    public ShiftService(RetailFormaDbContext db) => _db = db;

    public CashShift? GetOpenShift() => _db.CashShifts.FirstOrDefault(s => s.ClosedAt == null);
    public bool HasOpenShift => GetOpenShift() != null;

    public bool CanUseDrawer(CashShift shift, Guid userId)
    {
        var user = _db.Users.AsNoTracking().FirstOrDefault(u => u.Id == userId && u.IsActive);
        return user != null && (UserAccess.IsAdministrator(user) || shift.DrawerMode == DrawerMode.Shared ||
            shift.AssignedCashierId == userId);
    }

    public void RequireDrawerAccess(CashShift shift, Guid userId)
    {
        if (!CanUseDrawer(shift, userId))
            throw new InvalidOperationException("This drawer is assigned to another cashier. Ask an administrator for access.");
    }

    public void ConfigureDrawer(Guid adminId, DrawerMode mode, Guid? assignedCashierId = null)
    {
        if (!_db.Users.AsNoTracking().Any(u => u.Id == adminId && u.IsActive && UserAccess.AdministratorRoles.Contains(u.Role)))
            throw new InvalidOperationException("Only active administrators can configure drawer assignment.");
        if (!Enum.IsDefined(mode)) throw new InvalidOperationException("Invalid drawer mode.");
        if (mode == DrawerMode.Assigned && (!assignedCashierId.HasValue ||
            !_db.Users.AsNoTracking().Any(u => u.Id == assignedCashierId && u.IsActive && u.Role == UserRole.Cashier)))
            throw new InvalidOperationException("Select an active cashier for the assigned drawer.");
        SetDrawerSetting("DrawerMode", mode.ToString());
        SetDrawerSetting("DrawerAssignedCashierId", mode == DrawerMode.Assigned ? assignedCashierId!.Value.ToString() : "");
        _db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, UserId = adminId,
            Action = "DrawerAssignment", EntityName = "Setting",
            Details = $"Next shift: {mode}; assigned cashier {(mode == DrawerMode.Assigned ? assignedCashierId : null)}" });
        _db.SaveChanges();
    }

    public CashShift OpenShift(Guid branchId, Guid userId, decimal openingFloat, string? floatCounterpartCode = null)
    {
        if (openingFloat < 0) throw new InvalidOperationException("Opening float cannot be negative.");
        _db.RequireBusinessAuthorization();
        using var rollback = new PostingRollbackScope(_db);
        if (Math.Round(openingFloat, 2) != openingFloat) throw new InvalidOperationException("Opening float must have at most two decimal places.");
        if (openingFloat > 0 && floatCounterpartCode != AccountingPolicy.Reserve)
            throw new InvalidOperationException("Confirm that opening float is transferred from the company cash reserve.");
        using var transaction = _db.Database.BeginTransaction();
        if (HasOpenShift) throw new InvalidOperationException("A shift is already open. Close it before opening a new one.");
        var user = _db.Users.AsNoTracking().FirstOrDefault(u => u.Id == userId && u.IsActive)
            ?? throw new InvalidOperationException("An active user is required.");
        var configuredMode = _db.Settings.FirstOrDefault(s => s.Key == "DrawerMode")?.Value;
        var effectiveMode = configuredMode == "Assigned" ? DrawerMode.Assigned : DrawerMode.Shared;
        var configuredCashier = _db.Settings.FirstOrDefault(s => s.Key == "DrawerAssignedCashierId")?.Value;
        var effectiveCashier = Guid.TryParse(configuredCashier, out var configuredId) ? configuredId : (Guid?)null;
        if (!Enum.IsDefined(effectiveMode)) throw new InvalidOperationException("Invalid drawer mode.");
        if (effectiveMode == DrawerMode.Assigned && (!effectiveCashier.HasValue ||
            !_db.Users.Any(u => u.Id == effectiveCashier && u.IsActive && u.Role == UserRole.Cashier)))
            throw new InvalidOperationException("Select an active cashier for the assigned drawer.");
        var shift = new CashShift { BranchId = branchId, UserId = userId, OpenedAt = RetailForma.Core.BusinessClock.Now,
            OpeningFloat = openingFloat, DrawerMode = effectiveMode,
            AssignedCashierId = effectiveMode == DrawerMode.Assigned ? effectiveCashier : null };
        RequireDrawerAccess(shift, userId);
        _db.CashShifts.Add(shift);
        if (openingFloat > 0) Post(shift.OpenedAt, branchId, "ShiftOpeningFloat", shift.Id, "Opening float from company reserve",
            ("1000", openingFloat, 0), (AccountingPolicy.Reserve, 0, openingFloat));
        _db.SaveChanges();
        transaction.Commit();
        rollback.Complete();
        return shift;
    }

    private void SetDrawerSetting(string key, string value)
    {
        var setting = _db.Settings.FirstOrDefault(s => s.Key == key);
        if (setting == null) _db.Settings.Add(new Setting { Key = key, Value = value });
        else setting.Value = value;
    }

    public CashMovement RecordCashMovement(Guid userId, CashMovementType type, decimal amount, string reason,
        CashMovementCategory category = CashMovementCategory.Unclassified, string? reference = null,
        string? destination = null, string? expenseCategory = null, bool approve = false,
        bool companyOwnedDestination = false, string? counterpartAccountCode = null)
    {
        if (amount <= 0) throw new InvalidOperationException("Amount must be greater than zero.");
        _db.RequireBusinessAuthorization();
        using var rollback = new PostingRollbackScope(_db);
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A reason is required.");
        if (!Enum.IsDefined(type) || !Enum.IsDefined(category)) throw new InvalidOperationException("Invalid movement type or category.");
        if (Math.Round(amount, 2) != amount) throw new InvalidOperationException("Amounts must have at most two decimal places.");
        if (category is CashMovementCategory.Expense or CashMovementCategory.BankDeposit or CashMovementCategory.Remittance or CashMovementCategory.OwnerWithdrawal && type != CashMovementType.CashOut)
            throw new InvalidOperationException("Expenses, deposits, remittances and owner withdrawals must be cash out.");
        if (category == CashMovementCategory.Expense && string.IsNullOrWhiteSpace(expenseCategory))
            throw new InvalidOperationException("An expense category is required.");
        if (category is CashMovementCategory.BankDeposit or CashMovementCategory.Remittance &&
            (string.IsNullOrWhiteSpace(destination) || string.IsNullOrWhiteSpace(reference)))
            throw new InvalidOperationException("A destination and reference are required for deposits/remittances.");
        if (approve) RequireMovementAdmin(userId);
        var shift = GetOpenShift() ?? throw new InvalidOperationException("No open shift.");
        RequireDrawerAccess(shift, userId);
        var counterpart = AccountingPolicy.CashCounterpart(_db, userId, category, companyOwnedDestination, counterpartAccountCode);
        using var transaction = _db.Database.BeginTransaction();
        var now = RetailForma.Core.BusinessClock.Now;
        var movement = new CashMovement { ShiftId = shift.Id, Type = type, Amount = amount, Reason = reason.Trim(), UserId = userId, MovementDate = now,
            Category = category, Reference = reference?.Trim(), Destination = destination?.Trim(), ExpenseCategory = expenseCategory?.Trim(),
            ApprovedById = approve ? userId : null, ApprovedAt = approve ? now : null };
        _db.CashMovements.Add(movement);
        _db.AuditLogs.Add(new AuditLog { Date = now, UserId = userId, Action = "CashMovementCreate", EntityName = "CashMovement",
            EntityId = movement.Id, Details = $"{type}; {category}; {amount}; Ref {movement.Reference}; Approved: {approve}; counterpart {counterpart}; company-owned: {companyOwnedDestination}" });
        if (type == CashMovementType.CashOut)
            Post(now, shift.BranchId, "CashOut", movement.Id, $"CASH OUT: {movement.Reason}", (counterpart, amount, 0), (ShiftAccountCodes.CashOnHand, 0, amount));
        else
            Post(now, shift.BranchId, "CashIn", movement.Id, $"CASH IN: {movement.Reason}", (ShiftAccountCodes.CashOnHand, amount, 0), (counterpart, 0, amount));
        _db.SaveChanges();
        transaction.Commit();
        rollback.Complete();
        return movement;
    }

    private void RequireMovementAdmin(Guid userId)
    {
        if (!_db.Users.AsNoTracking().Any(u => u.Id == userId && u.IsActive && UserAccess.AdministratorRoles.Contains(u.Role)))
            throw new InvalidOperationException("Only active administrators may approve cash movements.");
    }

    public void ApproveCashMovement(Guid movementId, Guid adminId)
    {
        RequireMovementAdmin(adminId);
        var movement = _db.CashMovements.First(m => m.Id == movementId);
        _db.Entry(movement).Reload();
        if (movement.ApprovedAt.HasValue) throw new InvalidOperationException("This movement is already approved.");
        movement.ApprovedById = adminId; movement.ApprovedAt = RetailForma.Core.BusinessClock.Now;
        _db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, UserId = adminId, Action = "CashMovementApproval",
            EntityName = "CashMovement", EntityId = movementId, Details = $"Approved {movement.Category}: {movement.Amount}" });
        _db.SaveChanges();
    }

    public static readonly decimal[] Denominations = { 1000, 500, 200, 100, 50, 20, 10, 5, 1, 0.25m, 0.10m, 0.05m, 0.01m };
    public const string MinimumCountingDenominationKey = "MinimumCountingDenomination";
    public decimal GetMinimumCountingDenomination()
    {
        var value = _db.Settings.AsNoTracking().Where(s => s.Key == MinimumCountingDenominationKey).Select(s => s.Value).FirstOrDefault();
        if (value == null) return 0.25m;
        if (!decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var minimum) || !Denominations.Contains(minimum))
            throw new InvalidOperationException("Invalid cash-counting minimum. Ask an administrator to update Cash Counting Settings.");
        return minimum;
    }
    public decimal[] GetCountingDenominations()
    {
        var minimum = GetMinimumCountingDenomination();
        return Denominations.Where(d => d >= minimum).ToArray();
    }
    public void ConfigureCountingMinimum(Guid adminId, decimal minimum)
    {
        if (!_db.Users.AsNoTracking().Any(u => u.Id == adminId && u.IsActive && UserAccess.AdministratorRoles.Contains(u.Role)))
            throw new InvalidOperationException("Only active administrators can change cash counting settings.");
        if (!Denominations.Contains(minimum)) throw new InvalidOperationException("Choose a supported bill or coin denomination.");
        SetDrawerSetting(MinimumCountingDenominationKey, minimum.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        _db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, UserId = adminId, Action = "CashCountingSettings",
            EntityName = "Setting", Details = $"Minimum counting denomination: {minimum:0.00}" });
        _db.SaveChanges();
    }

    public sealed record ShiftTotals(decimal CashSales, decimal VoidedCashSales, decimal NonCashSales, decimal CashIn, decimal CashOut, int TransactionCount);

    public ShiftTotals GetShiftTotals(Guid shiftId)
    {
        var shift = _db.CashShifts.AsNoTracking().Include(s => s.Movements).First(s => s.Id == shiftId);
        var end = shift.ClosedAt ?? RetailForma.Core.BusinessClock.Now;
        var sales = _db.Sales.AsNoTracking().Include(s => s.Payments)
            .Where(s => s.CashShiftId == shift.Id || (s.CashShiftId == null &&
                s.BranchId == shift.BranchId && s.SaleDate >= shift.OpenedAt && s.SaleDate <= end))
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
            nonCash += sale.Payments.Where(p => p.Method != PaymentMethod.Cash).Sum(p => p.Amount);
        }
        var voidedCashSales = _db.Sales.AsNoTracking().Include(s => s.Payments)
            .Where(s => s.IsVoided && (s.VoidCashShiftId == shift.Id ||
                (s.VoidCashShiftId == null && s.BranchId == shift.BranchId &&
                s.VoidedAt >= shift.OpenedAt && s.VoidedAt <= end)))
            .ToList()
            .Where(s => s.Payments.Any(p => p.Method == PaymentMethod.Cash))
            .ToList();
        var voidedCash = voidedCashSales.Sum(s => s.Payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount));

        // Same-shift voids were already excluded above. A refund for a sale from
        // an earlier shift reduces the cash actually present in this drawer.
        cashSales -= voidedCashSales
            .Where(s => s.CashShiftId != shift.Id && s.SaleDate < shift.OpenedAt)
            .Sum(s => s.Payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount));
        var cashIn = shift.Movements.Where(m => m.Type == CashMovementType.CashIn).Sum(m => m.Amount);
        var cashOut = shift.Movements.Where(m => m.Type == CashMovementType.CashOut).Sum(m => m.Amount);
        return new ShiftTotals(cashSales, voidedCash, nonCash, cashIn, cashOut, count);
    }

    public CashShift CloseShift(Guid userId, decimal countedDrawer, string? notes, IReadOnlyDictionary<decimal, int>? denominations = null,
        Guid? shiftId = null, ShiftTotals? reviewedTotals = null)
    {
        if (countedDrawer < 0) throw new InvalidOperationException("Counted drawer cannot be negative.");
        _db.RequireBusinessAuthorization();
        using var rollback = new PostingRollbackScope(_db);
        if (Math.Round(countedDrawer, 2) != countedDrawer)
            throw new InvalidOperationException("Counted cash must have at most two decimal places.");
        using var transaction = _db.Database.BeginTransaction();
        var shift = shiftId.HasValue
            ? _db.CashShifts.FirstOrDefault(s => s.Id == shiftId.Value)
            : GetOpenShift();
        if (shift == null) throw new InvalidOperationException("No open shift.");
        _db.Entry(shift).Reload();
        if (shift.ClosedAt.HasValue) throw new InvalidOperationException("This shift is already closed.");
        RequireDrawerAccess(shift, userId);
        if (denominations != null)
        {
            if (denominations.Any(p => !Denominations.Contains(p.Key) || p.Value < 0))
                throw new InvalidOperationException("Invalid denomination or negative count.");
            var minimum = GetMinimumCountingDenomination();
            if (denominations.Any(p => p.Value > 0 && p.Key < minimum))
                throw new InvalidOperationException($"The minimum counting denomination is now ₱{minimum:0.00}. Reopen counting and review the entered quantities.");
            if (denominations.Sum(p => p.Key * p.Value) != countedDrawer)
                throw new InvalidOperationException("Denomination counts must equal the counted drawer.");
        }
        var totals = GetShiftTotals(shift.Id);
        if (reviewedTotals != null && totals != reviewedTotals)
            throw new InvalidOperationException("Shift activity changed. Review the updated cash totals and confirm again.");
        var expected = shift.OpeningFloat + totals.CashSales + totals.CashIn - totals.CashOut;
        var variance = Math.Round(countedDrawer - expected, 2, MidpointRounding.AwayFromZero);
        var now = RetailForma.Core.BusinessClock.Now;
        shift.ClosedAt = now; shift.ClosedById = userId; shift.CountedDrawer = countedDrawer; shift.ExpectedDrawer = expected; shift.Variance = variance; shift.Notes = notes?.Trim();
        if (denominations != null)
        {
            shift.DenominationCounts = denominations.Select(p => new DenominationCount { ShiftId = shift.Id, FaceValue = p.Key, Count = p.Value }).ToList();
            _db.DenominationCounts.AddRange(shift.DenominationCounts);
        }
        if (variance > 0) Post(now, shift.BranchId, "ShiftClose", shift.Id, $"Z-READING: VARIANCE +{variance:N2}", (ShiftAccountCodes.CashOnHand, variance, 0), (ShiftAccountCodes.CashOverShort, 0, variance));
        else if (variance < 0) Post(now, shift.BranchId, "ShiftClose", shift.Id, $"Z-READING: VARIANCE {variance:N2}", (ShiftAccountCodes.CashOverShort, -variance, 0), (ShiftAccountCodes.CashOnHand, 0, -variance));
        _db.AuditLogs.Add(new AuditLog { Date = now, UserId = userId, Action = "ShiftClose", EntityName = "CashShift", EntityId = shift.Id, Details = $"EXPECTED {expected:N2} COUNTED {countedDrawer:N2} VARIANCE {variance:+0.00;-0.00}" });
        _db.SaveChanges();
        transaction.Commit();
        rollback.Complete();
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
