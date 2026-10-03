using Microsoft.EntityFrameworkCore;
using MyPos.Core.Entities;

namespace MyPos.Core.Data;

public class MyPosDbContext : DbContext
{
    public MyPosDbContext(DbContextOptions<MyPosDbContext> options) : base(options) { }

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
    }
}
