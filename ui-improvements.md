# MyPos UI/UX improvement plan

Reviewed October 4, 2026 against the current WPF/XAML source. This file records both the UI recommendations and the implementation pass. `phase1.md` tracks the broader product and reliability work; this file focuses on the interface.

## Implementation status

The code pass now includes a reflowing POS header and product/cart workspace, wrapped sales-report summaries and actions, shared light-theme colors and visible keyboard focus, clearer empty states and stock labels, selection-aware actions, a pinned Settings save action with inline feedback, and a persistent print-failure retry banner. Sale completion and receipt-print outcomes are tracked separately. Held-sale recall warns before accepting reduced or skipped stock. Login, lock, payment, password setup, receipt preview, and held-sale dialogs received sizing, labeling, or action-state improvements. The main app remains maximized while login and lock stay compact.

The original light-blue color scheme remains in place. The purple reference palette was tried and then removed at the user's request; the earlier layout and usability improvements remain.

Wix Madefor Text is bundled with the desktop app and used for interface text and the daily-sales report. Interface icons now use Material Design in XAML's `PackIcon` rather than Segoe font glyphs; fixed-width receipt previews and print layouts keep Consolas so columns remain aligned. The font's SIL Open Font License is included under `src/MyPos.Desktop/Assets/Fonts/OFL.txt`.

The blue/white palette and Wix Madefor Text typography are shared through `App.xaml` across the main shell, screens, and dialogs. The app now uses Material Design in XAML's light theme and templates for buttons, fields, selectors, dates, and grids; compact MyPos overrides keep cashier workflows dense. The login keeps floating hints in its 280-by-48-pixel fields and the native Windows title bar. Visual checks at 100%/125%/150% scaling are still required.

The Shift screen now uses a wider, wrapping summary-card layout and a clearer current-shift action panel. The POS transaction header has labels above fields and no horizontal field scroller: Invoice, Customer, and Address use a 300-pixel column; Pay Mode and Delivery Type use a 150-pixel column. The 360-pixel blue summary emphasizes Total, Net, and VAT, and moves below the fields below 900 pixels. The product/cart workspace keeps its own 720-pixel reflow breakpoint. The SC/PWD ID appears only for that discount. Shift open, cash movement, and close dialogs have persistent field labels and consistent primary actions. These are layout and input-presentation changes, not changes to the shift or sale calculations.

Automated build and tests validate compilation and covered behavior, but do not prove visual layout or printer/scanner operation. The acceptance checklist below still requires a hands-on run at the target display sizes and Windows scaling settings. Capture baseline/after screenshots and adjust any remaining clipping found there. Do not treat a successful receipt preview as proof that paper output matches.

Material Design migration follow-up: the shared primary-button template now preserves its blue background on hover. Desktop tests exercise compact text/password/date content hosts and POS layouts at 1024 × 680 and an effective 840 × 460 viewport. At the shorter height, the POS work area scrolls to Pay while the status clock remains fixed. These layout assertions are not a substitute for inspecting the running app at 100%, 125%, and 150% Windows scaling; verify the fields, dropdowns, calendars, grids, focus states, and receipt/shift dialogs on the actual store display.

## Direction

Keep the existing C#/.NET 10 WPF application and its light Material Design styling. Aim for a fast cashier workspace: the active sale, amount due, and next action should be obvious at a glance. Login and session lock should remain compact windows; only the main app opens maximized. Do not switch to WinForms or add another UI package solely to change the appearance. Keep XAML and C# readable—do not minify them.

Use the supplied pre-invoicing image as a *field and workflow reference*, not a pixel-for-pixel theme. Keep MyPos's own VAT, discount, stock, receipt, and audit behavior.

## P0 — fix usability and layout risks first

| Priority | Current evidence | Suggested change | Done when |
| --- | --- | --- | --- |
| 1. Responsive POS header | `PosView.xaml` places transaction fields in a horizontally scrolling header beside a fixed 250-pixel total card. | Reflow transaction fields into fewer columns when space is limited; allow address to span available width. Keep the total visible without scrolling the header. | At 1366×768 and 125%/150% scaling, Invoice, Customer, Address, Pay Mode, Type, SC/PWD ID, and Total remain readable and reachable. |
| 2. Cart and actions | The cart and product matches share a 2.2:1 grid; fixed cart columns and several action buttons compete for the narrow state. | Give the cart a minimum usable width; collapse or move product matches below/behind search on narrow windows. Reserve a stable area for Pay, Hold, Recall, and Clear. | The amount due and Pay action are visible while editing a sale; cart money columns never truncate amounts. |
| 3. Sales report header | `DailySalesView.xaml` has a six-column `UniformGrid` and horizontal date/actions/receipt rows. | Wrap summary cards into two rows at smaller widths; separate date controls from export/print/void actions; wrap receipt reconciliation labels. | Date, actions, totals, and missing-receipt warning remain legible at 125%/150% scaling without overlapping. |
| 4. Validation and recovery | Checkout has inline invoice/ID errors, but the status strip also carries important sale and print messages. | Use inline errors for fields, a persistent recovery banner for print failure, and brief status feedback for routine actions. Distinguish **Sale saved** from **Receipt not printed**. | A cashier can tell whether payment posted and what to do next without guessing or risking a duplicate sale. |
| 5. Login and lock sizing | Login and lock use compact cards and scrolling; the password-change dialog is a separate fixed-width window. | Verify the three screens at 100%–150% scaling and with long error text. Keep the primary action visible, preserve keyboard focus, and avoid a second fullscreen treatment. | No clipped text/buttons; Enter submits the intended action; errors remain visible without hiding password fields. |

