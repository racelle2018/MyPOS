# RetailForma accounting-posting review
Originally reviewed: October 11, 2026 (Asia/Manila). Status rechecked: October 13, 2026.

This is a source/test review of the approved report audit's accounting blockers, not an accounting certification or a review of the production database. No saved sale, inventory movement, or journal was rewritten.

## Current status at a glance

Owner approved [the sourced mapping proposal](accounting-mapping-proposal.md) on October 13. Prospective opening stock, carrying-cost adjustments and classified cash postings are implemented with conditional safeguards. This is Owner approval, not a claim of independent accountant certification.

**Operational reports and approved prospective accounting mappings are implemented. Historical reconciliation and deferred financial workflows remain pending.** Paid receipts now debit Inventory and credit the existing selected payment account. No historical journal is changed. A balanced journal is not proof that all counterpart accounts or valuations are correct.

Latest verification is in [implementation and acceptance](phase1-implementation.md), including signed posting, no duplicate float, unknown-counterpart denial and rollback tests. Tests are not accounting certification or production-ledger reconciliation.

### Implemented safeguards and reporting

- [x] Sale/void money uses recorded invoice values; admin-only void approval, stock reversal and balanced sale/void regression scenarios exist.
- [x] New sales store historical category/unit/tax/line discount/total/net/VAT snapshots; missing legacy amounts remain unavailable with warnings. Current catalog values never silently reconstruct historical invoices.
- [x] New sales/refunds link to drawers; shared/assigned access and active-user/admin guards are enforced. Original close expected/count/variance are preserved while current sales reflect later approved voids; legacy timestamp links are disclosed.
- [x] Cash movements capture operational classification, required expense/deposit/reference fields, recorded user and optional admin approval ID/time. Drawer cash-out is counted once.
- [x] Cash Movements distinguishes cash in/out/net and a cash-out-only breakdown, omits unused categories from printed totals, labels record/approval details, and retains complete raw CSV metadata.
- [x] Daily Sales, Sales Summary, Product/Category Sales, Shift Reconciliation, Cash Movements and Void Events provide operational output. Print/PDF filters, totals and page layout are cleaned up without changing posting rules or exposing costs/profit to cashiers.
- [x] Accounting blockers were reviewed and documented separately from report implementation.
- [x] QRPh is a separate recorded noncash payment alongside GCash/Maya; old payment enum values are unchanged. Like existing noncash methods, posting uses the current CashInBank placeholder account. This is not provider integration or bank-settlement reconciliation.
- [x] Admin-configurable counting minimum defaults to ₱0.25 and is enforced for new denomination quantities. Saved historical counts, manual counted cash totals and sale/VAT precision remain unchanged; this is a counting preference, not a legal-tender assertion or rounding policy.

### Accounting correction status and remaining backlog

| Item | Current status | Required completion evidence |
| --- | --- | --- |
| Stock-receipt journal direction | Corrected for future paid purchases: debit Inventory, credit selected payment account; historical entries unchanged | Regression checks signed inventory/payment changes and preserves previous lines. Accountant review of broader purchase/valuation policy remains |
| Stock-adjustment journal | Implemented Dr5010/Cr1200 shortage; investigated positive counterpart3990/proprietor3000; documented zero-cost confirmation | Approved gain/loss/counterpart accounts; signed value rules and tests for positive/negative/zero-cost adjustments |
| Movement-specific accounts | Implemented expense6000, company bank1010/reserve1005 and proprietor drawings3100; unmapped categories rejected | Approved mappings for expense, deposit, remittance, float and owner withdrawal, with no double counting and balanced posting tests |
| Branch stock ownership | `InventoryMovement` has no BranchId; stock receipt journal uses `FirstBranchId()` | Ownership model, explicit posting scope, approved schema/history migration and isolation tests |
| Historical journal repair | No identification/reversal/correction workflow implemented | Preserve originals; approved detection/reporting and audited correcting entries with reconciliation evidence |
| Split tender/refund allocation | Posting captures one payment; reader summation is not split-tender entry support | Entry/settlement rules, snapshots and mixed cash/noncash refund-allocation tests |
| Ledger/valuation/profit reports | Deferred; operational net sales is not profit | Resolve posting/ownership/history blockers and validate costs, expense capture and reconciliation before publishing financial claims |

No accounting backlog item is marked complete merely because its operational report or classification exists.

## Confirmed code findings

1. ReceiveStock increases stock quantity and weighted-average cost; new Purchase journals now debit Inventory and credit the selected payment account. The original direction mismatch is corrected prospectively. Historical erroneous journals are preserved; identification/correcting entries require separate approval. See SaleService.ReceiveStock and its signed-posting regression test.
2. AdjustStock now posts carrying-cost losses to5010 and inventory1200; positive stock requires investigated opening-clearing/proprietor contribution counterpart. Missing cost is not silently accepted as known zero.
3. InventoryMovement has no BranchId. ReceiveStock posts to FirstBranchId. The present single-store stock model cannot support authoritative multi-branch inventory valuation.
4. Cash movements now use approved classified counterparts; company ownership and entity-specific drawings guards are enforced. Opening float posts once from reserve to till. Older generic-equity journals are preserved, not retrospectively repaired.
5. PostSale snapshots one payment; readers sum stored payments correctly, but split-tender entry and refund settlement allocation are not new implemented workflows.
6. Daily/Product/Summary reports read original sale snapshots and current void status, not journal balances. Net sales and average sale are not profit.

## Required separate correction decision

- Approved stock-receipt direction and distinct opening-stock counterparts are implemented with focused tests.
- Approved carrying-cost inventory-adjustment counterpart/rounding rules are implemented; unexplained surplus and unknown cost remain rejected.
- Decide how to identify/reverse erroneous historical journal entries. Preserve originals and use audited correcting/reversing entries; never mass-edit saved journals.
- Approved structured expense/deposit/remittance/float mappings are implemented; they do not establish complete expense-ledger/profit reporting.
- Define branch stock ownership and migration rules before multi-branch stock valuation.

## Acceptance still outstanding

- [x] Owner approved the sourced prospective mappings and adjustment rules; independent accounting certification is not claimed.
- [x] Approved prospective postings have focused signed-value/counterpart and rollback tests. Representative real-data reconciliation remains below, not claimed by unit tests.
- [ ] Reconcile a representative authorized dataset containing receipts, sales, discounts, voids, adjustments, deposits and expenses. No production dataset has been audited here.
- [ ] Decide whether/how historical erroneous journals will be corrected; obtain explicit approval before any historical mutation or correcting-entry run.
- [ ] Confirm future ledger/valuation/profit labels, scope and permissions only after their underlying workflows are supported.

## Scope decision for this report implementation

The operational report work is implemented independently. A/R, checks, case/piece conversion, gross/full net profit, inventory valuation and new Owner/Auditor roles remain deferred. Structured cash-movement capture and denomination counting are implemented and must not be described as still missing; movement-specific ledger mappings are implemented, while multi-branch valuation is not. Existing Admin/Cashier roles and branch-wide sales/product visibility are preserved; assigned drawer reconciliation/movement reports remain drawer-scoped. Costs and profit are not exposed by the new cashier-visible reports. See [Phase 1 status](phase1.md) for product and hardware acceptance work.

Historical corrections and account mapping require a separate approved implementation; this file does not authorize deleting, recalculating or overwriting production records.
