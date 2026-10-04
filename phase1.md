# MyPos Phase 1 polish plan

This is the Phase 1 review and delivery checklist for the local Windows POS. The original recommendations below are retained for context; implementation status is tracked here so the plan is not mistaken for a list of completed work.

## Implementation status (October 4, 2026)

Implemented in code: senior/PWD checkout argument fix and regression test; bounded shift reconciliation with void scenarios; verified SQLite backup, second-copy option, scheduled checks, staged restore with a pre-restore safety copy and isolated restore test; safe held-sale recall with full draft fields and no silent 24-hour deletion; visible open/closed shift status, held-sale count, inline validation, customer address autofill and a clean new-sale reset; sale-saved feedback independent of printing; QR output on both print paths; missing-printer warning and test receipt; forced change of the starter administrator password. A single-instance guard reduces accidental concurrent desktop writes. The added tests run as part of `dotnet test MyPos.slnx`.

Still requiring acceptance before a live rollout: exercise the actual barcode scanner and cash drawer; inspect 58 mm, 80 mm, and Epson L3110 paper output (including non-ASCII names, long items, and reprints); check the layout at the target resolution and Windows scaling; perform a restore drill on another Windows profile or computer and confirm the external second copy; have the intended receipt wording and business details reviewed locally. Automated tests and a successful build do not substitute for these checks.

## What is already in place

The app has a POS cart, product catalog, daily sales and audit views, users and roles, shift cash control, held sales, senior/PWD discounts, receipt preview, thermal and regular printing, and SQLite migrations. A five-minute idle lock and an automatic backup check also exist. The current automated tests focus on the core sale service and database seed; they do not exercise the WPF checkout, shift reconciliation, receipt rendering, or restore workflows.

## P0 — correct transaction and recovery risks first

1. **Fix senior/PWD checkout end to end.** `PosView.Pay()` passes `calc.Discount` into `SaleService.PostSale()`. For a senior/PWD sale, the calculator reports the statutory saving as `Discount`, while `PostSale()` rejects any positive regular discount combined with `SeniorPwd`. A senior/PWD checkout with savings therefore appears likely to fail despite the calculator tests passing. Pass the entered regular discount (zero for senior/PWD) to the service, then add a test covering the exact UI-to-service arguments and the posted total. Keep the discount shown in reports as the computed saving. See `src/MyPos.Desktop/Views/PosView.xaml.cs` and `src/MyPos.Core/Services/SaleService.cs`.

2. **Reconcile shift totals against one bounded shift.** `GetShiftTotals()` selects every sale since `OpenedAt` without filtering by branch or the shift's close time. It excludes voided sales from cash sales and also returns `VoidedCashSales`, which the close formula subtracts again. Define an explicit rule for voids made after a shift closes, use a start/end window and branch filter, and compute expected drawer from actual net cash flow exactly once. Add examples for cash, noncash, in/out, void before close, void after close, and consecutive shifts. See `src/MyPos.Core/Services/ShiftService.cs` and `src/MyPos.Desktop/Dialogs/ShiftCloseDialog.xaml.cs`.

3. **Prove backups can be restored.** `BackupService` creates a SQLite snapshot into `%APPDATA%\MyPos\backups`, keeps 14 files, and checks whether one is due only when the app starts. Add backup verification (`PRAGMA integrity_check` on the copy), a visible last-success/last-failure status, a safe restore flow with confirmation and an automatic pre-restore backup, and a documented recovery drill. Keep a second copy outside the POS computer so disk loss does not destroy both the database and its backups. Test restore into a fresh app profile before relying on this for a store. See `src/MyPos.Desktop/BackupService.cs` and `src/MyPos.Desktop/Views/SettingsView.xaml.cs`.

4. **Review receipt claims and physical output before live use.** The renderer emits official-receipt wording whenever `ReceiptType.System` is set, and settings allow blank accreditation fields. Treat the current receipt as a layout prototype until the store's actual registration details and required wording have been checked by a qualified local adviser. On the implementation side, the preview appends a `[ QR CODE ]` placeholder, but the current ESC/POS and regular print builders do not generate a QR image. Either render a real code on both paths or remove the preview promise. Test 58 mm and 80 mm paper plus the Epson L3110 with long names, large totals, and reprints. See `src/MyPos.Desktop/Printing/ReceiptPrinter.cs` and `ReceiptPrinting.cs`.

5. **Protect held drafts during recall.** The held-sale record is deleted by `Recall()` before the POS has rebuilt the cart. If loading the products or updating the screen fails afterward, the parked sale is lost. Rehydrate and validate first, then delete the held record only after the replacement cart is ready. Also preserve the customer address, payment mode, order type, discount kind/value, and SC/PWD ID if “park a sale” is meant to preserve the full draft. Warn distinctly when quantity was reduced because stock changed. See `src/MyPos.Core/Services/HeldSaleService.cs` and `src/MyPos.Desktop/Views/PosView.xaml.cs`.

## P1 — cashier flow and UI/UX

