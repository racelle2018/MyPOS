# RetailForma UI wording audit

Reviewed: October 11, 2026 (Asia/Manila).

Scope: source-level review of all Desktop XAML navigation, Home shortcuts, view labels, dialog titles/actions, dynamically assigned navigation/dialog titles, and keyboard-shortcut descriptions. This is a wording review, not a live-user usability study. No stored records, route IDs, permissions, or workflows changed.

## Clarified labels

| Location | Previous wording | Current wording | Reason |
| --- | --- | --- | --- |
| Sales & Inventory menu / page guide / Home | Shift / Cash Drawer; Shift | Shift Operations | Open, count, close and review shifts, rather than configure drawer access. |
| System Utilities menu / page guide / drawer section / opening guidance | Cash Drawer | Drawer Access Settings | Admin configuration of shared or assigned access for the next shift. |
| System Utilities menu / page guide | Settings | General Settings | Store, inventory and receipt/printer preferences, distinct from drawer and backup settings. |
| Shift operation buttons and dialog headings | Open Shift (X-Reading); Close Shift (Z-Reading) | Open Shift; Close Shift | Opening-float and closing-count forms are not named X/Z reading reports. |
| Shift action / cash-entry dialog | Cash In / Out | Record Cash In / Out; Record Cash Movement | Makes the write action distinct from viewing the Cash Movements report. |
| Shift history button | Reconciliation / Print | View Reconciliation | Opens a report; printing/exporting are choices inside it. |
| Home shortcut | Reports | Daily Sales | Its existing destination is Daily Sales, not a report-selection page. |
| Daily Sales invoice-number checks | RECONCILIATION | INVOICE CHECK | Distinguishes system/manual/missing invoice counts from drawer accountability. |
| Daily Sales output action | Print Report | Print / PDF | Matches the other report actions and exposes the existing PDF option. |
| General Settings receipt action | Preview / Test Receipt | Preview Sample Receipt | Opens a sample preview first; does not immediately print or create a real sale. |

## Reviewed and retained

- Daily Sales, Sales Summary and Product Sales: invoice/day detail, period grouping and sold-product/category detail respectively. Their overlap is intentional; they are not duplicate pages.
- Shift / Cashier Reconciliation versus Cash Movements: drawer accountability versus searchable cash-in/out entries. The previously removed duplicate reconciliation button stays removed.
- Void Sale versus Void Events: an admin reversal action versus an admin cancellation-history report. Both remain distinct.
- Products versus Product Sales: current product/stock management versus historical sold quantities and sales snapshots. Receive Stock is an action, not a sales report.
- User management versus Switch user/Log out/Lock Session: account administration versus session actions.
- Backup & Restore: separate from General Settings; Backup Now, Restore Backup and Save backup preferences describe different actions.
- View Receipt, Reprint Receipt and report Print / PDF: original receipt preview, receipt reprint and bond-paper report output are intentionally different.
- Hold/Recall/Discard: pending-sale draft operations, distinct from posted-sale voiding. Draft deletion wording remains explicit.
- Repeated Refresh, Save, Cancel, Close, date presets and search labels remain context-specific controls, not duplicate destinations.
- Keyboard-shortcut descriptions already identify menus and action contexts correctly; no key bindings changed.

Technical class names, route tags, setting keys, audit action identifiers and CSV identifiers remain unchanged for compatibility. Historical documentation retains the old names where describing earlier changes.
