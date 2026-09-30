using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;

namespace MyPos.Core.Services;

/// <summary>A cart line passed to PostSale. Price and cost are read fresh at commit time.
public record CartLine(Guid ProductId, decimal Qty);

public static class AccountCodes
{
    public const string CashOnHand = "1000";
    public const string CashInBank = "1010";
    public const string Inventory = "1200";
    public const string VatPayable = "2000";
    public const string SalesRevenue = "4000";
    public const string Cogs = "5000";
}

public class SaleService
{
    private readonly MyPosDbContext _db;

    public SaleService(MyPosDbContext db) => _db = db;

    // ---------- Receiving stock (purchases / opening inventory) ----------

    public InventoryMovement ReceiveStock(Guid productId, decimal qty, decimal unitCost, Guid userId,
        string notes = "", string? paidFromAccountCode = null)
    {
        if (qty <= 0) throw new ArgumentException("Quantity must be positive.", nameof(qty));
        if (unitCost < 0) throw new ArgumentException("Unit cost cannot be negative.", nameof(unitCost));

        using var tx = _db.Database.BeginTransaction();

        var product = _db.Products.First(p => p.Id == productId);

        // Weighted average cost — recalculated on every receipt
        var newQty = product.StockQty + qty;
        product.CostPrice = newQty == 0
            ? unitCost
            : Math.Round((product.StockQty * product.CostPrice + qty * unitCost) / newQty,
                4, MidpointRounding.AwayFromZero);
        product.StockQty = newQty;

        var movement = new InventoryMovement
        {
            ProductId = productId, Type = MovementType.Purchase, Qty = qty, UnitCost = unitCost,
            MovementDate = DateTime.Now, SourceType = "Purchase", UserId = userId, Notes = notes
        };
        _db.InventoryMovements.Add(movement);

        var amount = Round2(qty * unitCost);
        if (amount != 0)
            PostJournal(DateTime.Now, FirstBranchId(), "Purchase", movement.Id,
                $"Stock received: {product.Name}",
                (paidFromAccountCode ?? AccountCodes.CashOnHand, amount, 0),
                (AccountCodes.Inventory, 0, amount));

        _db.SaveChanges();
        tx.Commit();
        return movement;
    }

    // ---------- Posting a sale ----------

    public Sale PostSale(Guid branchId, Guid cashierId, IList<CartLine> lines, decimal discount = 0,
        decimal tendered = 0, PaymentMethod method = PaymentMethod.Cash, string? reference = null,
        ReceiptType receiptType = ReceiptType.None, string? manualReceiptNumber = null)
    {
        if (lines is not { Count: > 0 }) throw new InvalidOperationException("Cart is empty.");
        if (discount < 0) throw new InvalidOperationException("Discount cannot be negative.");

        using var tx = _db.Database.BeginTransaction();

        var branch = _db.Branches.First(b => b.Id == branchId);

        // 1) Load products fresh, validate stock
        var ids = lines.Select(l => l.ProductId).ToList();
        var products = _db.Products.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id);

        var prepared = new List<PreparedLine>();
        foreach (var line in lines)
        {
            if (line.Qty <= 0) throw new InvalidOperationException("Quantity must be positive.");
            if (!products.TryGetValue(line.ProductId, out var p) || !p.IsActive)
                throw new InvalidOperationException("Product not found or inactive.");
            if (p.StockQty < line.Qty)
                throw new InvalidOperationException($"Insufficient stock for '{p.Name}' (on hand: {p.StockQty}).");
            prepared.Add(new PreparedLine { Product = p, Qty = line.Qty, LineGross = Round2(p.Price * line.Qty) });
        }

        var gross = prepared.Sum(x => x.LineGross);
        if (discount > gross) throw new InvalidOperationException("Discount exceeds sale amount.");
        var total = gross - discount;
        if (tendered < total) throw new InvalidOperationException("Tendered amount is less than the total.");

