# Changelog

Notable MyPos changes, newest first. Historical entries are based on repository commit subjects/bodies, not GitHub push timestamps. Dates are commit dates; no release tags or version numbers are implied. New entries describe the changes delivered with this checkpoint.

## 2026-10-12 — Report cleanup and Phase 1 status (working changes, not yet pushed)

### Changed

- Apply selected-filters-only presentation across printable reports: omit default All/empty search labels and the entire Filters line when unused, retain date/period and required shift/grouping context, and identify branch scope when multiple branches exist. Preserve queries, permissions, and full CSV metadata. Record the rule in the MyPos WinBox UI skill.
- Simplify Sales Summary preview/print/PDF: remove the introductory explanatory sentence and duplicate top totals; keep one final totals line below the table. Date-range filtering remains available, including a single day through Today or matching From/To dates; Daily/Monthly controls grouping rather than limiting the range.
- Remove the repeated report-name footer from every report page. Retain continuation headings, upper-right page number/print date, and preparation/signature sections.
- Clean up Cash Movements: show totals once below the table, label the cash-out-only classification breakdown, and include only classifications with matching cash-out entries. Use readable classification/type names, thousands-separated amounts, labeled reference/destination/expense/approval details, and no empty optional detail lines. Move a shorter access/net-movement note below the totals; preserve approval status and accounting behavior.
- Remove duplicate top totals from Void Events and retain one final summary with a concise cancellation-date explanation below the table.
- Format decimal values with thousands separators in shared printed report tables without changing raw report data or CSV numeric formatting. Review the other printable reports for duplicate summaries and preserve their already-approved layouts.

### Verification and remaining work

- Latest verified solution test run: 61 passing tests (49 Core, 12 Desktop). Rendered report layouts checked; portrait A4/Short/Long pagination remains covered. This is not physical-printer or production acceptance certification.
- Approved operational report additions are implemented. The broader Phase 1 rollout is not yet fully accepted: backup retention/free-space warnings, first-run setup checklist, and an explicit transaction time-zone policy remain outstanding, along with real scanner/drawer/printer/scaling checks, an external-profile/computer restore drill, external-backup verification, and local receipt-wording review. See [Phase 1 plan](phase1.md).
- Stock-receipt posting direction, inventory-adjustment journal coverage, and related ledger/valuation/profit readiness require separately approved corrections. No historical transaction/journal rewrite is included. See [accounting-posting review](report-accounting-review.md).

## 2026-10-11 — Approved operational reports (working changes, not yet pushed)

