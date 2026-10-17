using System.Globalization;
using System.Text;
using RetailForma.Core.Entities;

namespace RetailForma.Core.Services;

public static class ReportCsv
{
    public static string Text(string? value)
    {
        value ??= "";
        // CSV quoting is not sufficient protection against spreadsheet formulas.
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') ||
            value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@') ||
            value.StartsWith('\t') || value.StartsWith('\r')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    public static string DailySales(DailySalesReport report, string branch)
    {
        var csv = new StringBuilder("Sale #,Date,Time,Branch,Invoice/OR,Customer,Pay Mode,Senior/PWD ID,Gross,Discount,Total,VAT,Net,Voided" +
            (report.ScopeDescription == null ? "" : ",Filter scope") + "\r\n");
        foreach (var sale in report.Sales)
            csv.AppendLine(string.Join(",", sale.SaleNumber.ToString(CultureInfo.InvariantCulture),
                sale.SaleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                sale.SaleDate.ToString("HH:mm:ss", CultureInfo.InvariantCulture), Text(branch),
                Text(sale.ReceiptNumber), Text(sale.CustomerName ?? "WALK-IN"),
                Text(string.Join(" + ", sale.Payments.Select(p => p.Method.ToString().ToUpperInvariant())) +
                    (sale.DiscountKind == DiscountKind.SeniorPwd ? " (SC)" : "")),
                Text(sale.SeniorIdNumber), Money(sale.GrossAmount), Money(sale.DiscountAmount),
                Money(sale.TotalAmount), Money(sale.VatAmount), Money(sale.NetAmount), sale.IsVoided ? "YES" : "NO") +
                (report.ScopeDescription == null ? "" : "," + Text(report.ScopeDescription)));
        return csv.ToString();
    }
}
