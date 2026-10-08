using MyPos.Core.Entities;

namespace MyPos.Desktop;

internal static class ProductCatalogSnapshot
{
    public static bool Same(IReadOnlyList<Product> previous, IReadOnlyList<Product> current)
    {
        if (previous.Count != current.Count) return false;

        for (var i = 0; i < previous.Count; i++)
        {
            var a = previous[i];
            var b = current[i];
            if (a.Id != b.Id || a.Barcode != b.Barcode || a.Name != b.Name ||
                a.Unit != b.Unit || a.Category != b.Category ||
                a.CostPrice != b.CostPrice || a.Price != b.Price ||
                a.IsVatExempt != b.IsVatExempt || a.StockQty != b.StockQty ||
                a.IsActive != b.IsActive)
                return false;
        }

        return true;
    }
}
