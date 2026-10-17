using System.Windows;
using Microsoft.EntityFrameworkCore;
using RetailForma.Desktop.Controls;
using RetailForma.Core.Entities;

namespace RetailForma.Desktop.Dialogs;

public partial class ProductEditDialog : Window
{
    private readonly Product? _editing;   // null = adding a new product

    public ProductEditDialog(Product? editing)
    {
        InitializeComponent();

        if (!Permissions.IsAdmin)
            throw new UnauthorizedAccessException("Only administrators may add or edit products.");

        // Catalog rows are read without tracking so edits from other instances
        // are visible. Resolve the tracked entity only when the edit starts.
        _editing = editing == null ? null : App.Service.Products().First(p => p.Id == editing.Id);

        LoadCategoryChoices();
        LoadUnitChoices();

        if (_editing != null)
        {
            Title = "Edit product";
            TitleText.Text = "Edit product";
            NameBox.Text = _editing.Name.ToUpperInvariant();
            BarcodeBox.Text = (_editing.Barcode ?? "").ToUpperInvariant();
            CategoryBox.Text = _editing.Category.ToUpperInvariant();
            UnitBox.Text = _editing.Unit.ToUpperInvariant();
            CostBox.Text = _editing.CostPrice.ToString("0.####");
            PriceBox.Text = _editing.Price.ToString("0.####");
            VatExemptBox.IsChecked = _editing.IsVatExempt;
        }
        else
        {
            Title = "Add product";
            TitleText.Text = "Add product";
            UnitBox.Text = "PC";   // most common default
        }

        BarcodeBox.Focus();
    }

    private void LoadCategoryChoices()
    {
        // Self-maintaining: every category already in the database, plus seeds for a fresh install
        var existing = App.Service.Products().Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();

        var seeds = new[]
        {
            "BEVERAGES", "SNACKS", "CANNED GOODS", "DAIRY",
            "CONDIMENTS", "CLEANING SUPPLIES", "PERSONAL CARE"
        };

        ComboBoxSearch.Attach(CategoryBox, existing.Concat(seeds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList(), casing: System.Windows.Controls.CharacterCasing.Upper);
    }

    private void LoadUnitChoices()
    {
        var existing = App.Service.Products().Select(p => p.Unit)
            .Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();

        var common = new[]
        {
            "PC", "PACK", "BOX", "SACHET", "KG", "G",
            "L", "ML", "DOZEN", "PAIR", "SET", "ROLL", "TRAY"
        };

        ComboBoxSearch.Attach(UnitBox, existing.Concat(common)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => u)
            .ToList(), casing: System.Windows.Controls.CharacterCasing.Upper);
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
            var taken = App.Service.Products().AsQueryable().ToList()
                .Any(p => p.Barcode == barcode && p.Id != _editing?.Id);
            if (taken) { Fail($"Barcode '{barcode}' is already used by another product."); return; }
        }

        try
        {
            App.Service.SaveProduct(new RetailForma.ServiceApplication.ProductInput(_editing?.Id, name,
                barcode.Length == 0 ? null : barcode, category, unit, price, VatExemptBox.IsChecked == true, cost));
        }
        catch (Exception ex) { Fail(ex.Message); return; }
        ActionDialog.Success(_editing == null ? "Product added." : "Product updated.", Title, this);
        DialogResult = true;
    }

    private void Fail(string message)
    {
        ErrorText.Text = message; ActionDialog.Warning(message, Title, this);
    }
}
