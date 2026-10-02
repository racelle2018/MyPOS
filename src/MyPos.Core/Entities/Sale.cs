namespace MyPos.Core.Entities;

public class Sale : EntityBase
{
    public Guid BranchId { get; set; }
    public int SaleNumber { get; set; }               // gapless, per branch
    public Guid? CashierId { get; set; }
    public DateTime SaleDate { get; set; }

    // Money snapshot — never recomputed after commit
    public decimal GrossAmount { get; set; }   // sum of line gross, before discount
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }   // what the customer pays (VAT-inclusive)
    public decimal VatAmount { get; set; }     // VAT portion of TotalAmount
    public decimal NetAmount { get; set; }     // TotalAmount − VatAmount
    public decimal VatRate { get; set; }       // e.g. 0.12m, snapshotted
    public decimal TenderedAmount { get; set; }
    public decimal ChangeAmount { get; set; }

    // Receipt linkage — decoupled from SaleNumber
    public ReceiptType ReceiptType { get; set; }
    public string? ReceiptNumber { get; set; }        // manual OR no. or system-printed no.
    public OrderType OrderType { get; set; } = OrderType.WalkIn;

    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerAddress { get; set; }

    // Voids — sales are never edited or deleted
    public bool IsVoided { get; set; }
    public Guid? VoidedById { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }

    public List<SaleItem> Items { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

public enum ReceiptType { None = 0, Manual = 1, System = 2 }

public enum OrderType { WalkIn = 1, PickUp = 2, Delivery = 3 }

public class SaleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";   // snapshots — recorded sales never change
    public string? Barcode { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }          // VAT-inclusive at time of sale
    public decimal LineGross { get; set; }
    public decimal UnitCost { get; set; }           // for the COGS journal line
}

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleId { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public decimal Amount { get; set; }
    public string? Reference { get; set; }          // GCash ref, card approval code...
}

public enum PaymentMethod { Cash = 1, Card = 2, Gcash = 3, Maya = 4, Bank = 5 }
