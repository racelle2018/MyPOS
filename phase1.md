# RetailForma Phase 1 polish plan

Latest implementation: see [Phase 1 implementation and acceptance](phase1-implementation.md). Owner display naming, the technical role/first-account tool, encrypted backups/local retention, Setup & Recovery and Philippine business time are implemented. Synced-folder backup copies, offline paid activation, online-trial source and owner-approved accounting mappings are implemented. Live license deployment/key provisioning, remote recovery and hardware acceptance remain required; Phase 1 is not rollout-complete. Older baseline recommendations are not current completion claims.

This is the Phase 1 review and delivery checklist for the local Windows POS. The original recommendations below are retained for context; implementation status is tracked here so the plan is not mistaken for a list of completed work.

Owner decisions and remaining implementation-policy details are recorded in [Phase 1 decisions](phase1-decisions.md). Phase 1 is the single-computer Basic tier; LAN multi-computer/multi-branch delivery is later work. Confirmed requirements include optionally enabling either/both Dropbox and Google Drive, AES-256 encrypted ZIP backups with approved retention safeguards, a full-access System Administrator role, paid purchases/opening inventory first, fixed PHP/Asia/Manila, XP-58IIH and compatible 80-mm receipt support. The Owner/Admin business-settings versus System Administrator-only licensing/installation/maintenance split is approved. These decisions do not mean those features or accounting corrections are already implemented or accepted; granular technical safeguards, licensing/provisioning and account mappings still need definition.

## Current implementation status — October 13, 2026 (Asia/Manila)

Core local POS features and the approved operational report additions are implemented. Phase 1 is **not fully rollout-accepted**: reliability/product-finish work and physical-store acceptance remain. Implementation means current source contains the feature; it does not certify hardware, accounting policy, or receipt compliance.

Latest verified solution run: See the latest verified counts in [implementation and acceptance](phase1-implementation.md). Build has zero warnings/errors. Final checkpoint verification is recorded in [implementation and acceptance](phase1-implementation.md). Tests do not certify production accounting, cloud recovery or physical hardware.

### Implemented

| Area | Current delivery | Evidence |
| --- | --- | --- |
| Checkout and money | Senior/PWD argument correction; shared calculator; stock/price recheck; transaction rollback and sale/void regression coverage | `PosView.xaml.cs`, `SaleService.cs`, `SaleCalculator.cs`, sale tests |
| Shift accountability | Bounded branch/shift sales; explicit new-sale/refund drawer links; legacy timestamp linkage disclosed; closed expected/count/variance preserved after later voids | `ShiftService.cs`, `ShiftReconciliationReport.cs`, shift/report tests |
| Drawer policy and voids | Admin-controlled Shared/Assigned policy in Drawer Access Settings, effective next shift; active-admin void approval; restricted drawer visibility | `CashDrawerSettingsView`, core shift/sale services |
| Shift close/count | Scrollable counting with fixed actions, empty quantities reset to zero, denomination persistence, recorded close operator, fresh-total/shift-ID checks and closing confirmation | `ShiftCloseDialog`, `CashShift.cs`, close-count/shift tests |
| Cash counting settings | Admin-only submenu with persistent/audited minimum denomination, default ₱0.25; new forms and close validation enforce it without rewriting historical counts or rounding transaction values | `CashCountingSettingsView`, `ShiftService.cs`, counting tests |
| QRPh recording | Separate QRPh noncash option alongside Cash/Card/GCash/Maya/Bank; payment filters and totals include it, with old stored payment values preserved. No payment-provider integration or online verification is implied | Payment enum, POS/payment/report paths, QRPh regression test |
| Appearance | Admin-only bundled/approved installed font, readability and table-spacing presets with immediate sample preview, Default (Inter), explicit size defaults, Save/Reset, validation/fallback and audit; matching heights and compact POS adaptation. Printed/PDF reports, regular receipts and preview follow only the saved font family; output sizes/emphasis and native thermal fonts remain fixed. Colors/borders stay fixed | `AppearanceSettingsService`, `AppearanceResources`, `AppearanceSettingsView`, `ReceiptDocument`, glyph/preset/layout tests |
| Held drafts | Persistent full draft fields, validation/stock-clamping before completing recall, explicit discard, held count; no automatic 24-hour deletion | `HeldSaleService.cs`, `PosView.xaml.cs`, held-sale tests |
| Cashier feedback | Visible shift status, customer address autofill, clean new-sale reset, field validation, saved-sale feedback independent of printing | `PosView.xaml.cs`, payment/receipt dialogs |
| UI and shortcuts | Home/top-menu navigation, compact approved styling, 12-DIP on-screen tables, 1-DIP UI borders, close-only regular dialogs, scrollable shortcut hint; Alt underlines/dotted adorners hidden by owner request | Shared styles, `MainWindow`, views/dialogs, Desktop tests |
| Action notifications | Shared success/warning/error/information/confirmation dialogs for explicit actions; active/native-visible ownership, safe confirmation defaults, fixed buttons and scrollable messages. Ongoing context stays inline; business audit logs are preserved | `ActionDialog`, `NotificationDialog`, [notification audit](notification-audit.md), Desktop tests |
| Login/session | Compact centered branding and floating fields, starter-password change, Caps Lock warning, press-and-hold password reveal, duplicate-login guard; all app windows hidden during lock and restored only after successful unlock | Login/lock windows, `IdleSessionGuard.cs`, Desktop tests |
| Backup/recovery | AES-256 ZIP snapshots and encrypted production safety copies; owner-held portable recovery password with DPAPI automation secret; legacy .db preserved; verified second-folder copy/status; staged restore; 12-hour due interval | `EncryptedBackup`, `BackupService`, isolated backup/restore tests |
| Printing support | Thermal/regular printer paths, real QR output on both, missing saved-printer warning, test receipt and reprint flow | Settings, `ReceiptPrinter.cs`, `ReceiptPrinting.cs`, receipt dialogs |
| Operational reports | Daily Sales, Sales Summary daily/monthly grouping, Product/Category Sales, Shift/Cashier Reconciliation, Cash Movements, admin-only Void Events, and Audit Log CSV | Report services/views and report tests |
| Historical report data | New-sale category/unit/tax/line-money snapshots; old unavailable values remain disclosed rather than reconstructed | `Sale.cs`, calculator/posting services, migrations, snapshot tests |
| Movement capture | Classification, expense category, destination/reference, recorded user and optional admin approval; classification is not expense-ledger certification | `CashShift.cs`, `ShiftService.cs`, Cash Movements report |
| Report output | Selected filters only; conditional multi-branch scope; shared headers, top-right page/print date, continuation headers, A4/Short/Long portrait, direct PDF save, editable/reset zoom and paper guide; duplicate report-name footers removed | Print/PDF builders/paginator and Desktop render checks |
| Report cleanup | Sales Summary/Cash Movements/Void Events totals once below the table; readable movement labels and cash-out-only breakdown; decimal separators in shared print tables | Report presentation builders; CSV remains unchanged |
| Local safeguards | Single-instance guard, SQLite WAL/timeout, audits, migrations and crash logging; core calculations/services remain separate from WPF | `App.xaml.cs`, core data/services, regression tests |

