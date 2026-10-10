# MyPos report reference audit

Reviewed: October 10, 2026 (Asia/Manila)

Status: proposals for owner review — no report implementation or removal authorized by this document.

## 1. Recommendation in brief

Keep the existing **Daily Sales** and **Audit Log**. Do not replace them with the older POS forms. Borrow the references' reconciliation, grouping, totals, and sign-off structure while retaining the approved MyPos typography and compact layout.

Recommended additions, in order:

1. **Shift / Cashier Reconciliation** — cash accountability, expected versus counted drawer, payment breakdown, and handover signatures.
2. **Cash Movements** — details of cash in/out; later distinguish expenses, deposits, withdrawals, and remittances explicitly.
3. **Product Sales with Category Summary** — one report with summary/detail options, not separate duplicate reports.
4. **Sales Summary for a Date Range** — daily/weekly/monthly views using one report definition.

Fix the identified export and data-definition issues before presenting new reports as reliable. Defer receivables, check collections, case/piece conversions, and full profit reporting until their underlying workflows are supported.

This is a software/report-design audit of supplied images and local code, not certification of accounting accuracy, tax compliance, or the other POS system. No production database records or live printer outputs were audited.

## 2. References reviewed in the supplied order

Source folder: `C:\Users\User\Desktop\reference\reports`.

| Order | File | Visible content | Recommendation |
| --- | --- | --- | --- |
| 1 | [1.jpg](C:/Users/User/Desktop/reference/reports/1.jpg) | Daily Cashiers Report: department sales, collections, disbursements, accountability, bill counts, coins, deposits, variance, and signatures | Use as the guide for a new Shift / Cashier Reconciliation report |
| 2 | [2.jpg](C:/Users/User/Desktop/reference/reports/2.jpg) | Details of Disbursements and Deposits | Incorporate as Cash Movements detail or an appendix to reconciliation |
| 3 | [3.jpg](C:/Users/User/Desktop/reference/reports/3.jpg) | Invoice list: invoice number, customer, gross, discount, invoice amount, total, preparer | Already substantially covered by Daily Sales; do not add a duplicate invoice-register page |
| 4 | [4.jpg](C:/Users/User/Desktop/reference/reports/4.jpg) | Product sales grouped by category, barcode, description, cases/pieces, quantity, amount, subtotals | Add product-sales detail with category grouping |
| 5 | [5.jpg](C:/Users/User/Desktop/reference/reports/5.jpg) | Continuation of image 4, ending with grand total | Same report as image 4, not a fifth report type |

Images 4–5 appear to be consecutive pages of the same product-sales report: matching heading, date, cashier, columns, and a grand total on image 5. This is an interpretation of the images, not confirmation of the source software's report definitions.

### Useful reconciliation visible in the examples

- Department amounts in image 1 total **300,395.00**, matching the invoice total in image 3 and product-sales grand total in image 5.
- The category subtotals visible across images 4–5 also sum to **300,395.00**.
- Image 1: `300,395.00 − 30.00 = 300,365.00` accountability.
- Image 1: `267,505.00` counted bills/coins plus `32,860.00` bank deposits equals `300,365.00`, explaining its displayed zero variance under that example's assumptions.
- Image 2: `30.00 + 32,860.00 = 32,890.00`. This is a combined list total, **not an expense total**: depositing money is not the same event as spending it.

Use these cross-report checks as a design pattern. Do not copy the example's amounts, categories, cashier, bank, or identifiers into MyPos defaults. The example does not establish rules for opening float, electronic payments, refunds, or unsettled card collections.

## 3. What MyPos currently supports

| Capability | Evidence in current code | What is missing |
| --- | --- | --- |
| Daily Sales | DailySalesView and ReportService; selected day, branch filter, invoice/customer/payment amounts, five summary metrics, reconciliation counts, details, receipt reprint, admin void | Date range, cashier filtering, dedicated payment summary, stronger export metadata |
| Printed / PDF Daily Sales | Portrait A4, Short 8.5×11, Long 8.5×13; preview zoom, direct PDF save, compact header, preparer signature | Printed page identity/count and continuation headers should be addressed; PDF filename should follow report date |
| Audit Log | Admin-only, date/action/user/search filters, CSV | Not a replacement for financial reconciliation; no printed report builder was found here |
| Shift / Cash Drawer | Opening float, cash/noncash totals, cash in/out, expected/counted drawer, variance, recent shift history | No dedicated printable reconciliation report; history view takes only the most recent 15 shifts |
| Product sales data | SaleItem stores product ID, name, barcode, quantity, price, line gross, and unit cost | No product/category report; category, unit, line discount, line VAT, and line final/net amounts are not historical SaleItem snapshots |
| Cash movements | CashMovement has shift, in/out type, amount, reason, user, timestamp | No structured deposit bank/slip, expense category, destination, approval, or denomination records |
| Inventory | Products plus purchase/sale/return/adjustment movements | No dedicated movement/count/valuation report found; movement records lack BranchId |
| Profit-related data | Sale unit costs and sales/COGS journal entries | No full expense/deposit classification or complete profit-report workflow |

