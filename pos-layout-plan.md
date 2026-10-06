# POS layout redesign — review before implementation

Reference: `C:\Users\User\Pictures\Screenshots\Screenshot (37) copy.jpg` (provided October 5, 2026). This is a layout reference, not a pixel-perfect copy. **This document is the only change in this step; the POS screen has not been redesigned yet.**

## 1. Layout to build

At normal desktop width, use a two-column work area under the existing MyPos shell header. The left column is the primary sale area (about two thirds); the right column is the product picker (about one third). Avoid copying the screenshot's large blank areas or any clipped/overlapping controls.

| Area | Intended content | Existing implementation to reuse |
| --- | --- | --- |
| Top-left transaction panel | Invoice / OR number, Pay Mode, Delivery / Type, floating Name and Address, Discount Type, discount amount when applicable, and SC/PWD ID when applicable | `HeaderFields`, `InvoiceBox`, `PayModeBox`, `TypeBox`, `CustomerNameBox`, `AddressBox`, `DiscountKindBox`, `DiscountBox`, `SeniorIdPanel` |
| Top-center/right within sale column | Large blue Total Amount card with net before VAT, VAT, and item count | `TotalCard`, `TotalText`, `NetText`, `VatText`, `ItemCountText` |
| Left main area | Itemized cart with code, description, quantity, price, and line amount; clear empty state | `CartArea`, `CartGrid`, `CartEmptyHint` |
| Right main area | Search-result/product table with Product, Price, Stock; clear empty state | `ProductsGrid`, `ProductsEmptyHint` |
| Bottom of sale column | Search/scan box plus Hold, Recall, Clear, Pay actions | `SearchBox`, `HoldButton`, `RecallButton`, `ClearButton`, `PayButton` |
| Bottom of window | Transaction message and live clock above the existing shell status strip | `StatusText`, `DateTimeText` in `PosView`; shell strip in `MainWindow` |

Keep the invoice field as a **separately labeled, non-floating** field, as requested in the previous change. Name and Address keep their floating hints. Preserve the current light blue MyPos/Material Design theme and the shell's left navigation rail; the reference does not require replacing the app-wide navigation.

## 2. Implementation sequence

1. **Freeze the current behavior.** Record control names, event handlers, keyboard shortcuts, receipt-mode behavior, cart/discount/totals calculations, and current layout test expectations. Work within the dirty worktree; do not replace unrelated files or reformat the entire view.
2. **Restructure only the POS XAML layout.** Make one outer two-column work area. Put the transaction panel and total card in the upper left region, the cart below, the product picker in the right region, and a compact bottom action/search strip under the cart. Move existing controls instead of recreating or renaming them so code-behind references and focus behavior remain intact. Use `Grid` row/column sizing and reasonable minimums, not large fixed blank heights or margins.
3. **Keep important secondary controls reachable.** The discount amount belongs next to Discount Type only when AMOUNT is selected; SC/PWD ID belongs nearby only when SENIOR/PWD is selected. Keep any needed subtotal/discount explanation visible but visually secondary. Retain the print-failure and no-open-shift banners above the sale work area.
4. **Handle narrower windows deliberately.** At reduced width, stack or reposition the total card and product list; allow vertical scrolling where necessary. The search and Pay controls must remain reachable at the current window minimum (`760 × 560`) and at common display scaling. At normal width, keep the product list alongside the cart. Avoid nested scrollbars that trap keyboard or mouse-wheel navigation.
5. **Preserve cashier flow when relocating search.** After adding a product, focus returns to `SearchBox`; a scanner still enters a barcode followed by Enter. Enter on the product list, double-click, quantity edits, Hold/Recall/Clear/Pay and their shortcuts must work unchanged. Do not let a layout move accidentally intercept Enter or F-keys.
6. **Verify totals and accessibility.** The prominent total card must continue to display values computed by `SaleCalculator`, not duplicate arithmetic in XAML/code-behind. Keep money right-aligned, product names readable (trimming/tooltip if needed), meaningful automation names, and visible focus/validation states. The invoice error must stay adjacent to the invoice field.
7. **Test and visually review.** Update `ThemeResourceTests` to assert the new regions/control placement and visibility at normal and compact sizes while preserving the invoice non-floating check and Name/Address floating checks. Run `dotnet test MyPos.slnx -c Release --no-restore`. Then manually inspect the running app with an empty cart, populated cart, discount variants, receipt modes, open/closed shift, long product names, and 100%/125%/150% Windows scaling. Tests alone do not prove the visual layout is good.

