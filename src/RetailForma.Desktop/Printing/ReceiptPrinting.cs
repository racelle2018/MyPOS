using System.Printing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using QRCoder;
using RetailForma.Core.Entities;
namespace RetailForma.Desktop.Printing;
public static class ReceiptPrinting
{
    public static ReceiptOptions LoadOptions(Sale? sale = null) => new()
    {
        CompanyName = AppSettings.Get("CompanyName", "MY STORE"), BranchName = AppSettings.Get("BranchName", ""), CompanyAddress = AppSettings.Get("CompanyAddress", ""), CompanyTin = AppSettings.Get("CompanyTin", ""), AccrNo = AppSettings.Get("AccrNo", ""), Min = AppSettings.Get("Min", ""), Sn = AppSettings.Get("Sn", ""), FooterMessage = AppSettings.Get("FooterMessage", "THANK YOU! PLEASE COME AGAIN."), Width = int.TryParse(AppSettings.Get("ReceiptWidth", "32"), out var width) ? width : 32, PrinterName = AppSettings.Get("ReceiptPrinterName", ""), ShowQr = AppSettings.Get("ReceiptShowQr", "true") == "true", CashierName = sale?.CashierId is Guid id ? App.Service.StaffMember(id)?.Username ?? "" : ""
    };
    public static string BuildPreviewText(Sale sale, bool reprint = false)
    {
        var options = LoadOptions(sale);
        return ReceiptPrinter.BuildPreviewText(sale, options, reprint);
    }

    public static BitmapImage CreateQrImage(Sale sale)
    {
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(ReceiptPrinter.QrPayload(sale), QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(qrData).GetGraphic(4);
        var image = new BitmapImage();
        using var stream = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
    public static bool TryPrint(Sale sale, bool reprint)
    {
        return AppSettings.Get("ReceiptPrintMode", "Thermal") == "Regular"
            ? TryPrintRegular(sale, reprint)
            : TryPrintThermal(sale, reprint);
    }

    public static bool TryPrintRegular(Sale sale, bool reprint)
    {
        try
        {
            var opt = LoadOptions(sale);
            // WPF sizes are device-independent pixels (1/96 inch).
            var paperWidth = opt.Width == 48 ? 302d : 219d;
            var document = ReceiptDocument.Build(sale, opt, reprint);
            var printer = ResolvePrinter(opt);
            var queue = new PrintServer().GetPrintQueues().FirstOrDefault(q => q.FullName == printer)
                ?? throw new InvalidOperationException($"Printer '{printer}' was not found on this computer.");
            var dialog = new System.Windows.Controls.PrintDialog { PrintQueue = queue, UserPageRangeEnabled = false };
            var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)document).DocumentPaginator;
            paginator.PageSize = new Size(paperWidth, 1100);
            dialog.PrintDocument(paginator, $"RetailForma Receipt - Sale #{sale.SaleNumber}");
            return true;
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "Regular receipt print failed (sale #{SaleNumber})", sale.SaleNumber); return false; }
    }

    private static bool TryPrintThermal(Sale sale, bool reprint)
    {
        try
        {
            var opt = LoadOptions(sale);
            RawPrinterHelper.SendRaw(ResolvePrinter(opt), ReceiptPrinter.BuildEpsonBytes(sale, opt, reprint));
            return true;
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "Receipt print failed (sale #{SaleNumber})", sale.SaleNumber); return false; }
    }

    private static string ResolvePrinter(ReceiptOptions opt)
    {
        if (string.IsNullOrWhiteSpace(opt.PrinterName))
            return new LocalPrintServer().DefaultPrintQueue?.FullName
                ?? throw new InvalidOperationException("No printer configured and no Windows default printer.");
        if (!new PrintServer().GetPrintQueues().Any(q => q.FullName == opt.PrinterName))
            throw new InvalidOperationException($"Printer '{opt.PrinterName}' (from Settings) does not exist on this computer.");
        return opt.PrinterName;
    }
}