1. **Make the active shift visible on the POS screen.** Show “Shift open” with cashier/opened time, or a prominent “Open shift to take payment” action. Today the cashier learns about a closed shift when pressing Pay. The shift action should take them directly to the Shift screen and return to the draft afterward.

2. **Clarify the payment sequence.** Keep invoice/OR validation, senior/PWD ID validation, stock recheck, tendered amount, receipt printing, and sale completion in a consistent order. Once a sale posts, the UI should say “Sale saved” even if printing fails; printing is a separate recoverable step. A failed print should never invite another payment attempt for the same sale.

3. **Consolidate feedback.** Use inline field errors for missing invoice and ID, a persistent message for print failures, and a short status/toast for reversible actions such as adding an item. The yellow status strip should not be the sole place where a blocked payment or failed print is explained. Give every warning a clear recovery action.

4. **Improve keyboard and focus behavior.** Document F2/F3/F4/F6/F12 in a small help panel. After opening a modal, return focus to the search field only when the cashier is truly back at the POS. Verify scanner Enter, quantity editing, tab order, Escape behavior, and keyboard access to every dialog with a cashier using the real scanner.

5. **Scale the layout for real screens.** The header, cart, totals, and top navigation use several fixed widths. Check 1366×768, 1920×1080, 125%/150% Windows scaling, and long translations or store names. Keep Pay and the amount due visible without horizontal clipping. Let the address and search areas take spare width; allow navigation to wrap or collapse on narrower displays.

6. **Make states explicit in tables.** Product inactive/low-stock states, voided sales, shift variance, and held-sale age need readable labels in addition to color. Preserve a visible keyboard focus indicator on grid rows and buttons. Right-align money, use consistent peso/decimal formatting, and avoid truncating numbers.

7. **Reduce repetitive entry.** Auto-fill customer address from a selected known customer, show recent held sales count beside Recall, and keep a clear “new sale” reset rule. Do not silently overwrite typed invoice numbers or customer details when navigating away and back.

## P1 — backup and backend reliability

1. **Schedule while the app is open.** A busy store may leave the app open for days; startup-only backup checks will not run again. Use an app timer or Windows scheduled task, and avoid blocking the cashier UI while `VACUUM INTO` runs.

2. **Make retention safe.** Prune only after a new backup has been created and verified. Retain a mix of recent and older snapshots (for example daily plus weekly) so a corruption discovered late is not represented in every retained copy. Show free-space warnings and the path of the last verified backup.

3. **Handle concurrent operations consistently.** Use a transaction for shift close, cash movements, held-sale transitions, and audits where those records must succeed together. Prevent a second app instance or competing window from posting into the same shift or assigning overlapping sale numbers. Test unexpected termination during a sale and during backup.

4. **Separate UI from core services.** Keep amount calculations, shift rules, backup validation, and print layout behind testable methods. The WPF code-behind should coordinate user input and display errors, rather than be the only place business rules exist. Preserve readable formatting throughout; do not minify XAML or C#.

5. **Review timestamps and audit identity.** Store timestamps with an explicit time-zone policy and retain the acting user for recall, lock failures, voids, and shift actions. Display local time to cashiers. Clarify which events can be altered after shift close and record corrections as new events rather than editing history.

## P2 — product finish

- Add a Settings “Printer check” card showing the selected mode, queue name, paper width, and an explicit test print. If a queue disappears, keep its saved name visible with a warning instead of silently showing Windows default.
- Improve the receipt preview so bold/large type, alignment, wrapping, and any QR code resemble the actual paper. Provide clear reprint labeling and log who reprinted.
- Add an in-app backup/restore help page with the exact backup location and a short emergency procedure.
- Add a first-run setup checklist: store details, admin password, printer mode, receipt mode, opening stock, and a successful test backup.
- Perform a short cashier usability session with scanner, cash drawer, Epson L3110, and intended thermal printer. Record confusing steps and time to complete a sale before further visual redesign.

## Suggested delivery order

| Pass | Work | Done when |
|---|---|---|
| 1 | Senior/PWD checkout and shift reconciliation | Full sale and shift scenarios pass; totals match hand calculations. |
| 2 | Backup verification and restore drill | A verified snapshot restores sales, products, users, and settings into a fresh profile. |
| 3 | Held-sale safety and transaction boundaries | A failed recall leaves the held record available; no partial cash/shift writes. |
| 4 | POS feedback, navigation, keyboard, scaling | A cashier can complete and recover from the main flows on the target display without guessing. |
| 5 | Receipt hardware and compliance review | Printed samples match preview closely; wording and business details are approved for the intended store. |

## Acceptance checks for Phase 1

- A standard cash sale, noncash sale, senior/PWD sale, void, and stock-limited sale post correctly with balanced entries.
- Two consecutive shifts reconcile independently; cash in/out and voids affect the expected drawer once.
- A held sale survives restart; recall restores its draft; stock changes produce a specific warning; a failed recall remains recoverable.
- A verified backup can be restored on another Windows profile/computer without using the live database.
- Both printer modes produce readable paper output on the intended hardware; print failure never hides a completed sale.
- Cashiers can operate the POS and recover from validation errors using keyboard/scanner at the actual screen resolution and scaling.
