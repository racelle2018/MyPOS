using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.Core.Services;
using RetailForma.LocalTransport;
using RetailForma.ServiceApplication;

namespace RetailForma.Desktop;

/// <summary>Typed client operations; never returns a database connection, key or persisted password hash.
/// The test-only adapter requires the explicit isolated WPF test host and its already supplied in-memory DB.</summary>
public sealed class DesktopService : IDisposable
{
    private readonly LocalApplicationClient client = new();
    public DesktopShiftCommands Drawer => new(this);
    public DesktopSaleCommands Sales => new(this);
    public DesktopHeldCommands Held => new(this);
    public User? StaffMember(Guid? id) => id.HasValue ? Staff().FirstOrDefault(u => u.Id == id) : null;
    internal RetailFormaDbContext? TestDatabase => System.Windows.Application.Current is App { IsolatedTestHost: true } ? App.Db : null;
    public T Call<T>(Operation operation, object? input = null) => client.Call<T>(operation, input);
    public void Call(Operation operation, object? input = null) => client.Call(operation, input);
    public List<Product> Products() => TestDatabase?.Products.AsNoTracking().ToList() ?? Call<List<Product>>(Operation.Products);
    public List<Branch> Branches() => TestDatabase?.Branches.AsNoTracking().ToList() ?? Call<List<Branch>>(Operation.Branches);
    public List<Customer> Customers() => TestDatabase?.Customers.AsNoTracking().ToList() ?? Call<List<Customer>>(Operation.Customers);
    public List<Account> Accounts() => TestDatabase?.Accounts.AsNoTracking().ToList() ?? Call<List<Account>>(Operation.Accounts);
    public List<User> Staff() => TestDatabase?.Users.ToList() ?? Call<List<ClientUser>>(Operation.Staff).Select(UserView).ToList();
    public List<CashShift> Shifts() => TestDatabase?.CashShifts.ToList() ?? Call<List<CashShift>>(Operation.Shifts);
    public List<string?> HistoricalCategories() => TestDatabase?.SaleItems.Select(i => i.CategorySnapshot).Distinct().ToList() ?? Call<List<string?>>(Operation.HistoricalCategories);
    public List<AuditLog> AuditLog(DateTime from, DateTime to) => TestDatabase?.AuditLogs.Where(a => a.Date >= from && a.Date < to).OrderByDescending(a => a.Date).ToList()
        ?? Call<List<AuditLog>>(Operation.AuditLog, new AuditInput(from, to));
    public AuditChoices AuditChoices()
    {
        if (TestDatabase is not { } db) return Call<AuditChoices>(Operation.AuditChoices);
        var users = db.Users.ToDictionary(u => u.Id, u => u.Username);
        return new(db.AuditLogs.Select(a => a.Action).Distinct().Order().ToArray(), db.AuditLogs.ToList().Where(a => a.UserId.HasValue)
            .Select(a => users.GetValueOrDefault(a.UserId!.Value)).Where(u => u != null).Cast<string>().Distinct().Order().ToArray());
    }
    internal static User UserView(ClientUser user) => new() { Id = user.Id, Username = user.Username, FullName = user.FullName,
        Role = user.Role, IsActive = user.IsActive, LastLoginAt = user.LastLoginAt, PasswordHash = "" };
    public Dictionary<string, string> Settings(bool beforeLogin = false) => TestDatabase?.Settings.ToDictionary(s => s.Key, s => s.Value)
        ?? Call<Dictionary<string, string>>(beforeLogin ? Operation.PublicIdentity : Operation.Settings);
    public bool InvoiceExists(Guid branch, string invoice) => TestDatabase?.Sales.ToList().Any(s => s.BranchId == branch && !s.IsVoided && string.Equals(s.ReceiptNumber, invoice, StringComparison.OrdinalIgnoreCase))
        ?? Call<bool>(Operation.InvoiceExists, new InvoiceInput(branch, invoice));
    public DailySalesReport DailySales(DateTime date, Guid branch, Guid? cashier = null, PaymentMethod? payment = null, string search = "") =>
        TestDatabase is { } db ? new ReportService(db).GetDailySales(date, branch, cashier, payment, search) :
        Call<DailySalesReport>(Operation.DailySales, new DailyInput(date, branch, cashier, payment, search));
    public ReportTableData ProductSales(ReportScope scope, bool summary = false) => TestDatabase is { } db ? new ExpandedReportService(db).ProductSales(scope, summary) : Call<ReportTableData>(Operation.ProductSales, new ScopeInput(scope, summary));
    public ReportTableData SalesSummary(ReportScope scope, bool summary = false) => TestDatabase is { } db ? new ExpandedReportService(db).SalesSummary(scope, summary) : Call<ReportTableData>(Operation.SalesSummary, new ScopeInput(scope, summary));
    public ReportTableData VoidEvents(ReportScope scope) => TestDatabase is { } db ? new ExpandedReportService(db).VoidEvents(scope) : Call<ReportTableData>(Operation.VoidEvents, new ScopeInput(scope));
    public void Validate(ReportScope scope, bool adminOnly = false)
    { if (TestDatabase is { } db) new ExpandedReportService(db).Validate(scope, adminOnly); else Call(Operation.ValidateScope, new ScopeInput(scope, AdminOnly: adminOnly)); }
    public string Describe(ReportScope scope) => TestDatabase is { } db ? new ExpandedReportService(db).Describe(scope) : Call<string>(Operation.DescribeScope, new ScopeInput(scope));
    public ShiftReconciliationReport ShiftReport(Guid shift, Guid actor) => TestDatabase is { } db ? new ShiftReportService(db).GetReport(shift, actor) : Call<ShiftReconciliationReport>(Operation.ShiftReport, new IdInput(shift));
    public CashMovementReport CashReport(Guid branch, Guid actor, DateOnly from, DateOnly to, CashMovementType? type = null,
        CashMovementCategory? category = null, string search = "", Guid? shiftId = null) => TestDatabase is { } db ? new CashMovementReportService(db).GetReport(branch, actor, from, to, type, category, search, shiftId) :
        Call<CashMovementReport>(Operation.CashMovementReport, new CashReportInput(branch, from, to, type, category, search, shiftId));
    public void Dispose() => client.Dispose();
    public void EndSession()
    {
        if (TestDatabase != null) return;
        try { Call(Operation.Logout); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException) { }
        finally { client.Dispose(); } // Closing the OS pipe revokes its server-side session even if logout cannot reply.
    }
    public void SaveSettings(Dictionary<string, string> values)
    {
        if (TestDatabase is not { } db) { Call(Operation.SaveSettings, new SettingsInput(values)); return; }
        foreach (var (key, value) in values) AppSettings.Set(key, value);
        db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, UserId = App.CurrentUser?.Id,
            Action = values.ContainsKey("SetupChecklistReviewed") ? "SetupChecklistReviewed" : "SettingChanged", Details = string.Join(", ", values.Keys) }); db.SaveChanges();
    }
    public AppearancePreferences Appearance()
    {
        if (TestDatabase is { } db) return new AppearanceSettingsService(db).Load();
        var values = Settings(App.CurrentUser == null);
        return new AppearancePreferences(values.GetValueOrDefault("Appearance.Font", "Default"), values.GetValueOrDefault("Appearance.TextSize", "Standard"), values.GetValueOrDefault("Appearance.TableSpacing", "Compact")).Safe();
    }
    public void SaveAppearance(AppearancePreferences preferences)
    { if (TestDatabase is { } db) new AppearanceSettingsService(db).Save(App.CurrentUser!.Id, preferences); else Call(Operation.SaveAppearance, preferences); }
    public void ClientEvent(string action, string details, Guid? entity = null)
    {
        if (TestDatabase is { } db)
        { db.AuditLogs.Add(new AuditLog { Date = RetailForma.Core.BusinessClock.Now, UserId = App.CurrentUser?.Id, Action = action, EntityId = entity, Details = details }); db.SaveChanges(); }
        else Call(Operation.RecordClientEvent, new ClientEventInput(action, details.Length > 200 ? details[..200] : details));
    }
    public Product SaveProduct(ProductInput input)
    {
        if (TestDatabase is not { } db) return Call<Product>(Operation.SaveProduct, input);
        var product = input.Id.HasValue ? db.Products.First(p => p.Id == input.Id) : new Product();
        if (!input.Id.HasValue) db.Products.Add(product);
        product.Name = input.Name; product.Barcode = input.Barcode; product.Category = input.Category; product.Unit = input.Unit;
        product.Price = input.Price; product.IsVatExempt = input.IsVatExempt; if (input.CostPrice.HasValue) product.CostPrice = input.CostPrice.Value;
        ClientEvent(input.Id.HasValue ? "ProductEdit" : "ProductCreate", product.Name, product.Id); return product;
    }
    public void ActivateProduct(Guid id, bool active)
    {
        if (TestDatabase is not { } db) { Call(Operation.ActivateProduct, new ToggleInput(id, active)); return; }
        var product = db.Products.First(p => p.Id == id); product.IsActive = active;
        ClientEvent(active ? "ProductActivate" : "ProductDeactivate", product.Name, id);
    }
    public void ActivateUser(Guid id, bool active)
    {
        if (TestDatabase is not { } db) { Call(Operation.ActivateUser, new ToggleInput(id, active)); return; }
        var user = db.Users.Find(id)!; user.IsActive = active; ClientEvent(active ? "UserActivate" : "UserDeactivate", user.Username, id);
    }
    public User SaveUser(UserInput input)
    {
        if (TestDatabase is not { } db) return UserView(Call<ClientUser>(Operation.SaveUser, input));
        var user = input.Id.HasValue ? db.Users.Find(input.Id)! : new User(); if (!input.Id.HasValue) db.Users.Add(user);
        user.Username = input.Username; user.FullName = input.FullName; user.Role = input.Role; user.IsActive = input.Active;
        if (!string.IsNullOrEmpty(input.Password)) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(input.Password);
        ClientEvent(input.Id.HasValue ? "UserEdit" : "UserCreate", user.Username, user.Id); return user;
    }
}
