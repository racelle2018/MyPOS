# Changelog

Notable MyPos changes, newest first. Historical entries are based on repository commit subjects/bodies, not GitHub push timestamps. Dates are commit dates; no release tags or version numbers are implied. New entries describe the changes delivered with this checkpoint.

## 2026-10-10 — Home, navigation, report printing, and documentation

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
