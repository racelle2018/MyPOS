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
        notes = notes?.Trim().ToUpperInvariant() ?? "";

        using var tx = _db.Database.BeginTransaction();

        var product = _db.Products.First(p => p.Id == productId);
        _db.Entry(product).Reload();

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
        ReceiptType receiptType = ReceiptType.None, string? manualReceiptNumber = null,
        string? customerName = null, string? customerAddress = null,
        OrderType orderType = OrderType.WalkIn, DiscountKind discountKind = DiscountKind.None,
        string? seniorIdNumber = null, decimal? expectedTotal = null)
    {
        if (lines is not { Count: > 0 }) throw new InvalidOperationException("Cart is empty.");
        customerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim().ToUpperInvariant();
        customerAddress = string.IsNullOrWhiteSpace(customerAddress) ? null : customerAddress.Trim().ToUpperInvariant();
        manualReceiptNumber = string.IsNullOrWhiteSpace(manualReceiptNumber) ? null : manualReceiptNumber.Trim().ToUpperInvariant();
        if (discount < 0) throw new InvalidOperationException("Discount cannot be negative.");
        if (discountKind == DiscountKind.SeniorPwd)
        {
            if (discount > 0) throw new InvalidOperationException("Senior/PWD discount cannot be combined with a regular discount.");
            if (string.IsNullOrWhiteSpace(seniorIdNumber)) throw new InvalidOperationException("Senior/PWD ID number is required.");
        }

        using var tx = _db.Database.BeginTransaction();

        var branch = _db.Branches.First(b => b.Id == branchId);
        _db.Entry(branch).Reload();

        // 1) Load products fresh, validate stock
        var ids = lines.Select(l => l.ProductId).ToList();
        var products = _db.Products.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id);
        foreach (var product in products.Values) _db.Entry(product).Reload();

        var prepared = new List<PreparedLine>();
        foreach (var line in lines)
        {
            if (line.Qty <= 0) throw new InvalidOperationException("Quantity must be positive.");
            if (!products.TryGetValue(line.ProductId, out var p) || !p.IsActive)
                throw new InvalidOperationException("Product not found or inactive.");
            if (p.StockQty < line.Qty)
                throw new InvalidOperationException($"Insufficient stock for '{p.Name}' (on hand: {p.StockQty}).");
            prepared.Add(new PreparedLine
            {
                Product = p,
                Qty = line.Qty,
                LineGross = Round2(p.Price * line.Qty)
            });
        }

        var gross = prepared.Sum(x => Round2(x.Product.Price * x.Qty));

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

        var calc = SaleCalculator.Compute(
            prepared.Select(x => new SaleCalculator.Line(x.Product.Price, x.Qty, x.Product.CostPrice, x.Product.IsVatExempt)).ToList(),
            discount, vatRate, discountKind);
        if (expectedTotal.HasValue && calc.Total != expectedTotal.Value)
            throw new InvalidOperationException("The sale total changed while payment was open. Review the cart and try Pay again.");
        if (discount > calc.Gross) throw new InvalidOperationException("Discount exceeds sale amount.");
        if (tendered < calc.Total) throw new InvalidOperationException("Tendered amount is less than the total.");
        var total = calc.Total;

        var net = calc.Net;
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
            OrderType = orderType,
            GrossAmount = calc.Gross, DiscountAmount = calc.Discount, TotalAmount = calc.Total,
            NetAmount = calc.Net, VatAmount = calc.Vat, VatRate = vatRate,
            TenderedAmount = tendered, ChangeAmount = tendered - calc.Total,
            ReceiptType = receiptType, ReceiptNumber = receiptNo
            , DiscountKind = discountKind, SeniorIdNumber = seniorIdNumber?.Trim().ToUpperInvariant()
        };

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var name = customerName.Trim();
            var customer = _db.Customers.ToList()
                .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (customer == null)
            {
                customer = new Customer { Name = name, Address = customerAddress?.Trim() ?? "" };
                _db.Customers.Add(customer);
            }
            else if (customer.Address.Length == 0 && !string.IsNullOrWhiteSpace(customerAddress))
                customer.Address = customerAddress.Trim();

            sale.CustomerId = customer.Id;
            sale.CustomerName = name;
            sale.CustomerAddress = string.IsNullOrWhiteSpace(customerAddress) ? null : customerAddress.Trim();
        }

        if (receiptType == ReceiptType.Manual && manualReceiptNumber != null)
        {
            var duplicate = _db.Sales.ToList().Any(s =>
                s.BranchId == branchId &&
                s.ReceiptNumber == manualReceiptNumber &&
                !s.IsVoided);

            if (duplicate)
                throw new InvalidOperationException(
                    $"Invoice/OR number '{manualReceiptNumber}' was already used.");
        }

        // 5) Items, payment, stock movements + cache update
        foreach (var line in prepared)
        {
            var p = line.Product;
            sale.Items.Add(new SaleItem
            {
                ProductId = p.Id, ProductName = p.Name, Barcode = p.Barcode,
                Qty = line.Qty, UnitPrice = p.Price, LineGross = calc.LineGrosses[prepared.IndexOf(line)], UnitCost = p.CostPrice
            });
            _db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = p.Id, Type = MovementType.Sale, Qty = line.Qty, UnitCost = p.CostPrice,
                MovementDate = now, SourceType = "Sale", SourceId = sale.Id
            });
            p.StockQty -= line.Qty;
        }
        sale.Payments.Add(new Payment { Method = method, Amount = calc.Total, Reference = reference });
        _db.Sales.Add(sale);

        // 6) The double entry — balanced by construction:
        //    debits  = total + cogs
        //    credits = net + vat + cogs, and total = net + vat by rule 3
        var cogs = calc.Cogs;
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
    }
}
