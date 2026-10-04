namespace MyPos.Core.Entities;

public class HeldSale : EntityBase
{
    public Guid BranchId { get; set; }
    public Guid UserId { get; set; }
    public DateTime HeldAt { get; set; }
    public string CustomerName { get; set; } = "";
    public string? CustomerAddress { get; set; }
    public string? InvoiceNumber { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public OrderType OrderType { get; set; } = OrderType.WalkIn;
    public DiscountKind DiscountKind { get; set; } = DiscountKind.None;
    public decimal DiscountAmount { get; set; }
    public string? SeniorIdNumber { get; set; }
    public string? Notes { get; set; }
    public string CartJson { get; set; } = "";
}