Important distinction: a schema supporting a value does not mean its report, validation, permissions, or historical interpretation is already implemented.

## 4. Review of each reference

### Reference 1 — Shift / Cashier Reconciliation: add

This is the most useful missing operational report. Daily Sales answers **what was sold**; reconciliation answers **what money should be present and what was handed over**.

Recommended content:

- Company, conditional branch line, address, report title, shift ID, opening/closing timestamps, shift status, responsible cashier/operator.
- Opening float.
- Valid sales and transaction count, with separate Cash, Card, GCash, Maya, and Bank totals.
- Cash in, cash out, refunds/void-related cash effects, expected drawer, counted drawer, and over/short variance.
- Cash movement details or an optional attached detail page.
- Prepared by, checked by, and received by signature lines where a real handover occurs.
- Counted bills/coins only after denomination entry is implemented. Do not fabricate a denomination breakdown from a single counted total.

Proposed reconciliation invariant:

`Expected drawer = opening float + cash receipts + cash in − cash out − cash refunds`

`Variance = counted drawer − expected drawer`

The current ShiftService already folds some void/refund effects into its CashSales figure. A new display must explain that or separate the components; **do not subtract those effects a second time**. Deposits taken from the drawer count as cash out once, not an additional second deduction.

Do not label total sales across all payment methods as “total cash for remittance.” Keep electronic collections separate. If retained float remains in the drawer, distinguish that from cash actually handed over.

#### Data safeguards required

- Sales currently have CashierId but not CashShiftId. ShiftService associates sales by branch and timestamp, not by cashier. A shift is therefore not automatically a cashier-only report.
- Decide whether the drawer is shared or individually assigned. Prefer explicit sale-to-shift linkage for new transactions and define how historical records are handled.
- Open-shift lookup is global, and only one open shift is allowed by the current service. Do not advertise simultaneous per-cashier/per-branch drawer reporting without changing that model.
- Closed shifts persist expected/count/variance, but not every reconciliation component. Define a reproducible closing snapshot and how later voids are disclosed.
- Distinguish report generator, shift opener, person closing the shift, and cashier who posted each sale. They can be different people.
- Digital approvals need stored approver IDs/timestamps; a printed signature line alone is not an electronic approval record.

### Reference 2 — Cash Movements: add, with clearer classification

Use one searchable report, optionally printed alongside reconciliation.

Suggested columns: date/time, shift, type, category, description, amount, recorded by, reference. Add bank/destination and approval details only when they are actually captured.

Show separate subtotals for cash in and cash out. Once structured types exist, separately subtotal expenses, deposits/remittances, owner withdrawals, and other transfers.

Current CashIn/CashOut plus free-text reason can support a basic movement report now. It cannot reliably identify “bank deposit” or “expense” by keyword alone. In particular, ShiftService currently posts generic movements against OwnerDraws; do not assume those records constitute an expense ledger.

Do not copy the reference's combined disbursement-plus-deposit amount into a metric named Expenses. A deposit made directly by a customer and a deposit of drawer cash also need different source treatment.

### Reference 3 — Invoice Register: retain Daily Sales, do not duplicate

MyPos Daily Sales already includes these fields and more. Keep the approved printed order:

`INVOICE → TIME → CUSTOMER → PAYMENT → GROSS → DISC → TOTAL → VAT → NET → VOID`

Improve this existing report instead of adding another menu item:

- Cashier filter, with “All cashiers” explicitly shown when applicable.
- Payment-method summary or filter.
- Search by invoice/customer.
- Optional compact print preset if the owner wants a simpler register; do not remove the currently approved columns without approval.
- Preserve receipt-type/missing-invoice counts and drill-down, since a dash in the printed invoice column otherwise does not explain why no invoice is linked.

Do not reintroduce Sale # or a sequential printed row-number column by default; it was explicitly removed from the printed report. Keep the internal SaleNumber/ID for traceability and actions.

### References 4–5 — Product Sales / Category Summary: add after snapshots are defined

This report answers which products/categories generated sales and in what quantities. It is distinct from the invoice register and from current inventory.

Recommended detail columns:

`BARCODE / CODE | PRODUCT | UNIT | QTY SOLD | GROSS | DISCOUNT | SALES TOTAL`

Group by category, optionally show category-only summary, subtotal monetary columns, and finish with a grand total. Filters: period, branch, cashier, category, product; default excludes voided sales with a clear exclusion note.

Keep product identity based on ProductId, not just name/barcode. Names and barcodes can change; different products must not collapse into one row accidentally.

#### Historical accuracy blockers

- SaleItem snapshots name/barcode/cost, but not category or unit. Joining today's Product.Category can move old sales into a new category when a product is edited. Joining today's unit can similarly mislabel old quantities.
- Store category/unit snapshots for new sales. For existing data, label grouping as current-category grouping or “Historical category unavailable”; do not silently invent original values.
- LineGross is before discount. Summing it does not reconcile to TotalAmount when discounts exist.
- Persist final line discount, line total, line VAT/net, and tax-treatment snapshots using the same calculation/rounding path as the sale. A simplistic proportional reconstruction may be wrong for mixed tax treatment and SC/PWD cases.
- Existing sale-level money snapshots remain the authoritative invoice totals. Do not recompute historical invoices from today's prices, costs, VAT flags, or category values.
- Sum quantities only within compatible units. “10 pcs + 3 kg = 13 items” is not a meaningful stock quantity total.

Do not add **In Cases / Loose Pcs / Total In Pcs** yet: MyPos has one product unit and no historical case-conversion factor. Use Unit + Qty. Add case/piece columns only if the business sells in both units and the conversion workflow is implemented.

Category codes and department codes in the references are source-system conventions, not required MyPos fields. Start with the category model already in use; a department hierarchy can be added later if it answers a real business need.

## 5. Fixes to impose before expanding reports

### Priority 0 — correctness and traceability

1. **Fix Daily Sales CSV numeric formatting.** ExportButton_Click uses unquoted `N2` amounts. A value such as `13,080.00` contains a CSV delimiter and can shift subsequent columns. Use invariant ungrouped numeric values or correct CSV quoting; test thousands, decimals, commas/quotes/newlines in text, and spreadsheet imports. Review formula-like text before spreadsheet use; quoting alone does not neutralize formula interpretation.
2. **Include the sales date in exported data.** The sales CSV has time only; its filename carries the date. Files become ambiguous when renamed or combined. Prefer a full date/time column and explicit branch/filter metadata.
3. **Use the selected report date in the PDF filename.** SavePdfButton_Click currently defaults to today's generation date. A report for October 9 generated October 10 should not default to a filename implying October 10 sales. Generation time belongs in the footer as already approved.
4. **Document void time semantics.** DailySalesReport excludes any sale currently marked void, even if voided later. A previous day's report can change after a later cancellation. Decide whether it is a current-status sales register or an as-of closing report; add a separate void-event listing for cancellation-date review. Do not change this policy silently.
5. **Define shift linkage and cash classifications** before copying the cashier accountability or deposit forms.
6. **Define product line/category/unit snapshots** before claiming historically stable category sales or after-discount product totals.

### Priority 1 — usable printed/exported output

- Printed page number and total pages, report date/range, branch, and enough continuation identity to identify loose pages. The preview's page count is not itself printed on the paper.
- Repeat column headers on continuation pages. Current BuildReportDocument adds one heading row to the ordinary table; explicit continuation-header handling was not found. Verify with a genuinely long report rather than relying on a one-page sample.
- Show active filters in print, PDF, and CSV so users know whether totals are branch-wide, cashier-specific, or filtered.
- Preserve portrait and the three approved paper sizes. Keep readable print text; use pagination rather than forcing everything onto one page.
- Keep the recently approved compact company/conditional branch/address header, bold title without a gray title band, and generated/preparer information below the report.
- Add checked/received signature lines to handover reports only. Do not clutter every product report with remittance signatures.
- Permit a clearly labelled zero-sales/zero-movement reconciliation when necessary. Current Daily Sales print/export actions are disabled for no sales; a closed zero-sales shift can still require accountability documentation.
- CSV remains useful for spreadsheet analysis. PDF currently embeds each rendered page as an image, so it preserves appearance but is not a searchable/selectable-text export. Text-based PDF is a later enhancement, not a reason to remove direct PDF save.

## 6. Retain, simplify, and defer

### Retain

- Daily Sales, Audit Log, shift opening/closing and cash in/out workflows.
- Total Sales, Net Sales, VAT, Transactions, and **Avg. Sale**; Avg. Sale was explicitly restored by the owner.
- Void history/status, receipt reconciliation, sale details, system-receipt reprint, and privileged void checks.
- CSV, direct PDF save, paper selection, and print preview.
- Internal identifiers and original financial snapshots even when not printed.

