using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;
using System.Security.Cryptography;
using System.Text.Json;

namespace RetailForma.ServiceApplication;

/// <summary>Authoritative operation allowlist. The host supplies the DB, license guard and real Windows session.</summary>
public sealed class ApplicationOperations(Func<RetailFormaDbContext> openDatabase, Action requireLicense, TimeProvider? time = null)
{
    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private readonly Dictionary<string, (int Count, long Since)> attempts = new(StringComparer.Ordinal);
    private readonly object loginGate = new();
    private Guid authorityEpoch = Guid.NewGuid();
    public void InvalidateAllSessions() => authorityEpoch = Guid.NewGuid();
    private static readonly Lazy<string> DummyHash = new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), 12));
    private static readonly HashSet<string> PublicSettings = new(StringComparer.Ordinal)
    { "CompanyName", "CompanyAddress", "BranchName", "CompanyTin", "Appearance.Font", "Appearance.TextSize", "Appearance.TableSpacing" };
    private static readonly HashSet<string> EditableSettings = new(StringComparer.Ordinal)
    { "CompanyName", "CompanyAddress", "CompanyTin", "BranchName", "CompanyLegalForm", "AccrNo", "Min", "Sn", "ReceiptIssuanceEnabled",
        "ReceiptWidth", "ReceiptPrintMode", "ReceiptPrinterName", "LowStockThreshold", "ReceiptShowQr", "FooterMessage",
        "SetupChecklistReviewed", "SetupCheck.Hardware", "SetupCheck.Accounting", "SetupCheck.Backup", "SetupCheck.External",
        "SetupCheck.Store", "SetupCheck.Security", "SetupCheck.Business", "SetupCheck.Users", "SetupCheck.Printing", "SetupCheck.Inventory",
        "BackupCopyFolder", "Backup.GoogleDrive.Enabled", "Backup.Dropbox.Enabled", "Backup.GoogleDrive.Folder", "Backup.Dropbox.Folder" };
    private static string ResetFlag(Guid id) => "Security.PasswordChangeRequired." + id.ToString("N");

    public object? Execute(ApplicationSession session, Operation operation, JsonElement input)
    {
        using var db = openDatabase();
        db.BusinessAuthorization = requireLicense;
        if (operation == Operation.PublicIdentity)
            return db.Settings.AsNoTracking().Where(s => PublicSettings.Contains(s.Key)).ToDictionary(s => s.Key, s => s.Value);
        if (operation == Operation.Logout) { session.Clear(); return null; }
        if (operation == Operation.Login) return Login(db, session, WireJson.Read<LoginInput>(input));
        var actor = RequireActor(db, session, allowLocked: operation == Operation.Unlock);
        if (operation == Operation.Lock) { session.Locked = true; return null; }
        if (operation == Operation.Unlock)
        {
            var password = WireJson.Read<PasswordInput>(input).Password;
            ValidateText(password, 256);
            CheckRate(session.WindowsSid);
            if (!BCrypt.Net.BCrypt.Verify(password, actor.PasswordHash))
            {
                FailedAttempt(session.WindowsSid); Audit(db, actor.Id, "UnlockFailed", "Session unlock rejected.");
                throw new UnauthorizedAccessException("Incorrect password. Please try again.");
            }
            ClearAttempts(session.WindowsSid); session.Locked = false; session.LastActivity = clock.GetTimestamp(); return View(db, actor);
        }
        if (operation == Operation.ChangePassword) return ChangePassword(db, session, actor, WireJson.Read<ChangePasswordInput>(input));
        if (MustChange(db, actor)) throw new UnauthorizedAccessException("Change the initial or recovery password before continuing.");
        var admin = UserAccess.IsAdministrator(actor);
        void Admin() { if (!admin) { Audit(db, actor.Id, "PermissionDenied", "Service denied " + operation); throw new UnauthorizedAccessException("An active Owner or System Administrator is required."); } }
        var shifts = new ShiftService(db);
        switch (operation)
        {
            case Operation.CurrentUser: return View(db, actor);
            case Operation.Products:
                var products = db.Products.AsNoTracking().Where(p => admin || p.IsActive).OrderBy(p => p.Id).ToList();
                if (!admin) foreach (var p in products) p.CostPrice = 0;
                return products;
            case Operation.Customers: return db.Customers.AsNoTracking().OrderBy(c => c.Name).ToList();
            case Operation.Branches: return db.Branches.AsNoTracking().OrderBy(b => b.CreatedAt).ToList();
            case Operation.Staff: return db.Users.AsNoTracking().OrderBy(u => u.FullName).ToList().Select(u => View(db, u)).ToList();
            case Operation.Accounts: return db.Accounts.AsNoTracking().OrderBy(a => a.Code).ToList();
            case Operation.HistoricalCategories: return db.SaleItems.AsNoTracking().Select(i => i.CategorySnapshot).Distinct().ToList();
            case Operation.AuditChoices:
                Admin(); return new AuditChoices(db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToArray(),
                    db.Users.Where(u => db.AuditLogs.Any(a => a.UserId == u.Id)).Select(u => u.Username).Distinct().OrderBy(u => u).ToArray());
            case Operation.Settings:
                return db.Settings.AsNoTracking().Where(s => !s.Key.StartsWith("Security.") && s.Key != "BackupRecoverySecret").ToDictionary(s => s.Key, s => s.Value);
            case Operation.Shifts:
                return db.CashShifts.AsNoTracking().Include(s => s.Movements).Include(s => s.DenominationCounts)
                    .Where(s => admin || s.DrawerMode == DrawerMode.Shared || s.AssignedCashierId == actor.Id).OrderByDescending(s => s.OpenedAt).ToList();
            case Operation.AuditLog:
                Admin(); var range = WireJson.Read<AuditInput>(input);
                if (range.To <= range.From || (range.To - range.From).TotalDays > 366) throw new ArgumentException("Choose an audit range of at most 366 days.");
                return db.AuditLogs.AsNoTracking().Where(a => a.Date >= range.From && a.Date < range.To).OrderByDescending(a => a.Date).ToList();
            case Operation.SaveProduct: Admin(); requireLicense(); return SaveProduct(db, actor, WireJson.Read<ProductInput>(input));
            case Operation.ActivateProduct:
                Admin(); requireLicense(); var toggle = WireJson.Read<ToggleInput>(input);
                var product = db.Products.Find(toggle.Id) ?? throw new ArgumentException("Product not found."); product.IsActive = toggle.Active;
                Audit(db, actor.Id, toggle.Active ? "ProductActivate" : "ProductDeactivate", product.Id.ToString()); return null;
            case Operation.SaveUser: Admin(); return SaveUser(db, actor, WireJson.Read<UserInput>(input));
            case Operation.ActivateUser:
                Admin(); var userToggle = WireJson.Read<ToggleInput>(input); var target = db.Users.Find(userToggle.Id) ?? throw new ArgumentException("User not found.");
                if (target.Id == actor.Id || !UserAccess.CanManageUser(actor, target)) throw new UnauthorizedAccessException("You cannot change this account's access.");
                target.IsActive = userToggle.Active; Audit(db, actor.Id, target.IsActive ? "UserActivate" : "UserDeactivate", target.Id.ToString()); return null;
            case Operation.SaveSettings: Admin(); SaveSettings(db, actor.Id, WireJson.Read<SettingsInput>(input)); return null;
            case Operation.SaveAppearance: Admin(); new AppearanceSettingsService(db).Save(actor.Id, WireJson.Read<AppearancePreferences>(input)); return null;
            case Operation.OpenDrawer:
                var currentDrawer = shifts.GetOpenShift();
                return currentDrawer == null || shifts.CanUseDrawer(currentDrawer, actor.Id) ? currentDrawer :
                    new CashShift { Id = currentDrawer.Id, BranchId = currentDrawer.BranchId, UserId = currentDrawer.UserId,
                        AssignedCashierId = currentDrawer.AssignedCashierId, DrawerMode = currentDrawer.DrawerMode, OpenedAt = currentDrawer.OpenedAt };
            case Operation.CanUseDrawer: var drawer = db.CashShifts.Find(WireJson.Read<IdInput>(input).Id); return drawer != null && shifts.CanUseDrawer(drawer, actor.Id);
            case Operation.OpenShift: var opening = WireJson.Read<OpenShiftInput>(input); return shifts.OpenShift(opening.BranchId, actor.Id, opening.OpeningFloat, opening.Counterpart);
            case Operation.ShiftTotals: var shiftId = WireJson.Read<IdInput>(input).Id; RequireShift(db, shifts, actor.Id, shiftId); return shifts.GetShiftTotals(shiftId);
            case Operation.CloseShift: var closing = WireJson.Read<CloseShiftInput>(input); return shifts.CloseShift(actor.Id, closing.Counted, closing.Notes, closing.Denominations, closing.ShiftId, closing.ReviewedTotals);
            case Operation.ConfigureDrawer: Admin(); var configuration = WireJson.Read<DrawerInput>(input); shifts.ConfigureDrawer(actor.Id, configuration.Mode, configuration.AssignedCashierId); return null;
            case Operation.CountingMinimum: return shifts.GetMinimumCountingDenomination();
            case Operation.CountingDenominations: return shifts.GetCountingDenominations();
            case Operation.ConfigureCounting: Admin(); shifts.ConfigureCountingMinimum(actor.Id, WireJson.Read<DecimalInput>(input).Value); return null;
            case Operation.RecordCashMovement:
                var movement = WireJson.Read<MovementInput>(input);
                return shifts.RecordCashMovement(actor.Id, movement.Type, movement.Amount, movement.Reason, movement.Category, movement.Reference,
                    movement.Destination, movement.ExpenseCategory, movement.Approve, movement.CompanyOwnedDestination, movement.Counterpart);
            case Operation.ApproveCashMovement: Admin(); shifts.ApproveCashMovement(WireJson.Read<IdInput>(input).Id, actor.Id); return null;
            case Operation.PostSale:
                var sale = WireJson.Read<PostSaleInput>(input);
                if (sale.Lines.Count > 1000) throw new ArgumentException("The cart exceeds the supported size.");
                return SanitizeSale(new SaleService(db).PostSale(sale.BranchId, actor.Id, sale.Lines, sale.Discount, sale.Tendered, sale.Method,
                    sale.Reference, sale.ReceiptType, sale.ManualReceiptNumber, sale.CustomerName, sale.CustomerAddress, sale.OrderType,
                    sale.DiscountKind, sale.SeniorIdNumber, sale.ExpectedTotal), admin);
            case Operation.VoidSale: Admin(); var voiding = WireJson.Read<ReasonInput>(input); new SaleService(db).VoidSale(voiding.Id, actor.Id, voiding.Reason); return null;
            case Operation.ReceiveStock: var receiving = WireJson.Read<ReceiveInput>(input); return new SaleService(db).ReceiveStock(receiving.ProductId, receiving.Quantity,
                receiving.UnitCost, actor.Id, receiving.Notes, receiving.PaidFromAccount, receiving.ReceiptKind, receiving.ZeroCostConfirmed);
            case Operation.AdjustStock: Admin(); var adjustment = WireJson.Read<AdjustInput>(input); return new SaleService(db).AdjustStock(adjustment.ProductId, adjustment.Delta, actor.Id, adjustment.Reason, adjustment.Counterpart, adjustment.ZeroCostConfirmed);
            case Operation.HeldSales:
                var held = new HeldSaleService(db).ListActive(WireJson.Read<IdInput>(input).Id);
                // Cart JSON contains historical unit costs. Supply the safe cart through ReadHeldCart instead.
                foreach (var h in held) h.CartJson = "[]";
                return held;
            case Operation.HoldSale:
                var hold = WireJson.Read<HoldInput>(input); if (hold.Cart.Count > 1000) throw new ArgumentException("The cart exceeds the supported size.");
                return new HeldSaleService(db).Hold(hold.BranchId, actor.Id, hold.Cart, hold.CustomerName, hold.Invoice, hold.Notes,
                    hold.CustomerAddress, hold.PaymentMethod, hold.OrderType, hold.DiscountKind, hold.DiscountAmount, hold.SeniorIdNumber);
            case Operation.ReadHeldCart:
                return new HeldSaleService(db).ReadCart(WireJson.Read<IdInput>(input).Id).Select(c => admin ? c : c with { UnitCost = 0 }).ToList();
            case Operation.CompleteRecall: new HeldSaleService(db).CompleteRecall(WireJson.Read<IdInput>(input).Id, actor.Id); return null;
            case Operation.DiscardHeld: new HeldSaleService(db).Discard(WireJson.Read<IdInput>(input).Id, actor.Id); return null;
            case Operation.InvoiceExists: var invoice = WireJson.Read<InvoiceInput>(input); return db.Sales.AsNoTracking().ToList().Any(s => s.BranchId == invoice.BranchId && !s.IsVoided && string.Equals(s.ReceiptNumber, invoice.Invoice.Trim(), StringComparison.OrdinalIgnoreCase));
            case Operation.DailySales:
                var day = WireJson.Read<DailyInput>(input);
                ValidateText(day.Search, 200);
                if (!db.Branches.Any(b => b.Id == day.BranchId) || day.Date.Date > RetailForma.Core.BusinessClock.Today || day.Payment.HasValue && !Enum.IsDefined(day.Payment.Value))
                    throw new ArgumentException("Choose a valid branch, report date and payment filter.");
                var daily = new ReportService(db).GetDailySales(day.Date, day.BranchId, day.CashierId, day.Payment, day.Search);
                foreach (var row in daily.Sales) SanitizeSale(row, admin); return daily;
            case Operation.ValidateScope: var validation = WireJson.Read<ScopeInput>(input); new ExpandedReportService(db).Validate(validation.Scope with { UserId = actor.Id }, validation.AdminOnly); return null;
            case Operation.DescribeScope: return new ExpandedReportService(db).Describe(WireJson.Read<ScopeInput>(input).Scope with { UserId = actor.Id });
            case Operation.ProductSales: var productScope = WireJson.Read<ScopeInput>(input); return new ExpandedReportService(db).ProductSales(productScope.Scope with { UserId = actor.Id }, productScope.Summary);
            case Operation.SalesSummary: var summaryScope = WireJson.Read<ScopeInput>(input); return new ExpandedReportService(db).SalesSummary(summaryScope.Scope with { UserId = actor.Id }, summaryScope.Summary);
            case Operation.VoidEvents: Admin(); return new ExpandedReportService(db).VoidEvents(WireJson.Read<ScopeInput>(input).Scope with { UserId = actor.Id });
            case Operation.ShiftReport: return new ShiftReportService(db).GetReport(WireJson.Read<IdInput>(input).Id, actor.Id);
            case Operation.CashMovementReport: var cashScope = WireJson.Read<CashReportInput>(input); return new CashMovementReportService(db).GetReport(cashScope.BranchId, actor.Id, cashScope.From, cashScope.To, cashScope.Type, cashScope.Category, cashScope.Search, cashScope.ShiftId);
            case Operation.RecordClientEvent:
                var clientEvent = WireJson.Read<ClientEventInput>(input);
                if (clientEvent.Event is not ("PermissionDenied" or "ReceiptReprint" or "ReportPrinted")) throw new UnauthorizedAccessException("Unsupported client event.");
                ValidateText(clientEvent.Detail, 200); Audit(db, actor.Id, "Client" + clientEvent.Event, clientEvent.Detail); return null;
            default: throw new UnauthorizedAccessException("Unsupported service operation.");
        }
    }

    private User RequireActor(RetailFormaDbContext db, ApplicationSession session, bool allowLocked)
    {
        var actor = session.UserId.HasValue ? db.Users.AsNoTracking().SingleOrDefault(u => u.Id == session.UserId) : null;
        if (actor == null || !actor.IsActive || session.AuthorityEpoch != authorityEpoch || !session.Matches(actor)) { session.Clear(); throw new UnauthorizedAccessException("Sign in again."); }
        if (clock.GetElapsedTime(session.LastActivity) >= TimeSpan.FromMinutes(30)) session.Locked = true;
        if (session.Locked && !allowLocked) throw new UnauthorizedAccessException("Unlock the session to continue.");
        session.LastActivity = clock.GetTimestamp(); return actor;
    }
    private ClientUser Login(RetailFormaDbContext db, ApplicationSession session, LoginInput input)
    {
        session.Clear(); ValidateText(input.Username, 128); ValidateText(input.Password, 256); CheckRate(session.WindowsSid);
        var user = db.Users.AsNoTracking().ToList().FirstOrDefault(u => string.Equals(u.Username, input.Username.Trim(), StringComparison.OrdinalIgnoreCase));
        var valid = BCrypt.Net.BCrypt.Verify(input.Password, user?.PasswordHash ?? DummyHash.Value);
        if (!valid || user is not { IsActive: true })
        { FailedAttempt(session.WindowsSid); Audit(db, null, "LoginFailed", "Credentials rejected."); throw new UnauthorizedAccessException("Invalid username or password."); }
        ClearAttempts(session.WindowsSid); var persisted = db.Users.Find(user.Id)!; persisted.LastLoginAt = RetailForma.Core.BusinessClock.Now;
        Audit(db, persisted.Id, "Login", "Authenticated through local service."); session.SignIn(persisted, clock, authorityEpoch); return View(db, persisted);
    }
    private ClientUser ChangePassword(RetailFormaDbContext db, ApplicationSession session, User actor, ChangePasswordInput change)
    {
        ValidateText(change.Current, 256); ValidatePassword(change.Next);
        if (change.Next != change.Confirmation || change.Next == change.Current || !BCrypt.Net.BCrypt.Verify(change.Current, actor.PasswordHash))
            throw new ArgumentException("Verify the current password and enter a different matching password.");
        var target = db.Users.Find(actor.Id)!; target.PasswordHash = BCrypt.Net.BCrypt.HashPassword(change.Next, 12);
        var flag = db.Settings.Find(ResetFlag(actor.Id)); if (flag != null) db.Settings.Remove(flag);
        Audit(db, actor.Id, "PasswordChanged", "Password changed; other service sessions invalidated."); session.SignIn(target, clock, authorityEpoch); return View(db, target);
    }
    private static bool MustChange(RetailFormaDbContext db, User user) => db.Settings.Any(s => s.Key == ResetFlag(user.Id) && s.Value == "true") ||
        user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase) && BCrypt.Net.BCrypt.Verify("admin123", user.PasswordHash);
    private static ClientUser View(RetailFormaDbContext db, User user) => new(user.Id, user.Username, user.FullName, user.Role, user.IsActive, user.LastLoginAt, MustChange(db, user));
    private static Sale SanitizeSale(Sale sale, bool admin)
    { if (!admin) foreach (var item in sale.Items) item.UnitCost = 0; return sale; }
    private static void RequireShift(RetailFormaDbContext db, ShiftService shifts, Guid actor, Guid id) =>
        shifts.RequireDrawerAccess(db.CashShifts.Find(id) ?? throw new ArgumentException("Shift not found."), actor);
    private static void ValidateText(string? value, int maximum)
    { if (value == null || value.Length > maximum || value.Contains('\0')) throw new ArgumentException("Invalid or oversized input."); }
    private static void ValidatePassword(string password)
    { if (string.IsNullOrWhiteSpace(password) || password.Length is < 16 or > 256 || password == "admin123") throw new ArgumentException("Use an individual password/passphrase of 16–256 characters."); }
    private static Product SaveProduct(RetailFormaDbContext db, User actor, ProductInput input)
    {
        ValidateText(input.Name, 200); ValidateText(input.Barcode ?? "", 128); ValidateText(input.Unit, 64); ValidateText(input.Category, 128);
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Unit) || input.Price < 0 || decimal.Round(input.Price, 2) != input.Price)
            throw new ArgumentException("Enter a product name, unit and valid price.");
        var barcode = string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim().ToUpperInvariant();
        if (barcode != null && db.Products.AsNoTracking().ToList().Any(p => p.Id != input.Id && string.Equals(p.Barcode, barcode, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("That barcode is already used.");
        var product = input.Id.HasValue ? db.Products.Find(input.Id.Value) ?? throw new ArgumentException("Product not found.") : new Product();
        if (!input.Id.HasValue) db.Products.Add(product);
        product.Name = input.Name.Trim().ToUpperInvariant(); product.Barcode = barcode; product.Category = input.Category.Trim().ToUpperInvariant();
        product.Unit = input.Unit.Trim().ToUpperInvariant(); product.Price = input.Price; product.IsVatExempt = input.IsVatExempt;
        if (input.CostPrice.HasValue)
        {
            if (input.CostPrice.Value < 0) throw new ArgumentException("Cost price cannot be negative.");
            product.CostPrice = input.CostPrice.Value;
        }
        Audit(db, actor.Id, input.Id.HasValue ? "ProductEdit" : "ProductCreate", product.Id.ToString()); return product;
    }
    private static ClientUser SaveUser(RetailFormaDbContext db, User actor, UserInput input)
    {
        ValidateText(input.Username, 128); ValidateText(input.FullName, 200);
        var name = input.Username.Trim();
        if (name.Length < 3 || !name.All(char.IsLetterOrDigit) || string.IsNullOrWhiteSpace(input.FullName) || !Enum.IsDefined(input.Role)) throw new ArgumentException("Enter a valid individual name, username and role.");
        if (input.Role == UserRole.SystemAdministrator && actor.Role != UserRole.SystemAdministrator) throw new UnauthorizedAccessException("Only a System Administrator can grant technical access.");
        if (!input.Id.HasValue && input.Role == UserRole.SystemAdministrator) throw new UnauthorizedAccessException("Technical accounts require the separate provisioning workflow.");
        if (db.Users.AsNoTracking().ToList().Any(u => u.Id != input.Id && string.Equals(u.Username, name, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Username already exists.");
        var user = input.Id.HasValue ? db.Users.Find(input.Id.Value) ?? throw new ArgumentException("User not found.") : new User();
        if (!UserAccess.CanManageUser(actor, input.Id.HasValue ? user : null) || user.Id == actor.Id && (input.Role != actor.Role || !input.Active)) throw new UnauthorizedAccessException("You cannot change this account's access.");
        if (!input.Id.HasValue || !string.IsNullOrEmpty(input.Password)) { ValidatePassword(input.Password ?? ""); user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(input.Password!, 12); }
        user.Username = name; user.FullName = input.FullName.Trim(); user.Role = input.Role; user.IsActive = input.Active;
        if (!input.Id.HasValue) db.Users.Add(user);
        Audit(db, actor.Id, input.Id.HasValue ? "UserEdit" : "UserCreate", user.Id.ToString()); return View(db, user);
    }
    private static void SaveSettings(RetailFormaDbContext db, Guid actor, SettingsInput input)
    {
        if (input.Values.Count > 32 || input.Values.Keys.Any(k => !EditableSettings.Contains(k))) throw new UnauthorizedAccessException("Unsupported settings change.");
        foreach (var (key, value) in input.Values)
        {
            ValidateText(value, 2000);
            if (key == "LowStockThreshold" && (!int.TryParse(value, out var minimum) || minimum < 0)) throw new ArgumentException("Invalid low-stock threshold.");
            if (key.StartsWith("Backup.") && key.EndsWith(".Enabled") && value is not ("true" or "false")) throw new ArgumentException("Invalid backup provider switch.");
        }
        using var tx = db.Database.BeginTransaction();
        foreach (var (key, value) in input.Values)
        { var row = db.Settings.Find(key); if (row == null) db.Settings.Add(new Setting { Key = key, Value = value }); else row.Value = value; }
        Audit(db, actor, input.Values.ContainsKey("SetupChecklistReviewed") ? "SetupChecklistReviewed" : "SettingChanged", string.Join(", ", input.Values.Keys.Order())); tx.Commit();
    }
    private static void Audit(RetailFormaDbContext db, Guid? actor, string action, string detail)
    { db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, UserId = actor, Action = action, Details = detail }); db.SaveChanges(); }
    private void CheckRate(string peer)
    {
        lock (loginGate)
        {
            if (attempts.TryGetValue(peer, out var state) && state.Count >= 5 && clock.GetElapsedTime(state.Since) < TimeSpan.FromMinutes(1))
                throw new UnauthorizedAccessException("Too many attempts. Wait one minute before trying again.");
        }
    }
    private void FailedAttempt(string peer)
    {
        lock (loginGate)
        {
            foreach (var stale in attempts.Where(a => clock.GetElapsedTime(a.Value.Since) >= TimeSpan.FromMinutes(1)).Select(a => a.Key).ToArray()) attempts.Remove(stale);
            if (!attempts.ContainsKey(peer) && attempts.Count >= 1024) throw new UnauthorizedAccessException("Authentication is busy; try later.");
            attempts[peer] = attempts.TryGetValue(peer, out var state) ? (state.Count + 1, state.Since) : (1, clock.GetTimestamp());
        }
    }
    private void ClearAttempts(string peer) { lock (loginGate) attempts.Remove(peer); }
}
