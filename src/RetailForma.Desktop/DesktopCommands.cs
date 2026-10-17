using RetailForma.Core.Entities;
using RetailForma.Core.Services;
using RetailForma.ServiceApplication;

namespace RetailForma.Desktop;

public sealed class DesktopShiftCommands(DesktopService service)
{
    private ShiftService? Local => service.TestDatabase is { } db ? new(db) : null;
    public CashShift? GetOpenShift() => Local is { } local ? local.GetOpenShift() : service.Call<CashShift?>(Operation.OpenDrawer);
    public bool HasOpenShift => GetOpenShift() != null;
    public bool CanUseDrawer(CashShift shift, Guid userId) => Local is { } local ? local.CanUseDrawer(shift, userId) : service.Call<bool>(Operation.CanUseDrawer, new IdInput(shift.Id));
    public void RequireDrawerAccess(CashShift shift, Guid userId)
    { if (Local is { } local) local.RequireDrawerAccess(shift, userId); else if (!CanUseDrawer(shift, userId)) throw new UnauthorizedAccessException("This drawer is assigned to another cashier."); }
    public CashShift OpenShift(Guid branchId, Guid userId, decimal openingFloat, string? floatCounterpartCode = null) => Local is { } local ? local.OpenShift(branchId, userId, openingFloat, floatCounterpartCode) :
        service.Call<CashShift>(Operation.OpenShift, new OpenShiftInput(branchId, openingFloat, floatCounterpartCode));
    public void ConfigureDrawer(Guid adminId, DrawerMode mode, Guid? assignedCashierId = null)
    { if (Local is { } local) local.ConfigureDrawer(adminId, mode, assignedCashierId); else service.Call(Operation.ConfigureDrawer, new DrawerInput(mode, assignedCashierId)); }
    public CashMovement RecordCashMovement(Guid userId, CashMovementType type, decimal amount, string reason,
        CashMovementCategory category = CashMovementCategory.Unclassified, string? reference = null, string? destination = null,
        string? expenseCategory = null, bool approve = false, bool companyOwnedDestination = false, string? counterpartAccountCode = null) =>
        Local is { } local ? local.RecordCashMovement(userId, type, amount, reason, category, reference, destination, expenseCategory, approve, companyOwnedDestination, counterpartAccountCode) :
        service.Call<CashMovement>(Operation.RecordCashMovement, new MovementInput(type, amount, reason, category, reference, destination, expenseCategory, approve, companyOwnedDestination, counterpartAccountCode));
    public void ApproveCashMovement(Guid movementId, Guid adminId)
    { if (Local is { } local) local.ApproveCashMovement(movementId, adminId); else service.Call(Operation.ApproveCashMovement, new IdInput(movementId)); }
    public decimal GetMinimumCountingDenomination() => Local is { } local ? local.GetMinimumCountingDenomination() : service.Call<decimal>(Operation.CountingMinimum);
    public decimal[] GetCountingDenominations() => Local is { } local ? local.GetCountingDenominations() : service.Call<decimal[]>(Operation.CountingDenominations);
    public void ConfigureCountingMinimum(Guid adminId, decimal minimum)
    { if (Local is { } local) local.ConfigureCountingMinimum(adminId, minimum); else service.Call(Operation.ConfigureCounting, new DecimalInput(minimum)); }
    public ShiftService.ShiftTotals GetShiftTotals(Guid shiftId) => Local is { } local ? local.GetShiftTotals(shiftId) : service.Call<ShiftService.ShiftTotals>(Operation.ShiftTotals, new IdInput(shiftId));
    public CashShift CloseShift(Guid userId, decimal countedDrawer, string? notes, IReadOnlyDictionary<decimal, int>? denominations = null,
        Guid? shiftId = null, ShiftService.ShiftTotals? reviewedTotals = null) => Local is { } local ? local.CloseShift(userId, countedDrawer, notes, denominations, shiftId, reviewedTotals) :
        service.Call<CashShift>(Operation.CloseShift, new CloseShiftInput(countedDrawer, notes, denominations?.ToDictionary(p => p.Key, p => p.Value), shiftId, reviewedTotals));
}