## 3. Functions and behavior to review before changing layout

These are current code paths in `src/MyPos.Desktop/Views/PosView.xaml.cs`; the proposed redesign should normally **retain** their behavior.

| Current function / flow | Why it matters for the layout review |
| --- | --- |
| `PosView_SizeChanged` | Currently rearranges the total card below 900px and stacks product/cart below 720px. These breakpoints must be reworked for the new regions, without clipping. |
| `UpdateInvoiceBoxState`, `InvoiceBox_TextChanged` | Automatic vs manual receipt state, required/duplicate invoice validation, tooltip and background. Invoice stays non-floating and its error stays visible. |
| `CustomerNameBox_TextChanged`, `LoadCustomers` | May fill Address from a matching customer; moving fields must not break this. |
| `DiscountKindBox_SelectionChanged`, `DiscountBox_TextChanged`, `ParseDiscount`, `RefreshTotals` | Controls discount amount and SC/PWD ID visibility; amounts, VAT and Total must remain consistent. |
| `RefreshProducts`, `SearchBox_TextChanged`, `SearchBox_KeyDown`, `ProductsGrid_KeyDown`, `ProductsGrid_MouseDoubleClick`, `AddAndReset` | Product filtering and scanner/keyboard selection; moving the search box must not make a scanner harder to use. |
| `AddToCart`, `QtyPlus_Click`, `QtyMinus_Click`, `RemoveLine_Click`, `CartLineVM.Qty` | Stock limits, quantity editing and cart updates. Layout may change, but stock enforcement does not. |
| `HoldCart`, `RecallHeld`, `ClearCart`, `ResetSaleDraft`, `UpdateHeldCount` | Buttons and hotkeys must still preserve/restore/reset the full sale draft and update state. |
| `Pay`, `UpdateShiftStatus`, `RetryReceiptButton_Click` | Pay gating by open shift/cart, receipt requirement, sale posting and failed-print recovery must remain visible and functional. |
| `UserControl_PreviewKeyDown` | F2 Pay, F3 Hold, F4 Clear, F6 Recall, Escape clear search, and Delete cart row. Test after relocating controls. |
| `UpdateClock`, `Status` | Keep live time and transaction messages in the lower status area; don't duplicate the shell's app/version strip. |

Also inspect `src/MyPos.Desktop/MainWindow.xaml`/`.cs` before modifying the POS view: the shell already owns the blue page header, navigation rail and bottom version/status strip, and keeps one `PosView` instance alive across navigation.

## 4. Decisions for you to review

1. **Product selection:** keep today's double-click/Enter-to-add behavior, or allow a single click to add? Recommendation: keep double-click/Enter to avoid accidental sales.
2. **Search position:** the screenshot puts Search/Scan at the bottom left. Keep it there visually, while retaining initial focus and focus return after adding an item? Recommendation: yes.
3. **Discount placement:** move Discount Type and contextual amount/SC-PWD ID into the transaction panel as shown; keep Subtotal and any discount summary near the total/cart footer? Recommendation: yes, so the amount due remains understandable.
4. **Bottom action bar on narrow screens:** should it wrap to two rows, or scroll horizontally? Recommendation: wrap; never hide Pay or require horizontal scrolling.
5. **Product table at 760px width:** stack it below the cart, or place it in a switchable panel? Recommendation for this pass: stack it below and keep search/actions accessible; a switchable panel adds new behavior.

Unless you choose otherwise, the implementation pass should follow those recommendations and remain a **UI/layout refactor**. No database migration, new payment rule, changed sale calculation, or change to the invoice field is part of this plan.

1. keep double-click/Enter to avoid accidental sales.
2. Yes
3. Yes the discount type will be drop down and if none is selected as a default the discount amount/ amount/SC-PWD ID it's a multi purpose field must be disabled as default and enable only depending on the use discount type option.
4. It should be wrap; never hide Pay or require horizontal scrolling.
5. stack it below and keep search/actions accessible.