# MyPos accounting-posting review
Originally reviewed: October 11, 2026 (Asia/Manila). Status rechecked: October 12, 2026.

This is a source/test review of the approved report audit's accounting blockers, not an accounting certification or a review of the production database. No saved sale, inventory movement, or journal was rewritten.

## Current status at a glance

**Operational report work is implemented. Accounting-posting corrections are not implemented.** A balanced journal is not proof that debit/credit direction or counterpart accounts are correct. Report styling, classification labels and admin approval do not repair the underlying ledger.

Latest verified solution run: 79 tests passing (60 Core, 19 Desktop). This is the latest implementation regression result, not accounting certification or a production-ledger reconciliation. Appearance font family also applies to printed/PDF reports, regular receipts and receipt preview; output sizes/emphasis and native thermal encoding stay fixed. No calculation or posting rule changes are included.

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

### Not implemented — accounting correction backlog

| Item | Current status | Required completion evidence |
| --- | --- | --- |
| Stock-receipt journal direction | Still reversed in `ReceiveStock`: debit payment, credit Inventory | Approved purchase/payment mapping; correct future postings; tests for debit/credit accounts, inventory-value sign, rounding and rollback |
| Stock-adjustment journal | `AdjustStock` writes stock movement/audit but no journal | Approved gain/loss/counterpart accounts; signed value rules and tests for positive/negative/zero-cost adjustments |
| Movement-specific accounts | Cash in/out still uses generic OwnerDraws regardless of classification | Approved mappings for expense, deposit, remittance, float and owner withdrawal, with no double counting and balanced posting tests |
| Branch stock ownership | `InventoryMovement` has no BranchId; stock receipt journal uses `FirstBranchId()` | Ownership model, explicit posting scope, approved schema/history migration and isolation tests |
| Historical journal repair | No identification/reversal/correction workflow implemented | Preserve originals; approved detection/reporting and audited correcting entries with reconciliation evidence |
| Split tender/refund allocation | Posting captures one payment; reader summation is not split-tender entry support | Entry/settlement rules, snapshots and mixed cash/noncash refund-allocation tests |
| Ledger/valuation/profit reports | Deferred; operational net sales is not profit | Resolve posting/ownership/history blockers and validate costs, expense capture and reconciliation before publishing financial claims |

No accounting backlog item is marked complete merely because its operational report or classification exists.

## Confirmed code findings

1. ReceiveStock increases stock quantity and weighted-average cost, but its Purchase journal currently debits the payment account and credits Inventory. The signed inventory journal effect is therefore negative while receipt stock value increases. This is the direction mismatch identified in the original audit. See src/MyPos.Core/Services/SaleService.cs, ReceiveStock.
2. AdjustStock updates quantity and writes an inventory movement plus audit log, but creates no matching journal in that method. A nonzero-cost adjustment can change stock value without changing ledger inventory value.
3. InventoryMovement has no BranchId. ReceiveStock posts to FirstBranchId. The present single-store stock model cannot support authoritative multi-branch inventory valuation.
4. CashIn/CashOut post against OwnerDraws. The new operational Expense/BankDeposit/Remittance classifications do not silently change this old posting policy. An Expense classification is a declared operational category, not proof of a correctly posted expense ledger.
5. PostSale snapshots one payment; readers sum stored payments correctly, but split-tender entry and refund settlement allocation are not new implemented workflows.
6. Daily/Product/Summary reports read original sale snapshots and current void status, not journal balances. Net sales and average sale are not profit.

## Required separate correction decision

- Confirm the intended stock-receipt account mapping, then correct future posting direction with focused tests.
- Choose signed inventory-adjustment counterpart accounts and rounding rules before adding adjustment journals.
- Decide how to identify/reverse erroneous historical journal entries. Preserve originals and use audited correcting/reversing entries; never mass-edit saved journals.
- Map structured expense, deposit, remittance and float transfers to approved counterpart accounts before publishing expense-ledger/profit reports.
- Define branch stock ownership and migration rules before multi-branch stock valuation.

## Acceptance still outstanding

- [ ] Owner/accounting adviser approves the future-posting account mappings and adjustment valuation/rounding policy.
- [ ] Reproduce each identified posting issue with focused tests, implement the approved correction, and demonstrate signed inventory value and cash/account reconciliation—not just balanced totals.
- [ ] Reconcile a representative authorized dataset containing receipts, sales, discounts, voids, adjustments, deposits and expenses. No production dataset has been audited here.
- [ ] Decide whether/how historical erroneous journals will be corrected; obtain explicit approval before any historical mutation or correcting-entry run.
- [ ] Confirm future ledger/valuation/profit labels, scope and permissions only after their underlying workflows are supported.

## Scope decision for this report implementation

The operational report work is implemented independently. A/R, checks, case/piece conversion, gross/full net profit, inventory valuation and new Owner/Auditor roles remain deferred. Structured cash-movement capture and denomination counting are implemented and must not be described as still missing; movement-specific ledger mappings and multi-branch valuation are not. Existing Admin/Cashier roles and branch-wide sales/product visibility are preserved; assigned drawer reconciliation/movement reports remain drawer-scoped. Costs and profit are not exposed by the new cashier-visible reports. See [Phase 1 status](phase1.md) for product and hardware acceptance work.

Historical corrections and account mapping require a separate approved implementation; this file does not authorize deleting, recalculating or overwriting production records.
