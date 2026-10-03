using System.Printing;
using System.Windows;
using MyPos.Core.Entities;
namespace MyPos.Desktop.Printing;
public static class ReceiptPrinting
{
    public static ReceiptOptions LoadOptions(Sale? sale = null) => new()
    {
        CompanyName = AppSettings.Get("CompanyName", "MY STORE"), BranchName = AppSettings.Get("BranchName", ""), CompanyAddress = AppSettings.Get("CompanyAddress", ""), CompanyTin = AppSettings.Get("CompanyTin", ""), AccrNo = AppSettings.Get("AccrNo", ""), Min = AppSettings.Get("Min", ""), Sn = AppSettings.Get("Sn", ""), FooterMessage = AppSettings.Get("FooterMessage", "THANK YOU! PLEASE COME AGAIN."), Width = int.TryParse(AppSettings.Get("ReceiptWidth", "32"), out var width) ? width : 32, PrinterName = AppSettings.Get("ReceiptPrinterName", ""), ShowQr = AppSettings.Get("ReceiptShowQr", "true") == "true", CashierName = sale?.CashierId is Guid id ? App.Db.Users.Find(id)?.Username ?? "" : ""
    };
    public static string BuildPreviewText(Sale sale, bool reprint = false)
    {
        var opt = LoadOptions(sale); var text = ReceiptPrinter.BuildPreviewText(sale, opt, reprint);
        return opt.ShowQr && sale.SaleNumber != 0 ? text + Environment.NewLine + Environment.NewLine + new string(' ', Math.Max(0, (opt.Width - 11) / 2)) + "[ QR CODE ]" : text;
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
            var document = new System.Windows.Documents.FlowDocument
            {
                PageWidth = 302, PageHeight = 1100, PagePadding = new Thickness(14, 20, 14, 20),
                ColumnWidth = double.PositiveInfinity, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 10
            };
            foreach (var line in ReceiptPrinter.BuildLines(sale, opt, reprint))
            {
                var paragraph = new System.Windows.Documents.Paragraph
                {
                    Margin = new Thickness(0, 1, 0, 1),
                    TextAlignment = line.Align switch
                    {
                        ReceiptAlign.Center => TextAlignment.Center,
                        ReceiptAlign.Right => TextAlignment.Right,
                        _ => TextAlignment.Left
                    }
                };
                var run = new System.Windows.Documents.Run(line.Text);
                if (line.Bold) run.FontWeight = FontWeights.Bold;
                if (line.Big) { run.FontSize = 14; run.FontWeight = FontWeights.Bold; }
                paragraph.Inlines.Add(run); document.Blocks.Add(paragraph);
            }
            var printer = ResolvePrinter(opt);
            var queue = new PrintServer().GetPrintQueues().FirstOrDefault(q => q.FullName == printer)
                ?? throw new InvalidOperationException($"Printer '{printer}' was not found on this computer.");
            var dialog = new System.Windows.Controls.PrintDialog { PrintQueue = queue, UserPageRangeEnabled = false };
            var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)document).DocumentPaginator;
            paginator.PageSize = new Size(302, 1100);
            dialog.PrintDocument(paginator, $"MyPos Receipt - Sale #{sale.SaleNumber}");
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