### Simplify or omit from new designs

- A separate invoice register duplicating Daily Sales.
- Separate report entries for images 4 and 5; they are one product report.
- Repeated generation date/user lines in the header; retain one footer record.
- Large blank spacing, copied source-system department codes, decorative “End of Report” text, and permanently empty collection sections.
- Case/piece columns without conversion data.
- An expense total that combines operating expenses and bank deposits.
- Summary labels implying that Net Sales or Avg. Sale represents profit.

“Omit” means do not add these to a new layout by default. It does **not** authorize deleting saved records, removing approved existing features, or hiding exceptions such as missing invoices/voids.

### Conditional / deferred

| Candidate | When it becomes useful | Current limitation |
| --- | --- | --- |
| A/R collections and customer balances | Credit sales and subsequent payments are actually used | Customer records exist, but no receivable/collection workflow found in the inspected model |
| Check collections | Checks are accepted and tracked through clearance | No Check payment method or check-status records found |
| Denomination count | Cashier counts drawer by bills/coins | Only one CountedDrawer amount is stored |
| Bank deposits / remittance ledger | Formal bank/remittance traceability is needed | No structured destination/slip/approval record |
| Gross profit report | Owner needs margin analysis | Cost snapshots exist; validate completeness and historical line revenue allocation first |
| Full net profit report | Complete expenses and other income are captured | Generic cash-out is not an expense classification; no complete profit workflow established |
| Inventory movement / low-stock / valuation | Stock control and purchasing review | Basic data exists; branch/history and valuation rules need verification |
| Department hierarchy | Existing categories are insufficient | Product only has a category string, not a department hierarchy |

Gross profit, if later implemented, should use recorded VAT-exclusive sales less recorded COGS, not today's product cost. It is still not final net profit. Zero-cost sample/demo or opening-stock records need review before presenting margin figures as trustworthy.

## 7. Additional data-quality risks for later accounting reports

These are source-review findings, not reproduced production incidents. They should be investigated and tested before using journal data as an authoritative ledger/valuation/profit source:

- ReceiveStock currently calls PostJournal with a debit to the payment account and a credit to Inventory. This appears opposite to the intended stock-receipt direction and needs a focused accounting-posting review before ledger-based reports are built.
- AdjustStock records stock movements and an audit entry but no matching journal entry in that method. Inventory value and ledger value may not reconcile after adjustments.
- InventoryMovement has no BranchId, and receiving uses FirstBranchId for the journal. Multi-branch inventory reports are not ready merely because sales have BranchId.
- Shift noncash handling assumes the current single-payment posting path; do not advertise split-tender reporting without reviewing mixed cash/noncash allocation and refund logic.

None of these were changed during this audit. They should be addressed through separately approved, tested work, not hidden inside a visual report update.

## 8. Suggested report menu and permissions

Keep the approved top-menu/submenu navigation; no duplicate internal report tabs.

Proposed Reports & Inquiry destinations:

- Daily Sales (existing; invoice register).
- Sales Summary (date-range view).
- Product Sales (category summary/detail options).
- Shift / Cashier Reconciliation (print the selected shift; also accessible from Shift history).
- Cash Movements (can also be opened from a reconciliation report).
- Audit Log (existing; admin only).

Use filters/presets for daily, weekly, monthly, payment, and category views instead of creating a separate menu item for every variation. Keep Shift / Cash Drawer under operations for opening/counting/closing; reporting should reuse those records rather than duplicating the operational workflow.

Recommended access policy for owner approval:

- Cashier: own assigned shift reconciliation and permitted sales/product views, excluding costs and profit. If the drawer is shared, define the accessible scope explicitly.
- Admin: all permitted branch/cashier reports, cost/profit views when reliable, audit, and exception review.
- Auditor/Owner are not separate current UserRole values: only Admin and Cashier exist. Add explicit read-only roles only if needed; do not imply that printed signatures grant application permissions.
- Current Daily Sales is branch-wide, not automatically own-cashier-only. Tightening scope is a policy change and needs approval.
- Apply permissions to data queries/export/print actions as well as menu visibility.

## 9. Acceptance checks for implementation

Before a new report is considered complete:

