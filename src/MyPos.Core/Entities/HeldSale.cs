namespace MyPos.Core.Entities;

public class HeldSale : EntityBase
{
    public Guid BranchId { get; set; }
    public Guid UserId { get; set; }
    public DateTime HeldAt { get; set; }
    public string CustomerName { get; set; } = "";
    public string? InvoiceNumber { get; set; }
    public string? Notes { get; set; }
    public string CartJson { get; set; } = "";
}
