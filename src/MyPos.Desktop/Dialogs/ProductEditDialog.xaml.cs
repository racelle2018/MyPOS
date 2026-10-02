using System.Windows;
using MyPos.Desktop.Controls;
using MyPos.Core.Entities;

namespace MyPos.Desktop.Dialogs;

public partial class ProductEditDialog : Window
{
    private readonly Product? _editing;   // null = adding a new product

    public ProductEditDialog(Product? editing)
    {
        InitializeComponent();

        if (!Permissions.IsAdmin)
            throw new UnauthorizedAccessException("Only administrators may add or edit products.");

        _editing = editing;

        LoadCategoryChoices();
        LoadUnitChoices();

        if (editing != null)
        {
            Title = "Edit product";
            TitleText.Text = "Edit product";
            NameBox.Text = editing.Name;
            BarcodeBox.Text = editing.Barcode ?? "";
            CategoryBox.Text = editing.Category;
            UnitBox.Text = editing.Unit;
            CostBox.Text = editing.CostPrice.ToString("0.####");
            PriceBox.Text = editing.Price.ToString("0.####");
            VatExemptBox.IsChecked = editing.IsVatExempt;
        }
        else
        {
            Title = "Add product";
            TitleText.Text = "Add product";
            UnitBox.Text = "PC";   // most common default
        }

        NameBox.Focus();
    }

    private void LoadCategoryChoices()
    {
        // Self-maintaining: every category already in the database, plus seeds for a fresh install
        var existing = App.Db.Products.Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();

        var seeds = new[]
        {
            "BEVERAGES", "SNACKS", "CANNED GOODS", "DAIRY",
            "CONDIMENTS", "CLEANING SUPPLIES", "PERSONAL CARE"
        };

        ComboBoxSearch.Attach(CategoryBox, existing.Concat(seeds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList());
    }

    private void LoadUnitChoices()
    {
        var existing = App.Db.Products.Select(p => p.Unit)
            .Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();

        var common = new[]
        {
            "PC", "PACK", "BOX", "SACHET", "KG", "G",
            "L", "ML", "DOZEN", "PAIR", "SET", "ROLL", "TRAY"
        };

        ComboBoxSearch.Attach(UnitBox, existing.Concat(common)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => u)
            .ToList());
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";

        var name = NameBox.Text.Trim().ToUpperInvariant();
        var barcode = BarcodeBox.Text.Trim().ToUpperInvariant();
        var category = CategoryBox.Text.Trim().ToUpperInvariant();
        var unit = UnitBox.Text.Trim().ToUpperInvariant();
        if (unit.Length == 0) unit = "PC";   // never save an empty unit

        if (name.Length == 0) { Fail("Product name is required."); return; }
        if (!decimal.TryParse(CostBox.Text.Trim(), out var cost) || cost < 0)
        { Fail("Enter a valid cost price (0 or more)."); return; }
        if (!decimal.TryParse(PriceBox.Text.Trim(), out var price) || price <= 0)
        { Fail("Enter a valid selling price."); return; }

        if (barcode.Length > 0)
        {
            var taken = App.Db.Products.ToList()
                .Any(p => p.Barcode == barcode && p.Id != _editing?.Id);
            if (taken) { Fail($"Barcode '{barcode}' is already used by another product."); return; }
        }

        if (_editing == null)
        {
            var p = new Product
            {
                Name = name,
                Barcode = barcode.Length == 0 ? null : barcode,
                Category = category,
                Unit = unit,
                CostPrice = cost,
                Price = price,
                IsVatExempt = VatExemptBox.IsChecked == true
            };
            App.Db.Products.Add(p);
            App.Db.AuditLogs.Add(new AuditLog
            {
                Date = DateTime.Now, UserId = App.CurrentUser?.Id, Action = "ProductCreate",
                EntityName = "Product", EntityId = p.Id,
                Details = $"'{name}' — price ₱{price:N2}"
            });
        }
        else
        {
            _editing.Name = name;
            _editing.Barcode = barcode.Length == 0 ? null : barcode;
            _editing.Category = category;
            _editing.Unit = unit;
            _editing.CostPrice = cost;
            _editing.Price = price;
            _editing.IsVatExempt = VatExemptBox.IsChecked == true;
            App.Db.AuditLogs.Add(new AuditLog
            {
                Date = DateTime.Now, UserId = App.CurrentUser?.Id, Action = "ProductEdit",
                EntityName = "Product", EntityId = _editing.Id, Details = $"'{name}'"
            });
        }

        App.Db.SaveChanges();
        DialogResult = true;
    }

    private void Fail(string message)
    {
        ErrorText.Text = message;
    }
}