### Not implemented or only partially complete

| Item | Status | Completion condition |
| --- | --- | --- |
| Licensing deployment | Offline issuer/replacement ledger, encrypted device-bound activation and trial runtime/API source implemented | Separate privately provisioned keys, approved HTTPS/PostgreSQL hosting, Release installer trust and end-to-end trial/lifetime/transfer acceptance remain; optional portal is Phase 2 |
| Backup retention/pruning | Implemented local automatic retention: 30 daily/8 weekly; verified new-copy guard; manual/safety/legacy/unverifiable and pending markers preserved | Direct-provider pending-upload acknowledgment/remote retention still require cloud integration; no live backups pruned during implementation |
| Backup free-space warning | Local backup/restore space checks with 64 MB reserve; UNC failures reported by actual writes | Physical low-space/remote quota acceptance remains |
| First-run setup checklist | Implemented admin-only Setup & Recovery first-review page, persistent audited checklist and emergency guidance; cashier navigation not blocked | Owner confirmations and physical/external recovery acceptance remain |
| Transaction timestamp policy | New events/default dates use fixed Asia/Manila via BusinessClock; archive/schedule elapsed times use UTC; legacy records are not converted | Deployment review under changed Windows timezone; historical dates remain as recorded |
| Crash/concurrency qualification | Partial: transactions, single-instance and stale-state guards exist | Fault-injection/unexpected-termination drills during sale, recall, close and backup; no claim of multi-client/server safety |
| Receipt preview fidelity | Partial: selected-font preview, measured WPF amount columns and real QR exist; native thermal encoding is retained. Paper equivalence is not established | Check wrapping/alignment against physical 58/80 mm and regular printers; preview keeps its existing 12-DIP text versus regular 10/14-DIP and native thermal sizes |
| Recovery help | Setup & Recovery page and [implementation guide](phase1-implementation.md) provide locations, passphrase/profile guidance and safe staged recovery | External/cloud restore drill and startup-failure recovery qualification remain |
| Inventory/accounting correctness | Future paid-purchase direction corrected: Inventory debit/payment credit; historical journals preserved | Approved prospective opening/adjustment/classified cash mappings are implemented; dataset reconciliation and deferred accounting remain in [accounting review](report-accounting-review.md) |

### Still needing live acceptance (not established by automated tests)

- [ ] Barcode scanner Enter/scan flow, quantity editing, dialog focus/tab/Escape behavior, and physical cash drawer operation.
- [ ] 58 mm, 80 mm, and Epson L3110 print samples: long/non-ASCII names, large amounts, QR scanning and reprints; verify failed printing does not cause duplicate payment.
- [ ] Store display/scaling checks at 1366×768, 1920×1080, 125% and 150%, including long store names and visible Pay/actions.
- [ ] Restore into a different Windows profile/computer, verify sales/products/users/settings and prove the external backup is independently usable.
- [ ] Short cashier usability session and local professional review of actual receipt wording/business registration details.

