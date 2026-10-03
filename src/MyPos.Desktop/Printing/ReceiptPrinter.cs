using System.Text;
using MyPos.Core.Entities;

namespace MyPos.Desktop.Printing;

public sealed class ReceiptOptions
{
    public string CompanyName { get; set; } = "MY STORE";
    public string BranchName { get; set; } = "";
    public string CompanyAddress { get; set; } = "";
    public string CompanyTin { get; set; } = "";
    public string AccrNo { get; set; } = "";
    public string Min { get; set; } = "";
    public string Sn { get; set; } = "";
    public string CashierName { get; set; } = "";
    public string FooterMessage { get; set; } = "THANK YOU! PLEASE COME AGAIN.";
    public int Width { get; set; } = 32;
    public string PrinterName { get; set; } = "";
    public bool ShowQr { get; set; } = true;
}
public enum ReceiptAlign { Left = 0, Center = 1, Right = 2 }
public sealed class ReceiptLine { public string Text { get; set; } = ""; public ReceiptAlign Align { get; set; } public bool Bold { get; set; } public bool Big { get; set; } }
public static class ReceiptPrinter
{
    public static List<ReceiptLine> BuildLines(Sale sale, ReceiptOptions opt, bool reprint = false)
    {
        var lines = new List<ReceiptLine>(); var w = opt.Width;
        void Add(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool big = false) => lines.Add(new ReceiptLine { Text = text, Align = align, Bold = bold, Big = big });
        string Sep() => new('-', w); string Pair(string left, string right) => left.Length + right.Length >= w ? Trunc(left, Math.Max(0, w - right.Length - 1)) + " " + right : left + new string(' ', w - left.Length - right.Length) + right;
        if (opt.CompanyName.Length > 0) Add(Trunc(opt.CompanyName, w), ReceiptAlign.Center, true, true);
        if (opt.BranchName.Length > 0) Add(Trunc(opt.BranchName, w), ReceiptAlign.Center, true);
        foreach (var part in Wrap(opt.CompanyAddress, w)) Add(part, ReceiptAlign.Center);
        if (opt.CompanyTin.Length > 0) Add(Trunc($"VAT REG. TIN: {opt.CompanyTin}", w), ReceiptAlign.Center);
        if (opt.AccrNo.Length > 0) Add(Trunc($"ACCR NO.: {opt.AccrNo}", w), ReceiptAlign.Center);
        if (opt.Min.Length > 0) Add(Trunc($"MIN: {opt.Min}", w), ReceiptAlign.Center);
        if (opt.Sn.Length > 0) Add(Trunc($"SN: {opt.Sn}", w), ReceiptAlign.Center);
        Add(""); Add(sale.SaleNumber == 0 ? "*** TEST PRINT ***" : "SALES INVOICE", ReceiptAlign.Center, true); Add("");
        Add(Pair($"DATE: {sale.SaleDate:MM/dd/yy}", $"TIME: {sale.SaleDate:HH:mm}"));
        if (opt.CashierName.Length > 0) Add(Trunc($"CASHIER: {opt.CashierName}", w));
        if (!string.IsNullOrWhiteSpace(sale.CustomerName)) Add(Trunc($"CUSTOMER: {sale.CustomerName}", w));
        if (!string.IsNullOrWhiteSpace(sale.CustomerAddress)) Add(Trunc($"ADDR: {sale.CustomerAddress}", w));
        if (sale.DiscountKind == DiscountKind.SeniorPwd) Add(Trunc($"SC/PWD ID: {sale.SeniorIdNumber}", w));
        if (!string.IsNullOrWhiteSpace(sale.ReceiptNumber) && sale.SaleNumber != 0) Add(Trunc($"INVOICE/OR NO.: {sale.ReceiptNumber}", w)); Add(Sep());
        foreach (var item in sale.Items) { Add(Trunc(item.ProductName, w)); Add(Pair($"  {item.Qty:0.##} X {item.UnitPrice:N2}", item.LineGross.ToString("N2"))); }
        Add(Sep()); Add($"{sale.Items.Count} ITEM(S)"); Add(Pair("SUBTOTAL", sale.GrossAmount.ToString("N2")));
        if (sale.DiscountAmount != 0) Add(Pair(sale.DiscountKind == DiscountKind.SeniorPwd ? "SC/PWD 20% + VAT-EX" : "DISCOUNT", "-" + sale.DiscountAmount.ToString("N2")));
        Add(Pair($"VAT ({sale.VatRate * 100:0.#}%)", sale.VatAmount.ToString("N2"))); Add(Pair("AMOUNT DUE", sale.TotalAmount.ToString("N2")), bold: true);
        Add(Pair(sale.Payments.FirstOrDefault()?.Method.ToString().ToUpperInvariant() ?? "CASH", sale.TenderedAmount.ToString("N2"))); Add(Pair("CHANGE", sale.ChangeAmount.ToString("N2"))); Add(Sep());
        if (sale.ReceiptType == ReceiptType.System && sale.SaleNumber != 0) { Add("THIS SERVES AS YOUR", ReceiptAlign.Center); Add("OFFICIAL RECEIPT", ReceiptAlign.Center, true); }
        if (sale.IsVoided) Add("*** VOIDED ***", ReceiptAlign.Center, true); if (reprint) Add("*** REPRINT ***", ReceiptAlign.Center, true);
        foreach (var part in Wrap(opt.FooterMessage, w)) Add(part, ReceiptAlign.Center);
        return lines;
    }
    public static string BuildPreviewText(Sale sale, ReceiptOptions opt, bool reprint = false) => string.Join(Environment.NewLine, BuildLines(sale, opt, reprint).Select(l => l.Text.Length >= opt.Width ? l.Text : l.Align == ReceiptAlign.Center ? new string(' ', (opt.Width - l.Text.Length) / 2) + l.Text : l.Align == ReceiptAlign.Right ? new string(' ', opt.Width - l.Text.Length) + l.Text : l.Text));
    public static byte[] BuildEpsonBytes(Sale sale, ReceiptOptions opt, bool reprint = false)
    {
        var b = new List<byte>(); void Raw(params byte[] x) => b.AddRange(x); Raw(0x1B, 0x40);
        foreach (var line in BuildLines(sale, opt, reprint)) { Raw(0x1B, 0x61, (byte)line.Align); Raw(0x1B, 0x45, (byte)(line.Bold ? 1 : 0)); Raw(0x1D, 0x21, (byte)(line.Big ? 0x11 : 0)); b.AddRange(Encoding.ASCII.GetBytes(line.Text)); Raw(0x0A); }
        Raw(0x1B, 0x64, 3); Raw(0x1D, 0x56, 1); return b.ToArray();
    }
    private static string Trunc(string value, int width) => value.Length <= width ? value : value[..width];
    private static IEnumerable<string> Wrap(string value, int width) { value = value.Trim(); while (value.Length > width) { yield return value[..width]; value = value[width..].TrimStart(); } if (value.Length > 0) yield return value; }
}