- Simplify Product/Category Sales presentation: hide the single-branch selector without removing branch filtering, show Period and selected filters only, use distinct report titles and one summary, and move a short explanatory note below the table. Explicitly label incomplete product totals as partial and warn about missing historical sale-item amounts; compare invoice totals only for unfiltered products/categories. Preserve historical unknowns, calculation logic and full CSV filter metadata.
- Centralize approved report headers/footers for Daily Sales, Shift/Cashier Reconciliation, Cash Movements, Sales Summary, Product Sales/category summary and Void Events. Use left-aligned 10-DIP bold company/10-DIP business details, centered 14-DIP bold titles, upper-right 10-DIP Page No. current/total and Print Date, and matching Prepared by/1-DIP signature lines. Keep optional branch details compact, preserve reconciliation reviewer sign-offs and all body data/filters, and remove lower Generated/page-count lines and repeated filter scope from page identity. Receipts and CSV are unchanged.
- Align the Daily Sales first-page Page No./Print Date block with the company header's top edge. Reserve right-side space in business details so long names/addresses wrap without overlapping metadata; branch/address lines retain zero vertical gaps.
- Match the owner's Daily Sales upper-right reference with two rows: Page No. as current/total (for example 1/1 or 2/3), and Print Date in MM/dd/yyyy without time. Align values to the paper content's right edge on every page.
- Move Daily Sales printed timestamp and Page X of Y to the upper-right on every preview/print/PDF page. Remove the lower Generated line and page count while retaining Prepared by, signature and report identity; other reports keep their current pagination layout.
- Fix login password reveal after reviewing the owner's recording: replace the second Material field with a borderless text-only overlay inside the existing PasswordBox, preserving its single outline/floating label. Empty passwords do not show a duplicate placeholder; release/deactivation still clears revealed text.
- Refine login with a 20-DIP welcome heading, shorter floating labels, a 40-DIP blue Login button, reserved Caps Lock warning space, and press-and-hold password visibility that clears on release/deactivation. Add a responsive Signing in state and duplicate-submission guard; password checks retain case-sensitive BCrypt and database work remains on the UI thread.
- Clarify the Daily Sales print/PDF note: Philippine pesos, void exclusions and net sales as total sales less VAT without deducting costs/expenses. Use darker slate text for print legibility; calculations remain unchanged.
- Match the revised Daily Sales print/PDF top section: two equal summary boxes with 1-DIP black outlines and heading separators, right-aligned inset values, and five detail rows. Remove Average sale from printed output only; retain selected filters above the boxes and existing calculations/on-screen metrics.
- Make login close-only like dialogs: hide minimize/maximize and disable resizing; retain the 300 × 450 layout and authentication behavior.
- Resize login to 300 × 450 DIP and its credential fields/Login button to 240 DIP wide. Preserve centered branding, floating labels, 1-DIP styling and authentication.
- Hide all open MyPos windows, including report previews and nested dialogs, while the session is locked. Restore their existing state only after successful unlock without ending pending modal dialogs; switching users never re-exposes the previous session.
- Simplified Daily Sales page identity to `Daily Sales · sales date` plus Page X of Y. Show only selected cashier/payment/search filters once below the title; remove repeated default filters and database branch name from print scope. Printed branch identity comes solely from General Settings, without changing database branch IDs or historical data.
- Moved Daily Sales summary values farther right, ending immediately before the reconciliation group; reconciliation values now align with the report's right edge. Preserve the label indent, equal group widths and 1-DIP rules.
- Inset Daily Sales Sales summary and Invoice reconciliation values 16 DIP from their section right edges while preserving numeric right alignment, label positions and equal group widths. Widen value columns to retain room for currency amounts.
- Aligned Daily Sales summary groups to equal widths, indented detail labels by 8 DIP, and retained a minimum 16-DIP label/value gap with right-aligned values. Preserve summary padding across A4/Short/Long paper changes without altering detail-table compacting.
- Refined Daily Sales printed summaries into Sales summary and Invoice reconciliation with sentence-case labels and 1-DIP heading rules. Move Discounts into sales, label net sales as excluding VAT, separate void count/amount, and reserve value emphasis for Total sales. Calculations, invoice table and on-screen UI are unchanged.
- Reduced Daily Sales print/PDF table rules, continuation-heading rules and signature line to 1 DIP; layout, typography and paper guide remain unchanged.
- Matched the owner's revised Daily Sales sketch: report date directly below business address, standalone centered title, spaced summary groups, and compact Generated/Prepared by/Signature footer. Omit the scope line for unfiltered reports while retaining it for filtered output and preserving page identity.
- Restyled Daily Sales print/PDF using reference 1's plain paper presentation: left-aligned business details, centered bold title/date, aligned label/value summaries and black horizontal rules without shaded cells or vertical grid lines. Preserve invoice order, summary values, signature, portrait paper options and continuation headings; other reports and on-screen layouts are unchanged.
- Simplified Daily Sales print/PDF: removed the duplicated footer totals, shortened the explanatory note, and omitted the repeated date range, unused category filter and empty search label from printed scope. Keep branch/cashier/payment scope, summaries, invoice/void details and prepared-by signature; on-screen controls and CSV remain unchanged.
- Added a default VS Code solution build task (Ctrl+Shift+B) with MSBuild diagnostics in Problems and isolated, ignored `artifacts/vscode` outputs. No automatic builds or application restarts.
- Hide extra keyboard-focus adorners app-wide through the shared MyPos and default WPF focus resources. Preserve normal input/validation borders, selection, hover, active states and keyboard navigation; Windows-owned dialogs retain native styling.
- Hide the whole menu bar's inherited dotted Alt-focus outline; preserve keyboard menu navigation, the separator, and individual menu-item focus/highlight styling.
- Hide Alt-key mnemonic underlines on navigation menus while retaining the shortcuts; replace inherited dashed menu focus chrome with the shared solid 1-DIP focus outline.
- Cleared bill/coin quantity cells now immediately display `0`, matching their zero contribution to the counted cash total.
- Fixed Close Shift bill/coin counting: cleared or whitespace-only quantities count as zero without invalidating the total. Negative, fractional, nonnumeric and overflowing counts remain rejected; an empty manual counted-cash field still requires input.
- Improved Close Shift with shift/operator identity, an aligned cash summary above counting, Balanced/Short/Over feedback and a default-No closing confirmation. Verify fresh totals and close the displayed shift ID inside the transaction; reject changed totals, already-closed shifts and amounts beyond two decimal places. Keep the scrollable body and pinned actions.
- Hide the main application while the session is locked; restore it only after successful unlock. The centered lock window has its own taskbar entry, and switching users keeps the previous session hidden.
- Adjusted login content for the 400 × 450 window: 320-DIP-wide credential fields/button, larger centered logo and heading, and balanced spacing. Company name remains directly under Welcome back; floating hints and authentication are unchanged.
- Resized login to 300 × 450 with a smaller centered logo, heading, credential fields and button. Removed the sign-in subtitle so the company name sits directly below Welcome back; floating hints and authentication behavior remain unchanged.
- Restored the original centered login composition (round logo, company name, welcome text and version) and floating username/password hints by owner request. Login styling retains Inter, approved colors, 1-DIP borders and report-style button chrome; the Close Shift sizing fix is unchanged.
- Bounded the Close Shift window to the usable desktop height; bill/coin counting scrolls within the body while Cancel/Close Shift and variance/validation remain in a fixed footer. Refreshed login with compact Inter typography, gray/white surfaces, nonfloating credential fields, 1-DIP borders and a matching Sign in button; authentication behavior is unchanged.
- Audited UI wording and distinguished Shift Operations from Drawer Access Settings and General Settings. Clarified Home's Daily Sales shortcut, cash recording, invoice checks, sample receipt preview and report Print/PDF actions; removed misleading X/Z-reading labels from opening/closing forms. See [UI wording audit](ui-wording-audit.md). No route IDs, permissions or stored data changed.
- Removed the redundant Cash Movements button/dialog from reconciliation by owner request. Its movement table and print/CSV details remain; the standalone Cash Movements report remains under Reports & Inquiry.
- Fixed report-link dialog crashes caused by resolving dialog-only styles from page resources. Cash Movements from reconciliation and reconciliation from shift history now use a shared close-only report window with its own dialog resources; regression checks initialize and lay out both dialog construction paths.
- Completed the remaining approved operational report work: Product Sales/category summary, Sales Summary daily/monthly presets, Daily Sales cashier/payment/search filters, and admin-only cancellation-date Void Events.
- Added historical line category/unit/tax/discount/total/net/VAT snapshots for new sales, preserving null legacy fields and original invoices; bounded discount rounding residuals so line totals cannot go negative.
- Added structured cash-movement classifications/references/destinations and active-admin approval records; kept operational class totals separate from unverified journal mappings.
- Added optional denomination counting, saved close operator, reconciliation count breakdown, selected-shift report access from full history, and a selected-shift Cash Movements link.
- Added shared printed continuation headings, report identity and Page X of Y to preview/print/PDF. Kept portrait papers, approved report typography and the preview-only guide.
- Added [accounting-posting source review](report-accounting-review.md); no historical ledger rewrite, profit report or explicitly deferred A/R/check/case workflow was introduced.
- Added Cash Movements under Reports & Inquiry with inclusive date filters, type filter, Today/Last 7 days, refresh, cash-in/out totals, and net movement.
- Added formula-safe CSV with invariant amounts and movement/shift identifiers; reused portrait A4/Short/Long print preview and direct PDF saving.
- Enforced existing branch/drawer access on each report refresh and output. Cash movement totals exclude sales, refunds, and opening float; cash out is not automatically an expense.
- Added regression coverage for date boundaries, branch/type filtering, assignment snapshots, inactive-user denial, empty ranges, stale tracked data, CSV escaping, navigation, date clearing, and report layout.

