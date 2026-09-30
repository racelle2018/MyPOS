namespace MyPos.Core.Entities;

public class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public MovementType Type { get; set; }
    public decimal Qty { get; set; }   // positive for Purchase/Return/Sale; SIGNED for Adjustment
    public decimal UnitCost { get; set; }
    public DateTime MovementDate { get; set; }
    public string SourceType { get; set; } = "";    // "Sale" | "Purchase" | "Adjustment" | "Void"
    public Guid? SourceId { get; set; }
    public string? Notes { get; set; }
    public Guid? UserId { get; set; }
}

public enum MovementType { Purchase = 1, Sale = 2, Adjustment = 3, Return = 4 }