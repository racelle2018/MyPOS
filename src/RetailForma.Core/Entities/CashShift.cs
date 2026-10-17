namespace RetailForma.Core.Entities;

public class CashShift : EntityBase
{
    public Guid BranchId { get; set; }
    public Guid UserId { get; set; }
    public DrawerMode DrawerMode { get; set; }
    public Guid? AssignedCashierId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal? CountedDrawer { get; set; }
    public decimal ExpectedDrawer { get; set; }
    public decimal Variance { get; set; }
    public Guid? ClosedById { get; set; }
    public List<DenominationCount> DenominationCounts { get; set; } = new();
    public string? Notes { get; set; }
    public List<CashMovement> Movements { get; set; } = new();
}

public enum DrawerMode { Shared = 0, Assigned = 1 }

public enum CashMovementType { CashIn = 1, CashOut = 2 }
public enum CashMovementCategory { Unclassified = 0, Expense = 1, BankDeposit = 2, Remittance = 3, OwnerWithdrawal = 4, FloatTransfer = 5, Other = 6 }

public class DenominationCount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShiftId { get; set; }
    public decimal FaceValue { get; set; }
    public int Count { get; set; }
}

public class CashMovement : EntityBase
{
    public Guid ShiftId { get; set; }
    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTime MovementDate { get; set; }
    public CashMovementCategory Category { get; set; }
    public string? ExpenseCategory { get; set; }
    public string? Destination { get; set; }
    public string? Reference { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