## 2026-10-10 — Home, navigation, report printing, and documentation

### Follow-up working changes — not yet pushed

- Split System Utilities into Settings, Cash Drawer, Backup & Restore, and the existing Shortcuts hint.
- Moved admin-only drawer configuration out of Open Shift; policy changes apply to the next shift without changing an active drawer.
- Moved backup preferences and recovery actions out of general Settings, preserving verified backups and staged restore safeguards.
- Added admin-controlled shared/assigned drawer access, sale/refund drawer links and an EF schema migration.
- Added Shift / Cashier Reconciliation with live sales/payment totals, preserved original closing count/variance, cash movements, CSV and portrait print/PDF.
- Enforced active-admin void approval in the core service and updated report totals for approved voids.
- Fixed Daily Sales CSV amount formatting, date/branch metadata and text escaping; aligned default PDF filename with the selected sales date.
- Added regression tests for assignment restrictions, shared cashiers, admin-only voids, later refunds, empty shifts and CSV formatting.

### Added

- Home landing page after login and top-menu navigation with dropdowns anchored beneath their originating menus.
- Scrollable keyboard-shortcuts hint panel, accessible through its own submenu without replacing the current page.
- Daily Sales portrait paper selection: A4, Short (8.5×11 in), and Long (8.5×13 in).
- Direct Save as PDF with a file-location dialog, without requiring a print-to-PDF printer.
- Editable preview zoom percentage and a Reset zoom button.
- [Report reference audit](reports-reference-audit.md), including recommendations, data limitations, implementation priorities, and owner approval checklist. Recommendations are not implemented features.
- This history-based changelog.

