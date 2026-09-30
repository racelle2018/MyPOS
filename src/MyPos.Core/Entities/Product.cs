namespace MyPos.Core.Entities;

public class Product : EntityBase
{
    public string? Barcode { get; set; }        // null if none (nulls don't break the unique index)
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "pc";
    public string Category { get; set; } = "";
    public decimal CostPrice { get; set; }      // weighted-average cost
    public decimal Price { get; set; }          // VAT-INCLUSIVE retail price
    public bool IsVatExempt { get; set; }
    public decimal StockQty { get; set; }       // CACHE — movements are the source of truth
    public bool IsActive { get; set; } = true;
}