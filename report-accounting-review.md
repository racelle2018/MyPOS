# MyPos accounting-posting review
Reviewed: October 11, 2026 (Asia/Manila)

This is a source/test review of the approved report audit's accounting blockers, not an accounting certification or a review of the production database. No saved sale, inventory movement, or journal was rewritten.

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

## Scope decision for this report implementation

The operational report work is implemented independently. A/R, checks, case/piece conversion, gross/full net profit, inventory valuation and new Owner/Auditor roles remain deferred. Existing Admin/Cashier roles and branch-wide sales/product visibility are preserved; assigned drawer reconciliation/movement reports remain drawer-scoped. Costs and profit are not exposed by the new cashier-visible reports.

Historical corrections and account mapping require a separate approved implementation; this file does not authorize deleting, recalculating or overwriting production records.