### Changed

- Distinguished the active page's menu marker from an open dropdown's highlight; retained blue hover states and native menu behavior.
- Removed duplicate Daily Sales/Audit Log tabs in favor of top-menu report destinations.
- Moved the date/time display beside the application version in the status bar.
- Centralized 1-DIP UI outline/focus thickness and refined input casing: uppercase for designated POS/product fields, case-preserving searches, settings, notes, and reasons.
- Standardized regular dialogs to close-only native window chrome; session-lock safeguards remain intact.
- Set the report preview window width to 950 DIP and retained portrait-only report output.
- Printed Daily Sales now orders columns as Invoice, Time, Customer, Payment, Gross, Discount, Total, VAT, Net, Void; omitted printed Sale #.
- Increased printed report fonts and table borders without enlarging the on-screen Reports page or the preview paper-guide outline.
- Kept the Daily Sales title bold without a gray highlight; tightened company/optional branch/address header spacing.
- Moved report generation details beneath the report and added the preparer's name and signature line.
- Added a preview-only paper boundary guide; removed duplicate zoom text and the input character counter.

### Fixed

- Avoided treating inline text such as Run/Span as Visual objects during table interaction, preventing the “Run is not a Visual or Visual3D” error.
- Restored product-row double-click editing for Admin users only; cashier double-click does not open the editor.
- Applied designated uppercase handling consistently to held-sale address/SC-PWD fields while preserving cash-movement reasons and stock notes.

