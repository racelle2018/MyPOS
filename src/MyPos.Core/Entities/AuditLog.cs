namespace MyPos.Core.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";        // "VoidSale", "PriceChange", "SettingChanged"...
    public string EntityName { get; set; } = "";
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
}