public sealed class DesktopSaleCommands(DesktopService service)
{
    private SaleService? Local => service.TestDatabase is { } db ? new(db) : null;
    public InventoryMovement ReceiveStock(Guid productId, decimal qty, decimal unitCost, Guid userId, string notes = "", string? paidFromAccountCode = null,
        StockReceiptKind receiptKind = StockReceiptKind.PaidPurchase, bool zeroCostConfirmed = false) => Local is { } local ? local.ReceiveStock(productId, qty, unitCost, userId, notes, paidFromAccountCode, receiptKind, zeroCostConfirmed) :
        service.Call<InventoryMovement>(Operation.ReceiveStock, new ReceiveInput(productId, qty, unitCost, notes, paidFromAccountCode, receiptKind, zeroCostConfirmed));
    public InventoryMovement AdjustStock(Guid productId, decimal qtyChange, Guid userId, string reason, string? positiveCounterpartCode = null, bool zeroCostConfirmed = false)
    {
        if (Local is { } local) return local.AdjustStock(productId, qtyChange, userId, reason, positiveCounterpartCode, zeroCostConfirmed);
        return service.Call<InventoryMovement>(Operation.AdjustStock, new AdjustInput(productId, qtyChange, reason, positiveCounterpartCode, zeroCostConfirmed));
    }
    public void VoidSale(Guid saleId, Guid userId, string reason)
    { if (Local is { } local) local.VoidSale(saleId, userId, reason); else service.Call(Operation.VoidSale, new ReasonInput(saleId, reason)); }
    public Sale PostSale(Guid branchId, Guid cashierId, IList<CartLine> lines, decimal discount = 0, decimal tendered = 0,
        PaymentMethod method = PaymentMethod.Cash, string? reference = null, ReceiptType receiptType = ReceiptType.None,
        string? manualReceiptNumber = null, string? customerName = null, string? customerAddress = null,
        OrderType orderType = OrderType.WalkIn, DiscountKind discountKind = DiscountKind.None, string? seniorIdNumber = null, decimal? expectedTotal = null) =>
        Local is { } local ? local.PostSale(branchId, cashierId, lines, discount, tendered, method, reference, receiptType, manualReceiptNumber, customerName, customerAddress, orderType, discountKind, seniorIdNumber, expectedTotal) :
        service.Call<Sale>(Operation.PostSale, new PostSaleInput(branchId, lines.ToList(), discount, tendered, method, reference, receiptType, manualReceiptNumber, customerName, customerAddress, orderType, discountKind, seniorIdNumber, expectedTotal));
}

public sealed class DesktopHeldCommands(DesktopService service)
{
    private HeldSaleService? Local => service.TestDatabase is { } db ? new(db) : null;
    public List<HeldSale> ListActive(Guid branchId) => Local is { } local ? local.ListActive(branchId) : service.Call<List<HeldSale>>(Operation.HeldSales, new IdInput(branchId));
    public HeldSale Hold(Guid branchId, Guid userId, List<HeldCartLine> cart, string? customerName, string? invoice, string? notes,
        string? customerAddress = null, PaymentMethod paymentMethod = PaymentMethod.Cash, OrderType orderType = OrderType.WalkIn,
        DiscountKind discountKind = DiscountKind.None, decimal discountAmount = 0, string? seniorIdNumber = null) =>
        Local is { } local ? local.Hold(branchId, userId, cart, customerName, invoice, notes, customerAddress, paymentMethod, orderType, discountKind, discountAmount, seniorIdNumber) :
        service.Call<HeldSale>(Operation.HoldSale, new HoldInput(branchId, cart, customerName, invoice, notes, customerAddress, paymentMethod, orderType, discountKind, discountAmount, seniorIdNumber));
    public List<HeldCartLine> ReadCart(Guid id) => Local is { } local ? local.ReadCart(id) : service.Call<List<HeldCartLine>>(Operation.ReadHeldCart, new IdInput(id));
    public void CompleteRecall(Guid id, Guid userId)
    { if (Local is { } local) local.CompleteRecall(id, userId); else service.Call(Operation.CompleteRecall, new IdInput(id)); }
    public void Discard(Guid id, Guid userId)
    { if (Local is { } local) local.Discard(id, userId); else service.Call(Operation.DiscardHeld, new IdInput(id)); }
}