        // 2) Allocate discount proportionally; last line absorbs rounding
        if (gross > 0)
        {
            decimal allocated = 0;
            for (var i = 0; i < prepared.Count; i++)
            {
                prepared[i].DiscountShare = i == prepared.Count - 1
                    ? discount - allocated
                    : Round2(discount * prepared[i].LineGross / gross);
                allocated += prepared[i].DiscountShare;
            }
        }

        // 3) VAT split per line — round the net, derive VAT by subtraction
        var vatRate = decimal.Parse(
            _db.Settings.First(s => s.Key == "VatRate").Value,
            System.Globalization.CultureInfo.InvariantCulture);

        decimal netSum = 0;
        foreach (var line in prepared)
        {
            var lineTotal = line.LineGross - line.DiscountShare;
            line.LineNet = line.Product.IsVatExempt ? lineTotal : Round2(lineTotal / (1m + vatRate));
            netSum += line.LineNet;
        }
        var net = netSum;
        var vat = total - net;   // derived — can never drift from the total

        // 4) Build the sale; gapless numbers assigned inside the transaction
        var now = DateTime.Now;

        string? receiptNo = null;
        if (receiptType == ReceiptType.System)
            receiptNo = $"R{branch.NextReceiptNumber++:D6}";
        else if (receiptType == ReceiptType.Manual)
            receiptNo = manualReceiptNumber
                ?? throw new InvalidOperationException("Manual receipt number is required.");

        var sale = new Sale
        {
            BranchId = branchId, CashierId = cashierId, SaleDate = now,
            SaleNumber = branch.NextSaleNumber++,
            GrossAmount = gross, DiscountAmount = discount, TotalAmount = total,
            NetAmount = net, VatAmount = vat, VatRate = vatRate,
            TenderedAmount = tendered, ChangeAmount = tendered - total,
            ReceiptType = receiptType, ReceiptNumber = receiptNo
        };

