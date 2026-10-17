using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;

namespace RetailForma.Core.Services;

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
    private readonly RetailFormaDbContext _db;

    public SaleService(RetailFormaDbContext db) => _db = db;

    // ---------- Receiving stock (purchases / opening inventory) ----------

    public InventoryMovement ReceiveStock(Guid productId, decimal qty, decimal unitCost, Guid userId,
        string notes = "", string? paidFromAccountCode = null, StockReceiptKind receiptKind = StockReceiptKind.PaidPurchase,
        bool zeroCostConfirmed = false)
    {
        if (qty <= 0) throw new ArgumentException("Quantity must be positive.", nameof(qty));
        _db.RequireBusinessAuthorization();
        using var rollback = new PostingRollbackScope(_db);
        if (unitCost < 0) throw new ArgumentException("Unit cost cannot be negative.", nameof(unitCost));
        notes = notes?.Trim() ?? "";
        if (!Enum.IsDefined(receiptKind)) throw new InvalidOperationException("Invalid stock receipt kind.");
        if (!_db.Users.AsNoTracking().Any(u => u.Id == userId && u.IsActive)) throw new InvalidOperationException("An active receiving user is required.");
        if (receiptKind != StockReceiptKind.PaidPurchase)
        {
            AccountingPolicy.RequireAdmin(_db, userId);
            if (notes.Length == 0) throw new InvalidOperationException("Opening/contributed stock requires valuation/source evidence in notes.");
            if (receiptKind == StockReceiptKind.OwnerContribution) AccountingPolicy.RequireProprietor(_db);
        }
        if (unitCost == 0 && (!zeroCostConfirmed || notes.Length == 0))
            throw new InvalidOperationException("Confirm a genuinely zero carrying cost and document the evidence; unknown cost is not zero.");
        if (unitCost == 0) AccountingPolicy.RequireAdmin(_db, userId);
        var counterpart = receiptKind switch
        {
            StockReceiptKind.OpeningBalance => AccountingPolicy.OpeningClearing,
            StockReceiptKind.OwnerContribution => "3000",
            _ => paidFromAccountCode ?? AccountCodes.CashOnHand
        };
        if (receiptKind == StockReceiptKind.PaidPurchase && counterpart is not ("1000" or "1010"))
            throw new InvalidOperationException("Paid purchases must use cash or company bank, not supplier credit or another counterpart.");
        var sourceType = receiptKind switch { StockReceiptKind.OpeningBalance => "OpeningInventory", StockReceiptKind.OwnerContribution => "StockContribution", _ => "Purchase" };

        using var tx = _db.Database.BeginTransaction();

        var product = _db.Products.First(p => p.Id == productId);
        _db.Entry(product).Reload();
        rollback.Refresh(product);
        if (receiptKind == StockReceiptKind.OpeningBalance && (product.StockQty != 0 || _db.InventoryMovements.Any(m => m.ProductId == productId)))
            throw new InvalidOperationException("Opening inventory is permitted only before this product has stock movements.");

        // Weighted average cost — recalculated on every receipt
        var newQty = product.StockQty + qty;
        product.CostPrice = newQty == 0
            ? unitCost
            : Math.Round((product.StockQty * product.CostPrice + qty * unitCost) / newQty,
                4, MidpointRounding.AwayFromZero);
        product.StockQty = newQty;

        var movement = new InventoryMovement
        {
            ProductId = productId, Type = receiptKind == StockReceiptKind.OpeningBalance ? MovementType.OpeningBalance :
                receiptKind == StockReceiptKind.OwnerContribution ? MovementType.Contribution : MovementType.Purchase, Qty = qty, UnitCost = unitCost,
            MovementDate = RetailForma.Core.BusinessClock.Now, SourceType = sourceType, UserId = userId, Notes = notes
        };
        _db.InventoryMovements.Add(movement);

        var amount = Round2(qty * unitCost);
        if (amount != 0)
            PostJournal(RetailForma.Core.BusinessClock.Now, FirstBranchId(), sourceType, movement.Id,
                $"Stock received: {product.Name}",
                (AccountCodes.Inventory, amount, 0),
                (counterpart, 0, amount));

        _db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, UserId = userId, Action = sourceType,
            EntityName = "InventoryMovement", EntityId = movement.Id, Details = $"Counterpart {counterpart}; zero carrying cost confirmed: {zeroCostConfirmed}; {notes}" });

        _db.SaveChanges();
        tx.Commit();
        rollback.Complete();
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
        _db.RequireBusinessAuthorization();
        using var rollback = new PostingRollbackScope(_db);
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
        var shiftService = new ShiftService(_db);
        var activeShift = shiftService.GetOpenShift();
        if (activeShift != null)
        {
            if (activeShift.BranchId != branchId) throw new InvalidOperationException("The open drawer belongs to another branch.");
            shiftService.RequireDrawerAccess(activeShift, cashierId);
        }
        _db.Entry(branch).Reload();
        rollback.Refresh(branch);

        // 1) Load products fresh, validate stock
        var ids = lines.Select(l => l.ProductId).ToList();
        var products = _db.Products.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id);
        foreach (var product in products.Values) { _db.Entry(product).Reload(); rollback.Refresh(product); }

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

        // SaleCalculator owns both invoice and line allocation snapshots.

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
        var now = RetailForma.Core.BusinessClock.Now;

        string? receiptNo = null;
        if (receiptType == ReceiptType.System)
            receiptNo = $"R{branch.NextReceiptNumber++:D6}";
        else if (receiptType == ReceiptType.Manual)
            receiptNo = manualReceiptNumber
                ?? throw new InvalidOperationException("Manual receipt number is required.");

        var sale = new Sale
        {
            BranchId = branchId, CashierId = cashierId, SaleDate = now,
            CashShiftId = activeShift?.Id,
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
            var money = calc.LineAmounts[prepared.IndexOf(line)];
            sale.Items.Add(new SaleItem
            {
                ProductId = p.Id, ProductName = p.Name, Barcode = p.Barcode,
                Qty = line.Qty, UnitPrice = p.Price, LineGross = money.Gross, UnitCost = p.CostPrice,
                CategorySnapshot = p.Category ?? "", UnitSnapshot = p.Unit,
                IsVatExemptSnapshot = p.IsVatExempt,
                TaxTreatmentSnapshot = discountKind == DiscountKind.SeniorPwd ? "SC/PWD VAT-exempt" : p.IsVatExempt ? "VAT-exempt" : "VAT-inclusive",
                LineDiscount = money.Discount, LineTotal = money.Total, LineNet = money.Net, LineVat = money.Vat
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
        rollback.Complete();
        return sale;
    }

    // ---------- Voiding (sales are never edited or deleted) ----------

    public void VoidSale(Guid saleId, Guid userId, string reason)
    {
        _db.RequireBusinessAuthorization();
        using var rollback = new PostingRollbackScope(_db);
        if (!_db.Users.Any(u => u.Id == userId && u.IsActive && UserAccess.AdministratorRoles.Contains(u.Role)))
            throw new InvalidOperationException("An active administrator must approve the void.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A void reason is required.", nameof(reason));

        using var tx = _db.Database.BeginTransaction();

        var sale = _db.Sales.Include(s => s.Items).First(s => s.Id == saleId);
        _db.Entry(sale).Reload();
        rollback.Refresh(sale);
        if (sale.IsVoided) throw new InvalidOperationException("This sale is already voided.");

        var original = _db.JournalEntries.Include(e => e.Lines)
            .First(e => e.SourceType == "Sale" && e.SourceId == sale.Id);

        // Reversing entry — every debit becomes a credit and vice versa
        var now = RetailForma.Core.BusinessClock.Now;
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
        var refundShift = new ShiftService(_db).GetOpenShift();
        sale.VoidCashShiftId = refundShift?.BranchId == sale.BranchId ? refundShift.Id : null;
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
        rollback.Complete();
    }

    // ---------- Stock corrections (physical count, damages) ----------

    public InventoryMovement AdjustStock(Guid productId, decimal qtyChange, Guid userId, string reason,
        string? positiveCounterpartCode = null, bool zeroCostConfirmed = false)
    {
        if (qtyChange == 0) throw new ArgumentException("Quantity change cannot be zero.", nameof(qtyChange));
        _db.RequireBusinessAuthorization();
        using var rollback = new PostingRollbackScope(_db);
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));
        AccountingPolicy.RequireAdmin(_db, userId);
        if (qtyChange > 0)
        {
            if (positiveCounterpartCode is not ("3000" or "3990"))
                throw new InvalidOperationException("A surplus needs investigated source evidence: use the paid receipt workflow for missed purchases, or select approved contribution/opening-clearing correction.");
            if (positiveCounterpartCode == "3000") AccountingPolicy.RequireProprietor(_db);
        }

        using var tx = _db.Database.BeginTransaction();

        var product = _db.Products.First(p => p.Id == productId);
        _db.Entry(product).Reload();
        rollback.Refresh(product);
        if (product.CostPrice < 0 || product.CostPrice == 0 && !zeroCostConfirmed)
            throw new InvalidOperationException("Confirm the carrying cost before adjusting stock; missing cost is not known zero.");
        if (product.StockQty + qtyChange < 0)
            throw new InvalidOperationException("Adjustment would make stock negative.");

        product.StockQty += qtyChange;

        var movement = new InventoryMovement
        {
            ProductId = productId, Type = MovementType.Adjustment, Qty = qtyChange, // signed
            UnitCost = product.CostPrice, MovementDate = RetailForma.Core.BusinessClock.Now,
            SourceType = "Adjustment", UserId = userId, Notes = reason
        };
        _db.InventoryMovements.Add(movement);

        var value = Round2(Math.Abs(qtyChange) * product.CostPrice);
        if (value != 0)
            PostJournal(RetailForma.Core.BusinessClock.Now, FirstBranchId(), "Adjustment", movement.Id, reason,
                (qtyChange > 0 ? AccountCodes.Inventory : AccountingPolicy.InventoryLoss, value, 0),
                (qtyChange > 0 ? positiveCounterpartCode! : AccountCodes.Inventory, 0, value));

        _db.AuditLogs.Add(new AuditLog
        {
            Date = RetailForma.Core.BusinessClock.Now, UserId = userId, Action = "AdjustStock", EntityName = "Product",
            EntityId = productId, Details = $"{qtyChange:+0.##;-0.##} — {reason}"
        });

        _db.SaveChanges();
        tx.Commit();
        rollback.Complete();
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
    private Guid FirstBranchId()
    {
        var branches = _db.Branches.Take(2).ToList();
        if (branches.Count != 1) throw new InvalidOperationException("Stock posting requires a single-store installation; multi-branch ownership is not implemented.");
        return branches[0].Id;
    }
    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private sealed class PreparedLine
    {
        public Product Product = null!;
        public decimal Qty;
        public decimal LineGross;     // price × qty, VAT-inclusive
    }
}
