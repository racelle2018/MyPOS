namespace MyPos.Core.Services;

/// <summary>Single source of truth for sale totals, VAT, discounts, and COGS.</summary>
public static class SaleCalculator
{
    public sealed record Line(decimal Price, decimal Qty, decimal UnitCost, bool IsVatExempt);

    public sealed record Result(
        decimal Gross,
        decimal Discount,
        decimal Total,
        decimal Net,
        decimal Vat,
        decimal Cogs,
        IReadOnlyList<decimal> LineGrosses);

    public static Result Compute(IReadOnlyList<Line> lines, decimal discount, decimal vatRate)
    {
        var grosses = lines.Select(l => Round2(l.Price * l.Qty)).ToArray();
        var gross = grosses.Sum();
        var disc = Math.Clamp(discount, 0, gross);
        var total = gross - disc;

        var shares = new decimal[lines.Count];
        if (gross > 0)
        {
            decimal allocated = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                shares[i] = i == lines.Count - 1
                    ? disc - allocated
                    : Round2(disc * grosses[i] / gross);
                allocated += shares[i];
            }
        }

        decimal net = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var lineTotal = grosses[i] - shares[i];
            net += lines[i].IsVatExempt ? lineTotal : Round2(lineTotal / (1m + vatRate));
        }

        var cogs = lines.Sum(l => Round2(l.UnitCost * l.Qty));
        return new Result(gross, disc, total, net, total - net, cogs, grosses);
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
