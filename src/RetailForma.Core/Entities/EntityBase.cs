namespace RetailForma.Core.Entities;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = RetailForma.Core.BusinessClock.Now;
}
