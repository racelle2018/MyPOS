# Accounting mappings — approved prospective implementation

Researched October 12, 2026; owner approved the proposal on October 13, 2026. Prospective mappings and the safeguards below are implemented. Owner approval is not independent accountant certification or a production-ledger audit. Input VAT, corporate/shareholder treatment and full valuation remain subject to professional review; no historic journal was rewritten.

## Authoritative reference principles

Inventory purchase/attributable costs form inventory cost; interchangeable goods may use weighted-average costing. Inventory losses/write-downs are expenses, not sale revenue. Measurement also requires consideration of net realisable value; RetailForma currently has no complete write-down/valuation workflow. [IFRS Foundation: IAS 2](https://www.ifrs.org/issued-standards/list-of-standards/ias-2-inventories/).

Owner contributions/distributions are distinct from income/expenses. Accordingly, an owner's personal withdrawal is not automatically a store operating expense. [Official Conceptual Framework, paragraphs 4.68–4.70](https://www.ifrs.org/content/dam/ifrs/publications/pdf-standards/english/2021/issued/part-a/conceptual-framework-for-financial-reporting.pdf). This publicly accessible PDF is an older issued edition, not a claim to reproduce the latest consolidated standard; the [current Foundation overview](https://www.ifrs.org/issued-standards/list-of-standards/conceptual-framework/) was also checked.

Policies and corrections need judgment based on the actual transaction; historic errors are not repaired by guessing a universal adjustment account. [IFRS Foundation: IAS 8 overview](https://www.ifrs.org/issued-standards/list-of-standards/ias-8-basis-of-preparation-of-financial-statements/). RetailForma must preserve original records and use separately authorized correction/reconciliation workflows.

The entries below are **proposed implementation mappings inferred from those principles and the existing RetailForma chart**, not entries copied from an authoritative mandatory chart. Numeric codes are RetailForma conventions, not prescribed by the cited guidance.

## Existing relevant RetailForma accounts

`1000` Cash on Hand; `1010` Cash in Bank; `1200` Inventory; `3000` Owner's Equity; `5000` Cost of Goods Sold; `5100` Cash Over/Short; `6000` Operating Expenses. `5100` currently represents drawer variance, not a universal stock-loss account. Noncash/e-wallet receipts currently share the bank placeholder; this is not settlement reconciliation.

## Proposed future-posting mappings

| Business action | Debit | Credit | Condition / unresolved decision |
| --- | --- | --- | --- |
| Paid stock purchase | `1200` Inventory | Selected `1000` cash / `1010` bank | Direction is already corrected. Unit cost must be approved inventory cost; recoverable input VAT/freight needs separate treatment, not invented here. |
| Opening stock migrated from existing books | `1200` Inventory | Proposed `3990` Opening Balance Clearing | Use verified quantities/carrying cost and reconcile against the full opening trial balance. Clearing is not sales or cash paid; it must not remain unexplained. New account/workflow require approval. |
| Actual owner contribution of stock | `1200` Inventory | Approved equity/contribution account | Different from migrated opening balances. `3000` is only a candidate where the entity's approved chart makes it appropriate. |
| Confirmed stock shortage/damage | Proposed `5010` Inventory Loss (expense) | `1200` Inventory | Use approved carrying-cost valuation, quantity evidence, reason and Owner approval; do not use selling price or drawer variance by default. |
| Stock surplus/correction | `1200` Inventory | Counterpart determined from cause | Investigate missed receipt/contribution/error first. No automatic "gain income" rule for every positive count adjustment. |
| Immediately paid operating expense | Approved expense account, e.g. `6000` or a detailed subaccount | `1000` cash / selected payment account | Only actual current-period expenses; deposits, advances, inventory and owner withdrawals are not automatically expenses. Tax treatment remains separate. |
| Deposit of till cash into the company's bank | `1010` Cash in Bank | `1000` Cash on Hand | Internal asset transfer, not income/expense. Separate fees if any; require bank/reference evidence. |
| Remittance to the same company's reserve/other cash location | Approved destination cash asset account | Source cash asset account | Receiver/ownership must be established. Proposed `1005` Cash Reserve if splitting till/reserve accounts is approved; third-party payments require a different counterpart. |
| Float supplied from company reserve / returned to reserve | Destination cash asset account | Source cash asset account | Requires till/reserve ownership/subledger definitions. Avoid recording the opening-float amount and a cash movement as two transfers. Owner-funded float is a different contribution/loan transaction. |
| Owner's personal withdrawal — sole proprietor | Approved drawings/equity account (candidate new `3100`) | `1000` Cash on Hand | Not an operating expense. Corporation/shareholder payments may instead settle an approved liability/dividend/receivable; do not apply a sole-proprietor rule universally. |

Bank/reserve transfer entries above are an inferred bookkeeping design for movements within the same entity; they are not profit events. Cash-flow classification and account ownership still need review. [IFRS Foundation: IAS 7](https://www.ifrs.org/issued-standards/list-of-standards/ias-7-statement-of-cash-flows/).

## Proposed valuation/implementation safeguards

- Keep weighted-average receipt costing and decimal arithmetic. Proposed adjustment valuation is approved carrying unit cost × signed quantity; monetary postings round to 2 decimals while cost precision stays at the existing 4 decimals. These precision choices are RetailForma design proposals, not standard-mandated values.
- Genuine zero-cost stock needs documented approval; missing legacy cost must not be silently treated as known zero. Do not manufacture historical cost from today's catalog.
- Use distinct opening-inventory versus paid-purchase actions, explicit counterpart/branch ownership and approval/audit records. Never trigger a new cash payment for migrated opening stock.
- Reject unsupported/unmapped classifications instead of silently posting every movement to Owner's Equity. Correct new postings only; no bulk historical journal rewrite is authorized.
- Adopt new account codes only after review; do not rename historical accounts or imply VAT/profit reporting is complete.

## Approval checklist

- [x] Opening-clearing and sole-proprietor contribution/drawings safeguards approved; unsupported corporate-payment mappings remain deferred.
- [x] Proposed new accounts `3990`, `5010`, `1005`, `3100`, or approved replacements.
- [x] Positive/negative/zero-cost adjustment valuation, reasons and approving user.
- [x] Expense categories, bank/reserve destinations, float semantics and remittance recipient ownership.
- [ ] Input VAT and inventory cost policy; supplier credit remains deferred.
- [x] Prospective implementation and signed-value/rollback/no-double-counting tests.

Approved prospective code: paid receipts Dr1200/Cr1000 or1010; opening stock Dr1200/Cr3990; proprietor stock contribution Dr1200/Cr3000; shortages Dr5010/Cr1200; investigated surplus counterpart3990 or proprietor3000. Expense6000, company deposit1010, company reserve1005, proprietor drawings3100; unsupported categories are rejected. Opening float transfers reserve1005 to till1000 once. Zero carrying cost requires documented Owner confirmation. CompanyLegalForm must explicitly be SoleProprietor before personal drawings/contributions. All new accounts are seeded idempotently without renaming old accounts. Approval does not authorize historical repair, supplier credit, invented input VAT or multi-branch valuation.
