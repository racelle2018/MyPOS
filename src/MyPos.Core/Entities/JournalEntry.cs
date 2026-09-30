namespace MyPos.Core.Entities;

public class JournalEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime EntryDate { get; set; }
    public Guid BranchId { get; set; }
    public string SourceType { get; set; } = "";    // "Sale" | "Void" | "Purchase" | "Adjustment"
    public Guid SourceId { get; set; }
    public string Description { get; set; } = "";
    public List<JournalLine> Lines { get; set; } = new();
}

public class JournalLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}