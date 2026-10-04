using System.IO;
using Microsoft.EntityFrameworkCore;
using MyPos.Core.Data;
using MyPos.Core.Entities;
using MyPos.Desktop;
using MyPos.Desktop.Printing;
using Xunit;

namespace MyPos.Desktop.Tests;

public class BackupAndReceiptTests
{
    [Fact]
    public void Verified_backup_contains_database_rows_and_rejects_corruption()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mypos-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var database = Path.Combine(folder, "mypos.db");
            var backup = Path.Combine(folder, "backup.db");
            var options = new DbContextOptionsBuilder<MyPosDbContext>()
                .UseSqlite($"Data Source={database};Pooling=False").Options;
            using (var db = new MyPosDbContext(options))
            {
                DbInitializer.Initialize(db);
                db.Products.Add(new Product { Name = "Backup test", Barcode = "BACKUP-1", Price = 42m });
                db.SaveChanges();
            }

            BackupService.CreateVerifiedBackup(database, backup);
            BackupService.Verify(backup);
            using (var restored = new MyPosDbContext(
                       new DbContextOptionsBuilder<MyPosDbContext>()
                           .UseSqlite($"Data Source={backup};Pooling=False").Options))
            {
                Assert.Contains(restored.Products, product => product.Name == "Backup test");
            }

            File.WriteAllText(Path.Combine(folder, "corrupt.db"), "broken data");
            Assert.ThrowsAny<Exception>(() => BackupService.Verify(Path.Combine(folder, "corrupt.db")));

            using (var db = new MyPosDbContext(options))
            {
                db.Products.Add(new Product { Name = "Later row", Barcode = "LATER-1", Price = 10m });
                db.SaveChanges();
            }

            File.Copy(backup, Path.Combine(folder, "restore-pending.db"));
            BackupService.ApplyPendingRestore(folder);

            var safetyCopy = Directory.GetFiles(Path.Combine(folder, "backups"), "pre-restore-*.db");
            Assert.Single(safetyCopy);
            BackupService.Verify(safetyCopy[0]);
            using (var restoredLive = new MyPosDbContext(options))
            {
                Assert.Contains(restoredLive.Products, product => product.Name == "Backup test");
                Assert.DoesNotContain(restoredLive.Products, product => product.Name == "Later row");
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Receipt_claim_depends_on_mode_and_thermal_output_contains_real_qr_command()
    {
        var sale = new Sale
        {
            BranchId = Guid.NewGuid(),
            SaleNumber = 15,
            SaleDate = new DateTime(2026, 10, 4, 10, 30, 0),
            ReceiptType = ReceiptType.System,
            TotalAmount = 112m,
            GrossAmount = 112m,
            VatAmount = 12m,
            VatRate = 0.12m,
            TenderedAmount = 120m,
            ChangeAmount = 8m,
            Items = new List<SaleItem>
            {
                new() { ProductName = "Item", Qty = 1m, UnitPrice = 112m, LineGross = 112m }
            }
        };
        var options = new ReceiptOptions { CompanyName = "Test Store", Width = 32, ShowQr = true };

        var systemText = ReceiptPrinter.BuildPreviewText(sale, options);
        Assert.Contains("OFFICIAL RECEIPT", systemText);
        Assert.Contains("DATE: 10/04/26", systemText);
        Assert.Contains("AMOUNT DUE", systemText);

        var bytes = ReceiptPrinter.BuildEpsonBytes(sale, options);
        Assert.Contains(bytes, value => value == 0x1D);
        Assert.True(bytes.Length > ReceiptPrinter.BuildEpsonBytes(sale,
            new ReceiptOptions { CompanyName = "Test Store", Width = 32, ShowQr = false }).Length);

        sale.ReceiptType = ReceiptType.Manual;
        Assert.DoesNotContain("OFFICIAL RECEIPT", ReceiptPrinter.BuildPreviewText(sale, options));
    }
}
