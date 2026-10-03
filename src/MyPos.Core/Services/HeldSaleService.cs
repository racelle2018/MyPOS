using System.Text.Json;
using MyPos.Core.Data;
using MyPos.Core.Entities;

namespace MyPos.Core.Services;

public sealed record HeldCartLine(Guid ProductId, string Name, string? Barcode, decimal Price, decimal UnitCost, bool IsVatExempt, decimal Qty, decimal StockAvailable);

public sealed class HeldSaleService
{
    private readonly MyPosDbContext _db;
    public HeldSaleService(MyPosDbContext db) => _db = db;

    public List<HeldSale> ListActive(Guid branchId)
    {
        var cutoff = DateTime.Now.AddHours(-24);
        var stale = _db.HeldSales.Where(h => h.HeldAt < cutoff).ToList();
        foreach (var held in stale)
        {
            _db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, Action = "HeldSaleExpired", EntityName = "HeldSale", EntityId = held.Id, Details = $"{held.CustomerName} - HELD {held.HeldAt:MM/dd HH:mm}" });
        }
        if (stale.Count > 0) { _db.HeldSales.RemoveRange(stale); _db.SaveChanges(); }
        return _db.HeldSales.Where(h => h.BranchId == branchId).OrderByDescending(h => h.HeldAt).ToList();
    }

    public HeldSale Hold(Guid branchId, Guid userId, List<HeldCartLine> cart, string? customerName, string? invoice, string? notes)
    {
        if (cart.Count == 0) throw new InvalidOperationException("Nothing to hold - the cart is empty.");
        var held = new HeldSale { BranchId = branchId, UserId = userId, HeldAt = DateTime.Now, CustomerName = customerName?.Trim().ToUpperInvariant() ?? "", InvoiceNumber = string.IsNullOrWhiteSpace(invoice) ? null : invoice.Trim().ToUpperInvariant(), Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim().ToUpperInvariant(), CartJson = JsonSerializer.Serialize(cart) };
        _db.HeldSales.Add(held);
        _db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, UserId = userId, Action = "HeldSaleCreate", EntityName = "HeldSale", EntityId = held.Id, Details = $"{held.CustomerName} - {cart.Count} item(s)" });
        _db.SaveChanges();
        return held;
    }

    public List<HeldCartLine> Recall(Guid heldSaleId)
    {
        var held = _db.HeldSales.Find(heldSaleId) ?? throw new InvalidOperationException("Held sale not found.");
        var lines = JsonSerializer.Deserialize<List<HeldCartLine>>(held.CartJson) ?? new();
        _db.HeldSales.Remove(held);
        _db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, UserId = held.UserId, Action = "HeldSaleRecall", EntityName = "HeldSale", EntityId = held.Id, Details = held.CustomerName });
        _db.SaveChanges();
        return lines;
    }

    public void Discard(Guid heldSaleId, Guid userId)
    {
        var held = _db.HeldSales.Find(heldSaleId) ?? throw new InvalidOperationException("Held sale not found.");
        _db.HeldSales.Remove(held);
        _db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, UserId = userId, Action = "HeldSaleDiscard", EntityName = "HeldSale", EntityId = held.Id, Details = held.CustomerName });
        _db.SaveChanges();
    }
}