1. Same branch/date/cashier/void policy produces the same sales total in Daily Sales, invoice detail, product/category totals, and payment breakdown.
2. Tests cover discounts, SC/PWD, VAT-exempt and mixed items, zero totals, decimal quantities, missing invoices, and large amounts.
3. Voids are visible and excluded consistently; later voids and earlier-shift refunds do not silently distort the closing report or get deducted twice.
4. Shift tests cover opening float, cash in/out, deposit cash-out, no sales, day-crossing shifts, shared-drawer users, and over/short counts.
5. Product/category history survives edits to category, unit, price, cost, name, and barcode; old unknown snapshots remain disclosed.
6. CSV columns remain aligned and include report date/scope. PDF filename/content match the selected period.
7. A4, Short, and Long portrait outputs handle many pages, long customer/product names, large amounts, repeated headers, page numbers, totals, and unbroken signature blocks.
8. No future-date sales filter, no unauthorized cost/other-cashier exports, and no mutation of original sales to make a report match.
9. Printed generated/preparer details identify the person generating the report, not necessarily the cashier being reported.

## 10. Owner review checklist

Nothing below is approved merely by appearing here. Tick or reply with the items you want implemented.

- [ ] Approve Priority 0 CSV/date/PDF filename fixes.
- [ ] Confirm current-status versus as-of-close void policy.
- [ ] Confirm shared drawer versus individually assigned cashier shifts.
- [ ] Add explicit sale-to-shift linkage and reproducible closing snapshots.
- [ ] Add Shift / Cashier Reconciliation with payment breakdown and handover signatures.
- [ ] Add Cash Movements detail with basic in/out first.
- [ ] Add structured expense/deposit/remittance classifications and references.
- [ ] Add denomination entry and print breakdown.
- [ ] Add Product Sales with category summary/detail after historical snapshot changes.
- [ ] Add Sales Summary date-range presets.
- [ ] Add printed page numbering, continuation identity, and repeating headers.
- [ ] Decide report access scopes and whether read-only Owner/Auditor roles are needed.
- [ ] Defer A/R, checks, case conversions, and full net profit until their workflows exist.
- [ ] Approve a separate review of inventory/journal posting issues before accounting reports.

Suggested first implementation batch: **export correctness, report-date metadata, and Shift / Cashier Reconciliation design/data rules**. No existing report needs to be removed now.

## 11. Source map

Local evidence inspected:

- [ReportService.cs](src/MyPos.Core/Services/ReportService.cs): daily range/branch query, totals and void exclusion.
- [Sale.cs](src/MyPos.Core/Entities/Sale.cs): invoice, cashier, customer, payment, void, and line snapshots.
- [Product.cs](src/MyPos.Core/Entities/Product.cs): current category/unit/cost/stock.
- [CashShift.cs](src/MyPos.Core/Entities/CashShift.cs): persisted shift and movement fields.
- [ShiftService.cs](src/MyPos.Core/Services/ShiftService.cs): open-shift scope, timestamp association, cash totals, closing variance, generic cash postings.
- [SaleService.cs](src/MyPos.Core/Services/SaleService.cs) and [SaleCalculator.cs](src/MyPos.Core/Services/SaleCalculator.cs): sale snapshots, discounts, VAT, cost, void reversal, receipts and adjustments.
- [InventoryMovement.cs](src/MyPos.Core/Entities/InventoryMovement.cs), [JournalEntry.cs](src/MyPos.Core/Entities/JournalEntry.cs), and [MyPosDbContext.cs](src/MyPos.Core/Data/MyPosDbContext.cs): available history/model.
- [DailySalesView.xaml.cs](src/MyPos.Desktop/Views/DailySalesView.xaml.cs): actions, CSV, printed header/body/footer.
- [AuditLogView.xaml.cs](src/MyPos.Desktop/Views/AuditLogView.xaml.cs): audit scope and export.
- [ShiftView.xaml.cs](src/MyPos.Desktop/Views/ShiftView.xaml.cs), [ShiftCloseDialog.xaml.cs](src/MyPos.Desktop/Dialogs/ShiftCloseDialog.xaml.cs), [CashMovementDialog.xaml.cs](src/MyPos.Desktop/Dialogs/CashMovementDialog.xaml.cs): operational entry and recent history.
- [ReportsView.xaml.cs](src/MyPos.Desktop/Views/ReportsView.xaml.cs), [MainWindow.xaml](src/MyPos.Desktop/MainWindow.xaml), [Permissions.cs](src/MyPos.Desktop/Permissions.cs), and [User.cs](src/MyPos.Core/Entities/User.cs): navigation/access.
- [ReportPrintPreviewWindow.xaml.cs](src/MyPos.Desktop/Dialogs/ReportPrintPreviewWindow.xaml.cs) and [ReportPdfExporter.cs](src/MyPos.Desktop/Dialogs/ReportPdfExporter.cs): paper/print/save behavior and image-based PDF generation.

Only this audit document was added for the request. No application code, source images, saved transactions, reports, or permissions were changed.