## P1 — make the visual system consistent

1. **Create shared design tokens in `App.xaml`.** Centralize the accent, text, surface, border, danger, warning, success, spacing, and corner-radius values. Several views still hard-code colors and sizes. Change a token once and review all screens before adding more local overrides.
2. **Set a clear type hierarchy.** One page title, one section-heading treatment, regular field labels, and readable helper text. Current 10–11-pixel pale-gray labels in reports/settings deserve a contrast and scaling check. Avoid all-caps for paragraphs or instructions.
3. **Keep visible keyboard focus.** `App.xaml` suppresses the default focus visual on some controls; replace it with a consistent, visible focus state rather than removing it. Give icon-only and in-grid remove buttons accessible names, and verify tab order through dialogs and checkout.
4. **Standardize form controls.** Labels should stay visible even when a placeholder is present. Use Material Design hints for optional guidance; do not use placeholders as the only label. Make error text specific (“Invoice / OR number is required”) and place it next to the field.
5. **Use color plus words.** Stock, voided sales, shift variance, backup status, and print status should have readable text states, not red/amber/green alone. Check normal text contrast against its actual background; Windows guidance uses at least 4.5:1 for ordinary text.
6. **Use consistent table behavior.** Right-align money, preserve full values, offer sensible default column widths, and show an informative empty state. Keyboard selection and double-click behavior should be predictable across Products, Daily Sales, Audit, Users, and Held Sales.

## P1 — screen-by-screen refinements

- **POS (`Views/PosView.xaml`):** Keep the scan/search field close to the item grid and return focus to it after adding a product. Show an empty-cart hint with the scanner/Enter path. Clarify that “Line Amount” is quantity × unit price before sale-level discount. Consider “Net before VAT” instead of the shorter “Before VAT” if cashiers confuse it with gross subtotal. Keep the bottom date/time read-only as previously specified.
- **Products (`Views/ProductCatalogView.xaml`):** The search field is fixed at 320 pixels and actions wrap independently. Let search take remaining width, keep Add Product prominent, and place selection-dependent actions together. State why Edit/Deactivate is unavailable when no row is selected.
- **Reports (`Views/DailySalesView.xaml`, `Views/AuditLogView.xaml`):** Make the selected date and report type unmistakable. Keep the most important totals above the table, but use responsive card rows. Treat the “missing receipts” condition as an actionable warning rather than a color change alone. Preserve export/print availability in empty states only when meaningful.
- **Settings (`Views/SettingsView.xaml`):** Divide the long form into clear groups—Store, Receipts & Printer, Backup & Recovery, Inventory—with a short status summary for each. Keep Save Settings discoverable after scrolling. Explain what is saved immediately versus what needs a restart; show the selected printer and last verified backup in plain language.
- **Shift and held sales (`Views/ShiftView.xaml`, `Dialogs/HeldSalesDialog.xaml`):** Show open/closed state, cashier, opening time, and counted/expected/variance labels together. For held sales, make Recall the primary action and Discard unmistakably destructive; show stock-reduction warnings before confirming recall.
- **Receipt preview and completion:** Show “Sale # saved” even when the printer fails, then offer **Retry print** and **View receipt** without another payment path. Preview should not imply the paper output was verified; test real thermal and inkjet devices.

## Delivery order

1. Establish a screenshot baseline for POS, login, lock, products, reports, settings, and the payment/receipt dialogs at the target store resolution.
2. Fix POS header/cart/actions and report overflow. Do not redesign colors in the same pass.
3. Consolidate shared tokens, form labels, focus states, and table alignment.
4. Improve empty/error/success states and keyboard/scanner flow.
5. Run a cashier walkthrough and adjust the layout based on observed mistakes and time to complete a sale.

## Acceptance checklist

- Test at **1366×768** and **1920×1080**, at **100%, 125%, and 150%** Windows scaling; include a narrow restored main window even though startup is maximized.
- No clipped labels, controls, money values, buttons, date picker, or dialog footer. Long customer/product/store names wrap or truncate intentionally, with access to the full value.
- Complete a sale using only keyboard + scanner: scan, change quantity, apply discount, enter invoice/ID if required, pay, recover from a print failure, and start the next sale.
- Tab and Shift+Tab move logically; visible focus is present; icon-only controls have accessible names. Check with Windows Narrator if available.
- Cashier confirmation, validation failure, backup failure, and print failure each have a distinct, specific message and recovery action.
- `dotnet build MyPos.slnx` and `dotnet test MyPos.slnx` pass after each screen-sized batch of changes. These checks do **not** replace visual and hardware testing.

## References

- [Microsoft: WPF styles and templates](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/styles-templates-overview)
- [Microsoft: accessible text, contrast, and scaling](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessible-text-requirements)
- [Microsoft: accessibility testing](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-testing)