        // 5) Items, payment, stock movements + cache update
        foreach (var line in prepared)
        {
            var p = line.Product;
            sale.Items.Add(new SaleItem
            {
                ProductId = p.Id, ProductName = p.Name, Barcode = p.Barcode,
                Qty = line.Qty, UnitPrice = p.Price, LineGross = line.LineGross, UnitCost = p.CostPrice
            });
            _db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = p.Id, Type = MovementType.Sale, Qty = line.Qty, UnitCost = p.CostPrice,
                MovementDate = now, SourceType = "Sale", SourceId = sale.Id
            });
            p.StockQty -= line.Qty;
        }
        sale.Payments.Add(new Payment { Method = method, Amount = total, Reference = reference });
        _db.Sales.Add(sale);

        // 6) The double entry — balanced by construction:
        //    debits  = total + cogs
        //    credits = net + vat + cogs, and total = net + vat by rule 3
        var cogs = prepared.Sum(x => Round2(x.Product.CostPrice * x.Qty));
        var debitAccount = method == PaymentMethod.Cash ? AccountCodes.CashOnHand : AccountCodes.CashInBank;

        PostJournal(now, branchId, "Sale", sale.Id, $"Sale #{sale.SaleNumber}",
            (debitAccount, total, 0),
            (AccountCodes.SalesRevenue, 0, net),
            (AccountCodes.VatPayable, 0, vat),
            (AccountCodes.Cogs, cogs, 0),
            (AccountCodes.Inventory, 0, cogs));

        _db.SaveChanges();
        tx.Commit();
        return sale;
    }

    // ---------- Voiding (sales are never edited or deleted) ----------

    public void VoidSale(Guid saleId, Guid userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A void reason is required.", nameof(reason));

        using var tx = _db.Database.BeginTransaction();

        var sale = _db.Sales.Include(s => s.Items).First(s => s.Id == saleId);
        if (sale.IsVoided) throw new InvalidOperationException("This sale is already voided.");

        var original = _db.JournalEntries.Include(e => e.Lines)
            .First(e => e.SourceType == "Sale" && e.SourceId == sale.Id);

        // Reversing entry — every debit becomes a credit and vice versa
        var now = DateTime.Now;
        var reversal = new JournalEntry
        {
            EntryDate = now, BranchId = sale.BranchId, SourceType = "Void", SourceId = sale.Id,
            Description = $"Reversal of Sale #{sale.SaleNumber}"
        };
        foreach (var line in original.Lines)
            reversal.Lines.Add(new JournalLine { AccountId = line.AccountId, Debit = line.Credit, Credit = line.Debit });
        _db.JournalEntries.Add(reversal);

        // Put the stock back
        var ids = sale.Items.Select(i => i.ProductId).ToList();
        var products = _db.Products.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id);
        foreach (var item in sale.Items)
        {
            _db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = item.ProductId, Type = MovementType.Return, Qty = item.Qty, UnitCost = item.UnitCost,
                MovementDate = now, SourceType = "Void", SourceId = sale.Id,
                Notes = $"Void of Sale #{sale.SaleNumber}", UserId = userId
            });
            products[item.ProductId].StockQty += item.Qty;
        }

        sale.IsVoided = true;
        sale.VoidedAt = now;
        sale.VoidedById = userId;
        sale.VoidReason = reason;

        _db.AuditLogs.Add(new AuditLog
        {
            Date = now, UserId = userId, Action = "VoidSale", EntityName = "Sale",
            EntityId = sale.Id, Details = $"Sale #{sale.SaleNumber}: {reason}"
        });

        _db.SaveChanges();
        tx.Commit();
    }

    // ---------- Stock corrections (physical count, damages) ----------

    public InventoryMovement AdjustStock(Guid productId, decimal qtyChange, Guid userId, string reason)
    {
        if (qtyChange == 0) throw new ArgumentException("Quantity change cannot be zero.", nameof(qtyChange));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));

        using var tx = _db.Database.BeginTransaction();

        var product = _db.Products.First(p => p.Id == productId);
        if (product.StockQty + qtyChange < 0)
            throw new InvalidOperationException("Adjustment would make stock negative.");

        product.StockQty += qtyChange;

        var movement = new InventoryMovement
        {
            ProductId = productId, Type = MovementType.Adjustment, Qty = qtyChange, // signed
            UnitCost = product.CostPrice, MovementDate = DateTime.Now,
            SourceType = "Adjustment", UserId = userId, Notes = reason
        };
        _db.InventoryMovements.Add(movement);

        _db.AuditLogs.Add(new AuditLog
        {
            Date = DateTime.Now, UserId = userId, Action = "AdjustStock", EntityName = "Product",
            EntityId = productId, Details = $"{qtyChange:+0.##;-0.##} — {reason}"
        });

        _db.SaveChanges();
        tx.Commit();
        return movement;
    }

    // ---------- helpers ----------

    private JournalEntry PostJournal(DateTime date, Guid branchId, string sourceType, Guid sourceId,
        string description, params (string code, decimal debit, decimal credit)[] lines)
    {
        var entry = new JournalEntry
        {
            EntryDate = date, BranchId = branchId, SourceType = sourceType,
            SourceId = sourceId, Description = description
        };
        foreach (var (code, debit, credit) in lines)
        {
            if (debit == 0 && credit == 0) continue;   // e.g. zero VAT, zero cost
            entry.Lines.Add(new JournalLine { AccountId = AccountByCode(code).Id, Debit = debit, Credit = credit });
        }
        _db.JournalEntries.Add(entry);
        return entry;
    }

    private Account AccountByCode(string code) => _db.Accounts.First(a => a.Code == code);
    private Guid FirstBranchId() => _db.Branches.OrderBy(b => b.CreatedAt).First().Id;
    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private sealed class PreparedLine
    {
        public Product Product = null!;
        public decimal Qty;
        public decimal LineGross;     // price × qty, VAT-inclusive
        public decimal DiscountShare;
        public decimal LineNet;       // VAT-exclusive
        public decimal LineVat;
    }
}