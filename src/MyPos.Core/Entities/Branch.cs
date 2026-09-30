namespace MyPos.Core.Entities;

public class Branch : EntityBase
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";

    // Gapless sequences — incremented inside the sale transaction
    public int NextSaleNumber { get; set; } = 1;
    public int NextReceiptNumber { get; set; } = 1;   // independent from sales
}