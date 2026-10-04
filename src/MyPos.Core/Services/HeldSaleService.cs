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
        return _db.HeldSales.Where(h => h.BranchId == branchId).OrderByDescending(h => h.HeldAt).ToList();
    }

    public HeldSale Hold(
        Guid branchId,
        Guid userId,
        List<HeldCartLine> cart,
        string? customerName,
        string? invoice,
        string? notes,
        string? customerAddress = null,
        PaymentMethod paymentMethod = PaymentMethod.Cash,
        OrderType orderType = OrderType.WalkIn,
        DiscountKind discountKind = DiscountKind.None,
        decimal discountAmount = 0,
        string? seniorIdNumber = null)
    {
        if (cart.Count == 0) throw new InvalidOperationException("Nothing to hold - the cart is empty.");
        var held = new HeldSale
        {
            BranchId = branchId,
            UserId = userId,
            HeldAt = DateTime.Now,
            CustomerName = customerName?.Trim().ToUpperInvariant() ?? "",
            CustomerAddress = customerAddress?.Trim(),
            InvoiceNumber = string.IsNullOrWhiteSpace(invoice) ? null : invoice.Trim().ToUpperInvariant(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            PaymentMethod = paymentMethod,
            OrderType = orderType,
            DiscountKind = discountKind,
            DiscountAmount = discountAmount,
            SeniorIdNumber = seniorIdNumber?.Trim(),
            CartJson = JsonSerializer.Serialize(cart)
        };
        _db.HeldSales.Add(held);
        _db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, UserId = userId, Action = "HeldSaleCreate", EntityName = "HeldSale", EntityId = held.Id, Details = $"{held.CustomerName} - {cart.Count} item(s)" });
        _db.SaveChanges();
        return held;
    }

    public List<HeldCartLine> ReadCart(Guid heldSaleId)
    {
        var held = _db.HeldSales.Find(heldSaleId) ?? throw new InvalidOperationException("Held sale not found.");
        return JsonSerializer.Deserialize<List<HeldCartLine>>(held.CartJson)
            ?? throw new InvalidDataException("Held cart data could not be read.");
    }

    public void CompleteRecall(Guid heldSaleId, Guid userId)
    {
        var held = _db.HeldSales.Find(heldSaleId) ?? throw new InvalidOperationException("Held sale not found.");
        using var transaction = _db.Database.BeginTransaction();
        _db.HeldSales.Remove(held);
        _db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, UserId = userId, Action = "HeldSaleRecall", EntityName = "HeldSale", EntityId = held.Id, Details = held.CustomerName });
        _db.SaveChanges();
        transaction.Commit();
    }

    public void Discard(Guid heldSaleId, Guid userId)
    {
        var held = _db.HeldSales.Find(heldSaleId) ?? throw new InvalidOperationException("Held sale not found.");
        _db.HeldSales.Remove(held);
        _db.AuditLogs.Add(new AuditLog { Date = DateTime.Now, UserId = userId, Action = "HeldSaleDiscard", EntityName = "HeldSale", EntityId = held.Id, Details = held.CustomerName });
        _db.SaveChanges();
    }
}
