using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using MyPos.Core.Entities;
using MyPos.Core.Services;

namespace MyPos.Desktop.Dialogs;

public partial class HeldSalesDialog : Window
{
    public List<HeldCartLine>? RecalledCart { get; private set; }
    public string? RecalledCustomer { get; private set; }
    public string? RecalledInvoice { get; private set; }
    private readonly Guid _branchId;
    private List<HeldSale> _held = new();
    public HeldSalesDialog(Guid branchId) { InitializeComponent(); _branchId = branchId; Load(); }
    private void Load() { _held = new HeldSaleService(App.Db).ListActive(_branchId); HeldGrid.ItemsSource = _held.Select(h => new HeldRowVM(h)).ToList(); }
    private HeldSale? Selected => (HeldGrid.SelectedItem as HeldRowVM)?.Held;
    private void RecallButton_Click(object sender, RoutedEventArgs e) => RecallSelected();
    private void HeldGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => RecallSelected();
    private void RecallSelected() { var held = Selected; if (held == null) { MessageBox.Show("Select a held sale first."); return; } RecalledCart = new HeldSaleService(App.Db).Recall(held.Id); RecalledCustomer = held.CustomerName; RecalledInvoice = held.InvoiceNumber; DialogResult = true; }
    private void DiscardButton_Click(object sender, RoutedEventArgs e) { var held = Selected; if (held == null) return; if (MessageBox.Show($"Discard the held sale for '{held.CustomerName}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return; new HeldSaleService(App.Db).Discard(held.Id, App.CurrentUser!.Id); Load(); }
}

public sealed class HeldRowVM
{
    public HeldSale Held { get; }
    public HeldRowVM(HeldSale held) => Held = held;
    private List<HeldCartLine> Lines => JsonSerializer.Deserialize<List<HeldCartLine>>(Held.CartJson) ?? new();
    public string HeldAtText => Held.HeldAt.ToString("MMM d, h:mm tt");
    public string CustomerName => string.IsNullOrWhiteSpace(Held.CustomerName) ? "WALK-IN" : Held.CustomerName;
    public string ItemCount => Lines.Count.ToString();
    public string TotalText => $"₱{Lines.Sum(l => Math.Round(l.Price * l.Qty, 2)):N2}";
    public string UserText => App.Db.Users.Find(Held.UserId)?.Username ?? "—";
}
