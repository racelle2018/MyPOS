namespace MyPos.Core.Entities;

public class CashShift : EntityBase
{
    public Guid BranchId { get; set; }
    public Guid UserId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal? CountedDrawer { get; set; }
    public decimal ExpectedDrawer { get; set; }
    public decimal Variance { get; set; }
    public string? Notes { get; set; }
    public List<CashMovement> Movements { get; set; } = new();
}

public enum CashMovementType { CashIn = 1, CashOut = 2 }

public class CashMovement : EntityBase
{
    public Guid ShiftId { get; set; }
    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTime MovementDate { get; set; }
}
