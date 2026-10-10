namespace MyPos.Desktop.Views;

public sealed record KeyboardShortcut(string Action, string Context, IReadOnlyList<string> Keys);
public sealed record KeyboardShortcutGroup(string Title, IReadOnlyList<KeyboardShortcut> Shortcuts);

/// <summary>Help for existing MyPos handlers and standard WPF keyboard actions, not new key bindings.</summary>
public static class KeyboardShortcutCatalog
{
    public static IReadOnlyList<KeyboardShortcutGroup> Groups { get; } =
    [
        new("Application", [
            new("Lock session", "While signed in", ["F12"]),
            new("Home", "Top menu", ["Alt", "H"]),
            new("Sales & Inventory menu", "Top menu", ["Alt", "S"]),
            new("Reports & Inquiry menu", "Top menu", ["Alt", "R"]),
            new("Master File menu", "Administrators only", ["Alt", "M"]),
            new("System Utilities menu", "Administrators only", ["Alt", "U"]),
            new("System menu", "Top menu", ["Alt", "Y"])
        ]),
        new("POS / New Sale", [
            new("Edit quantity", "Select an item in Current Sale", ["F1"]),
            new("Pay", "Current sale", ["F2"]),
            new("Hold sale", "Current sale", ["F3"]),
            new("Clear sale", "Current sale", ["F4"]),
            new("Recall held sale", "Open the held-sales list", ["F6"]),
            new("Remove selected item", "Current Sale table must have focus", ["Delete"]),
            new("Find / add product", "Search: add an exact barcode or single match; otherwise select the first match", ["Enter"]),
            new("Add selected product", "Products table has focus", ["Enter"]),
            new("Pay from discount field", "Discount amount or SC/PWD ID has focus", ["Enter"]),
            new("Clear product search", "When search contains text", ["Esc"])
        ]),
        new("Products", [
            new("Clear search", "Search field has focus", ["Esc"]),
            new("Select search result", "Search field has focus and exactly one product matches", ["Enter"])
        ]),
        new("Report date fields", [
            new("Validate and apply date", "Date field has focus", ["Enter"]),
            new("Restore selected date", "Discard the unfinished date entry", ["Esc"])
        ]),
        new("Dialogs and login", [
            new("Confirm / save", "Activates the default button where available; Enter also unlocks the session", ["Enter"]),
            new("Cancel / close", "Dialogs with a Cancel or Close button; does not log out", ["Esc"])
        ]),
        new("Navigation and text fields", [
            new("Next control", "Standard keyboard navigation", ["Tab"]),
            new("Previous control", "Standard keyboard navigation", ["Shift", "Tab"]),
            new("Move selection", "Focused table, list, or menu", ["↑", "↓"]),
            new("Select all text", "Focused text field", ["Ctrl", "A"]),
            new("Copy selected text", "Focused text field", ["Ctrl", "C"]),
            new("Paste text", "Editable text field", ["Ctrl", "V"]),
            new("Cut selected text", "Editable text field", ["Ctrl", "X"])
        ])
    ];
}
