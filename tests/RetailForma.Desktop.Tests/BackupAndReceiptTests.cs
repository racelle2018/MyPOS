using System.IO;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.Core.Entities;
using RetailForma.Desktop;
using RetailForma.Desktop.Printing;
using Xunit;

namespace RetailForma.Desktop.Tests;

public class BackupAndReceiptTests
{
    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void Thermal_encoding_preserves_fixed_width_text_and_native_style_commands(int width)
    {
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        try
        {
            var sale = new Sale
            {
                SaleNumber = 15, SaleDate = new DateTime(2026, 10, 4, 10, 30, 0),
                ReceiptType = ReceiptType.System, ReceiptNumber = "R000015", IsVoided = true,
                CustomerName = "CUSTOMER", CustomerAddress = "ADDRESS", SeniorIdNumber = "ID-1",
                DiscountKind = DiscountKind.SeniorPwd, GrossAmount = 112, DiscountAmount = 32,
                TotalAmount = 80, VatRate = .12m, TenderedAmount = 100, ChangeAmount = 20,
                Items = new() { new() { ProductName = "Item", Qty = 1, UnitPrice = 112, LineGross = 112 } },
                Payments = new() { new() { Method = PaymentMethod.QRPh, Amount = 80 } }
            };
            var opt = new ReceiptOptions
            {
                CompanyName = "Test Store", BranchName = "BRANCH", CompanyAddress = "STORE ADDRESS",
                CompanyTin = "123", AccrNo = "456", Min = "789", Sn = "012", CashierName = "CASHIER",
                FooterMessage = "THANK YOU", Width = width, ShowQr = false
            };
            string Pair(string left, string right) => left.PadRight(width - right.Length) + right;
            var expected = new (string Text, byte Align, bool Bold, bool Big)[]
            {
                ("Test Store", 1, true, true), ("BRANCH", 1, true, false), ("STORE ADDRESS", 1, false, false),
                ("VAT REG. TIN: 123", 1, false, false), ("ACCR NO.: 456", 1, false, false),
                ("MIN: 789", 1, false, false), ("SN: 012", 1, false, false),
                ("", 0, false, false), ("SALES INVOICE", 1, true, false), ("", 0, false, false),
                (Pair("DATE: 10/04/26", "TIME: 10:30"), 0, false, false),
                ("CASHIER: CASHIER", 0, false, false), ("CUSTOMER: CUSTOMER", 0, false, false),
                ("ADDR: ADDRESS", 0, false, false), ("SC/PWD ID: ID-1", 0, false, false),
                ("INVOICE/OR NO.: R000015", 0, false, false), (new string('-', width), 0, false, false),
                ("Item", 0, false, false), (Pair("  1 X 112.00", "112.00"), 0, false, false),
                (new string('-', width), 0, false, false), ("1 ITEM(S)", 0, false, false),
                (Pair("SUBTOTAL", "112.00"), 0, false, false), (Pair("SC/PWD 20% + VAT-EX", "-32.00"), 0, false, false),
                (Pair("VAT (12%)", "0.00"), 0, false, false), (Pair("AMOUNT DUE", "80.00"), 0, true, false),
                (Pair("QRPH", "100.00"), 0, false, false), (Pair("CHANGE", "20.00"), 0, false, false),
                (new string('-', width), 0, false, false), ("THIS SERVES AS YOUR", 1, false, false),
                ("OFFICIAL RECEIPT", 1, true, false), ("*** VOIDED ***", 1, true, false),
                ("*** REPRINT ***", 1, true, false), ("THANK YOU", 1, false, false)
            };
            var bytes = new List<byte> { 0x1B, 0x40 };
            foreach (var line in expected)
            {
                bytes.AddRange(new byte[] { 0x1B, 0x61, line.Align, 0x1B, 0x45, (byte)(line.Bold ? 1 : 0), 0x1D, 0x21, (byte)(line.Big ? 0x11 : 0) });
                bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(line.Text));
                bytes.Add(0x0A);
            }
            bytes.AddRange(new byte[] { 0x1B, 0x64, 3, 0x1D, 0x56, 1 });
            Assert.Equal(bytes.ToArray(), ReceiptPrinter.BuildEpsonBytes(sale, opt, true));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
    }

    [Fact]
    public void Verified_backup_contains_database_rows_and_rejects_corruption()
    {
        var folder = Path.Combine(Path.GetTempPath(), "retailforma-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var database = Path.Combine(folder, "retailforma.db");
            var backup = Path.Combine(folder, "backup.db");
            var options = new DbContextOptionsBuilder<RetailFormaDbContext>()
                .UseSqlite($"Data Source={database};Pooling=False").Options;
            using (var db = new RetailFormaDbContext(options))
            {
                DbInitializer.Initialize(db);
                db.Products.Add(new Product { Name = "Backup test", Barcode = "BACKUP-1", Price = 42m });
                db.SaveChanges();
            }

            BackupService.CreateVerifiedBackup(database, backup);
            BackupService.Verify(backup);
            using (var restored = new RetailFormaDbContext(
                       new DbContextOptionsBuilder<RetailFormaDbContext>()
                           .UseSqlite($"Data Source={backup};Pooling=False").Options))
            {
                Assert.Contains(restored.Products, product => product.Name == "Backup test");
            }

            File.WriteAllText(Path.Combine(folder, "corrupt.db"), "broken data");
            Assert.ThrowsAny<Exception>(() => BackupService.Verify(Path.Combine(folder, "corrupt.db")));

            using (var db = new RetailFormaDbContext(options))
            {
                db.Products.Add(new Product { Name = "Later row", Barcode = "LATER-1", Price = 10m });
                db.SaveChanges();
            }

            File.Copy(backup, Path.Combine(folder, "restore-pending.db"));
            BackupService.ApplyPendingRestore(folder);

            var safetyCopy = Directory.GetFiles(Path.Combine(folder, "backups"), "pre-restore-*.db");
            Assert.Single(safetyCopy);
            BackupService.Verify(safetyCopy[0]);
            using (var restoredLive = new RetailFormaDbContext(options))
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
        Assert.Contains("*** REPRINT ***", ReceiptPrinter.BuildPreviewText(sale, options, reprint: true));

        var bytes = ReceiptPrinter.BuildEpsonBytes(sale, options);
        Assert.Contains(bytes, value => value == 0x1D);
        Assert.True(bytes.Length > ReceiptPrinter.BuildEpsonBytes(sale,
            new ReceiptOptions { CompanyName = "Test Store", Width = 32, ShowQr = false }).Length);

        sale.ReceiptType = ReceiptType.Manual;
        Assert.DoesNotContain("OFFICIAL RECEIPT", ReceiptPrinter.BuildPreviewText(sale, options));
    }
}