Source: changes since [e37c89d](https://github.com/racelle2018/MyPOS/commit/e37c89d), delivered with this checkpoint. Printed PDFs currently preserve layout using rendered page images; searchable/selectable PDF text is not claimed.

## 2026-10-09 — Compact UI and table interactions

### Changed

- Applied the WinBox-inspired compact layout, Inter typography, consistent borders, and responsive controls to Reports, Products, Shift, Users, and Settings.
- Allowed Settings to scroll while keeping Save accessible.
- Standardized POS/dialog styling and all on-screen table text to 12 DIP.
- Aligned table scrollbars with column headers and matched POS input-field styling.
- Restored native main-window controls and refined keyboard focus; POS opens focused on product search.
- Improved report date input formatting/validation and live product price/stock updates across running instances, with payment safeguards.

### Added / fixed

- Products Refresh preserves search and selection.
- Recall (F6) remains fully visible without a redundant tooltip.
- Lock-window inline errors were replaced with an owned warning dialog.

Sources: [01b0f63](https://github.com/racelle2018/MyPOS/commit/01b0f63), [e37c89d](https://github.com/racelle2018/MyPOS/commit/e37c89d).

## 2026-10-06 — POS and desktop layout refinement

- Polished responsive sale entry, current-sale actions, and readable selection states.
- Refreshed login/dialog styling and added quantity editing.
- Added POS layout sketches and UI notes to the repository.

Source: [5c053d4](https://github.com/racelle2018/MyPOS/commit/5c053d4).

## 2026-10-04 — Phase 1 UI and data safety

- Refreshed WPF screens and the sales layout.
- Hardened backup and receipt flows and preserved held-sale drafts.
- Improved shift/session handling and added regression tests and project notes.

Source: [e183da4](https://github.com/racelle2018/MyPOS/commit/e183da4).

## 2026-10-03 — Receipts, held sales, users, and audit

### Added

- Regular-printer receipt mode using FlowDocument rendering, with a Settings mode switch alongside thermal printing.
- Persistent held sales with F3 park/F6 recall, stock-clamping recall, explicit discard, and audit events.
- Audit Log viewer with date/action/user/search filters, action colors, and CSV export.

### Changed

- Username renaming with uniqueness validation and an audit trail.
- User-list inactive filter and double-click editing.
- Reports became the host for Daily Sales and Audit Log.

Historical note: the held-sales commit subject mentions “24h expiry,” but automatic expiry is not currently implemented. Held drafts remain until recalled or explicitly discarded. The regular-printer commit mentions L3110-class support; that is historical commit information, not a new hardware-certification claim.

Sources: [5dba6e6](https://github.com/racelle2018/MyPOS/commit/5dba6e6), [3c468e5](https://github.com/racelle2018/MyPOS/commit/3c468e5), [c5fbd69](https://github.com/racelle2018/MyPOS/commit/c5fbd69), [cf8646d](https://github.com/racelle2018/MyPOS/commit/cf8646d).

## 2026-10-02 — Navigation and login polish

- Added navigation icons and active-screen highlighting.
- Added a branded login card, store name, caps-lock warning, and failed-login audit events.

Sources: [a75712f](https://github.com/racelle2018/MyPOS/commit/a75712f), [4ec0c32](https://github.com/racelle2018/MyPOS/commit/4ec0c32).

## 2026-10-01 — Selection and editing behavior

- Removed double-click editing at that checkpoint and restored active selection after dialogs closed.
- Later superseded for Products by Admin-only double-click editing in the October 10 checkpoint.

Source: [51ecb50](https://github.com/racelle2018/MyPOS/commit/51ecb50).

## 2026-09-30 — Initial application and core engine

- Created the Desktop, Core, and Tests solution structure.
- Added the sale/void/stock-receiving/stock-adjustment engine and initial tests.
- Added the login window and app shell with BCrypt authentication and audit logging.

Sources: [191181a](https://github.com/racelle2018/MyPOS/commit/191181a), [ace126d](https://github.com/racelle2018/MyPOS/commit/ace126d), [372f49a](https://github.com/racelle2018/MyPOS/commit/372f49a).

## Maintenance

For future changes, add a dated entry summarizing delivered behavior and any important limitations. Link the commit after it exists; do not infer features from optimistic commit subjects or list audit proposals as completed work.