### Explicitly deferred (not missing approved operational report pages)

A/R/customer collections, checks/clearance, case/piece conversion, split-tender settlement, inventory valuation/multi-branch stock ownership, gross/full net profit, historical journal correction, additional Owner/Auditor roles, cloud/server sync, and searchable/selectable-text PDF require separate scope/policy decisions. Current PDFs use rendered page images.

## Archived original recommendations — October 4, 2026

The P0/P1/P2 recommendations and delivery order below are retained as the **original baseline**, not current defect claims. For example, startup-only backups, missing QR output and unsafe immediate recall describe earlier code. The current status tables above supersede those statements. No historical recommendation authorizes changing accounting policy, production records, or owner-approved UI behavior.

## P0 — correct transaction and recovery risks first

1. **Fix senior/PWD checkout end to end.** `PosView.Pay()` passes `calc.Discount` into `SaleService.PostSale()`. For a senior/PWD sale, the calculator reports the statutory saving as `Discount`, while `PostSale()` rejects any positive regular discount combined with `SeniorPwd`. A senior/PWD checkout with savings therefore appears likely to fail despite the calculator tests passing. Pass the entered regular discount (zero for senior/PWD) to the service, then add a test covering the exact UI-to-service arguments and the posted total. Keep the discount shown in reports as the computed saving. See `src/RetailForma.Desktop/Views/PosView.xaml.cs` and `src/RetailForma.Core/Services/SaleService.cs`.

2. **Reconcile shift totals against one bounded shift.** `GetShiftTotals()` selects every sale since `OpenedAt` without filtering by branch or the shift's close time. It excludes voided sales from cash sales and also returns `VoidedCashSales`, which the close formula subtracts again. Define an explicit rule for voids made after a shift closes, use a start/end window and branch filter, and compute expected drawer from actual net cash flow exactly once. Add examples for cash, noncash, in/out, void before close, void after close, and consecutive shifts. See `src/RetailForma.Core/Services/ShiftService.cs` and `src/RetailForma.Desktop/Dialogs/ShiftCloseDialog.xaml.cs`.

3. **Prove backups can be restored.** `BackupService` creates a SQLite snapshot into `%APPDATA%\MyPos\backups`, keeps 14 files, and checks whether one is due only when the app starts. Add backup verification (`PRAGMA integrity_check` on the copy), a visible last-success/last-failure status, a safe restore flow with confirmation and an automatic pre-restore backup, and a documented recovery drill. Keep a second copy outside the POS computer so disk loss does not destroy both the database and its backups. Test restore into a fresh app profile before relying on this for a store. See `src/RetailForma.Desktop/BackupService.cs` and `src/RetailForma.Desktop/Views/SettingsView.xaml.cs`.

4. **Review receipt claims and physical output before live use.** The renderer emits official-receipt wording whenever `ReceiptType.System` is set, and settings allow blank accreditation fields. Treat the current receipt as a layout prototype until the store's actual registration details and required wording have been checked by a qualified local adviser. On the implementation side, the preview appends a `[ QR CODE ]` placeholder, but the current ESC/POS and regular print builders do not generate a QR image. Either render a real code on both paths or remove the preview promise. Test 58 mm and 80 mm paper plus the Epson L3110 with long names, large totals, and reprints. See `src/RetailForma.Desktop/Printing/ReceiptPrinter.cs` and `ReceiptPrinting.cs`.

5. **Protect held drafts during recall.** The held-sale record is deleted by `Recall()` before the POS has rebuilt the cart. If loading the products or updating the screen fails afterward, the parked sale is lost. Rehydrate and validate first, then delete the held record only after the replacement cart is ready. Also preserve the customer address, payment mode, order type, discount kind/value, and SC/PWD ID if “park a sale” is meant to preserve the full draft. Warn distinctly when quantity was reduced because stock changed. See `src/RetailForma.Core/Services/HeldSaleService.cs` and `src/RetailForma.Desktop/Views/PosView.xaml.cs`.

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

## Acceptance checks for Phase 1 — current sign-off status

- [x] Automated sale/discount/void/stock and balanced-posting scenarios are covered. This does not validate every inventory journal's account direction.
- [x] Automated bounded/consecutive-shift and cash/void tests cover counting drawer cash effects once and preserving closing snapshots.
- [x] Held-draft persistence/recall safeguards and stock-change warnings are implemented with regression coverage; real cashier restart/recovery acceptance remains open.
- [x] Isolated verified-backup/pre-restore-safety-copy checks exist.
- [ ] Complete a restore drill on another profile/computer and verify the external copy without touching the live database.
- [ ] Accept physical paper output on both printer modes and the intended hardware.
- [ ] Accept cashier/scanner/dialog behavior at the actual display resolution/scaling.
- [ ] Finish outstanding reliability/product work above and obtain receipt/business-detail sign-off before declaring Phase 1 fully rollout-ready.
