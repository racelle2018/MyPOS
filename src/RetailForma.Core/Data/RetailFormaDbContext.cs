using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Entities;

namespace RetailForma.Core.Data;

public class RetailFormaDbContext : DbContext
{
    // Set by the desktop runtime; tests/API hosts supply their own authorization context.
    public Action? BusinessAuthorization { get; set; }
    public void RequireBusinessAuthorization() => BusinessAuthorization?.Invoke();
    private void AuthorizeBusinessChanges()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries().Any(e => (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) &&
            (e.Entity is Product or Sale or SaleItem or Payment or InventoryMovement or JournalEntry or JournalLine or CashShift or CashMovement or DenominationCount or HeldSale or Customer)))
            RequireBusinessAuthorization();
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    { AuthorizeBusinessChanges(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { AuthorizeBusinessChanges(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
    public RetailFormaDbContext(DbContextOptions<RetailFormaDbContext> options) : base(options) { }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CashShift> CashShifts => Set<CashShift>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<DenominationCount> DenominationCounts => Set<DenominationCount>();
    public DbSet<HeldSale> HeldSales => Set<HeldSale>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>(e => e.HasIndex(x => x.Username).IsUnique());

        mb.Entity<Product>(e => e.HasIndex(x => x.Barcode).IsUnique());

        mb.Entity<Sale>(e =>
        {
            e.HasIndex(x => new { x.BranchId, x.SaleNumber }).IsUnique(); // enforces gapless, no duplicates
            e.HasIndex(x => new { x.BranchId, x.ReceiptNumber })
                .IsUnique()
                .HasFilter("\"ReceiptNumber\" IS NOT NULL");
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SaleId);
            e.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.SaleId);
        });

        mb.Entity<InventoryMovement>(e => e.HasIndex(x => x.ProductId));

        mb.Entity<Account>(e => e.HasIndex(x => x.Code).IsUnique());

        mb.Entity<JournalEntry>(e =>
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.JournalEntryId));

        mb.Entity<JournalLine>(e => e.HasIndex(x => x.AccountId));

        mb.Entity<Setting>(e => e.HasKey(x => x.Key));
        mb.Entity<CashShift>(e => e.HasMany(x => x.Movements).WithOne().HasForeignKey(x => x.ShiftId));
        mb.Entity<CashShift>(e => e.HasMany(x => x.DenominationCounts).WithOne().HasForeignKey(x => x.ShiftId));
        mb.Entity<DenominationCount>(e => e.HasIndex(x => new { x.ShiftId, x.FaceValue }).IsUnique());
    }
}
