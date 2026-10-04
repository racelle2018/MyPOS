# MyPos — System Concept & Development Handoff

## 1. What MyPos Is

**MyPos** is a commercial, offline-first Point of Sale + accounting system for small Philippine retail stores (sari-sari stores, mini-marts, single-branch shops). It is being built as a product to sell, not an internal tool. Its differentiators:

1. **A real double-entry accounting engine** under a simple POS — every sale auto-posts balanced journal entries (revenue, VAT, COGS, inventory), so the owner gets a true P&L, not just "total sales"
2. **Offline-first** — local selling works without internet; cloud sync is planned for Phase 2, not implemented yet
3. **Two receipt modes** — Mode A (store issues manual BIR receipts, software is internal tracking, no accreditation friction) and Mode B (system-issued receipts with BIR header fields) — a settings toggle, per the store's compliance maturity
4. **Statutory correctness** — PH VAT-inclusive pricing, Senior Citizen/PWD discount law (RA 9994/10754), gapless invoice numbering

**Target platform:** Windows 10/11 (Home or Pro — irrelevant, the store PC is just a client). .NET 10 LTS.

## 2. Current State (repository check: 2026-10-04)

### Implemented in the current repository — Phase 1 + Phase 1.5

This is a code-and-test inventory, not a claim that every screen, printer, or compliance workflow has passed real-hardware acceptance testing.

**Phase 1 (core product):**

- Login (BCrypt, caps-lock warning, failed-login audit; the database currently seeds `admin` / `admin123` and requires that starter password to be changed on login). There is no `SetupWindow`.
- App shell: light theme, left icon navigation rail, page header and status area; the main window starts maximized while login and session lock stay compact. The POS view is persistent (cart survives navigation).
- Product catalog: CRUD, live search, receive stock (weighted-average cost + journal entry), activate/deactivate (never delete), stock color coding (red=out, amber=≤threshold, threshold configurable), role-based UI
- POS selling screen: invoice/OR number (required in Mode A, yellow, duplicate detection, unique per branch), customer (find-or-create, snapshot on sale), address, payment mode and order type, live date/time status bar, scan/search, cart with editable quantity (buttons + typing, stock-clamped), light totals panel with live VAT split, payment dialog, and sale-complete screen with print status
- Accounting engine (`SaleService`): `PostSale`, `VoidSale` (reversing entries, stock restored), `ReceiveStock`, `AdjustStock` — all transactional, rollback-safe
- `SaleCalculator`: single source of truth for money math, shared by UI and engine (screen can never disagree with journal)
- Daily sales report: sales book, summary cards, receipt reconciliation (system/manual/missing/voids), day stepper, CSV export, A4-landscape print with preview, void flow (admin-only, double-confirm, reason required)
- Audit log viewer: filters (range/action/user/search), color-coded actions, CSV export; Reports screen is a hub (Daily Sales | Audit Log)
- Receipt printing: ESC/POS thermal mode and FlowDocument regular-printer mode, with a receipt preview/retry path. In system-receipt mode the POS attempts to print automatically after saving; a failed print opens the preview for retry. Do not describe this as preview-first or as an F8 reprint shortcut unless those behaviors are implemented and verified.
- Shift management (Z-reading): opening float (X-reading), cash in/out during shift, counted-vs-expected variance posting to "Cash Over/Short" (5100), shift-gated selling, shift history
- Senior/PWD discount: statutory computation (VAT-exempt AND 20% off the net), SC/PWD ID capture, no stacking with regular discounts
- Held sales: park (F3) / recall (F6) with SQLite persistence, stock clamping on recall, explicit discard, and audit events. Automatic expiry is not implemented.
- User management: create/edit, roles (Admin/Cashier), password reset, activate/deactivate (never delete — identities own history), username rename with audit trail, self-lockout protection (can't demote/deactivate yourself), last-login tracking, inactive-user filter
- Session safety: logout cart-discard warning, 5-minute idle lock (password resume, switch-user flow, no Alt+F4), F12 manual lock, unlock-failure auditing
- Settings: store details, BIR accreditation fields, receipt mode + printer type + printer dropdown + paper width, low-stock threshold, backup-now button, test print
- Hardening: EF migrations (schema self-upgrades), SQLite WAL + lock timeout, rotating Serilog logs, automatic SQLite backups (`VACUUM INTO`, checked on a 12h schedule), optional second backup copy, staged restore, and global crash handling. Automatic backup retention is not implemented.

### Test suite — 20 tests passing (checked 2026-10-04)

`dotnet test MyPos.slnx --configuration Release --no-restore` passed: 17 Core tests and 3 Desktop tests. Coverage includes sale/accounting invariants, discounts, held sales, shift behavior, and desktop checks. Treat these tests as a regression contract; add tests for new behavior.

## 3. Architecture

```
MyPos.slnx
├── src/MyPos.Core/          ← NO UI dependencies, ever
│   ├── Entities/            ← User, Branch, Product, Sale/SaleItem/Payment,
│   │                          InventoryMovement, Account, JournalEntry/Line,
│   │                          AuditLog, Setting, Customer, CashShift/CashMovement,
│   │                          HeldSale
│   ├── Data/                ← MyPosDbContext, DbInitializer (Migrate + seed), migrations
│   └── Services/            ← SaleCalculator, SaleService, ReportService,
│                              ShiftService, HeldSaleService
├── src/MyPos.Desktop/       ← WPF: views, dialogs, controls, converters, printing
├── tests/MyPos.Tests/       ← xUnit Core tests (17 passing)
└── tests/MyPos.Desktop.Tests/ ← xUnit Desktop tests (3 passing)
```

**The Core/Desktop split is sacred.** Core = entities + engine + all business rules. Desktop = UI + hardware only. Phase 2's API references the _same_ Core — this is why the split exists.

**Planned additions (Phase 2):** `src/MyPos.Api` (ASP.NET Core), `src/MyPos.Web` or separate repo (Next.js dashboard). Cloud DB: PostgreSQL (Npgsql). Hosting: a Linux VPS (DigitalOcean/Azure) for API; Vercel for dashboard.

## 4. The Accounting Engine — Rules That Must Never Break

1. **Money is `decimal`, never float/double.** SQLite stores decimals as TEXT → **always `ToList()` before filtering/aggregating in queries; compute in memory**. This convention is everywhere; keep it.
2. **VAT (12%): prices are VAT-inclusive.** Net = Round2(gross ÷ 1.12); **VAT = gross − net** (derived by subtraction, never independently computed — cannot drift). VAT rate is snapshotted per sale.
3. **Senior/PWD (statutory):** VAT-exempt price ÷ 1.12 first, then 20% off the net. Both, always. Reported as a discount (gross − net). No stacking with amount discounts; SC/PWD ID required.
4. **Double entry per sale:** Dr Cash-on-Hand (or Cash-in-Bank) [total] · Dr COGS [cost] · Cr Sales Revenue [net] · Cr VAT Payable [vat] · Cr Inventory [cost]. Balanced by construction.
5. **Append-only ledger.** Sales and journal entries are never edited or deleted. Corrections = reversing entries (voids). This also makes sync idempotent and conflict-free.
6. **Stock quantity is a cache; `InventoryMovements` is the source of truth.** Every stock change writes a movement row.
7. **Costing = weighted average**, recalculated on every stock receipt.
8. **Gapless numbering:** `Sale.NextSaleNumber` / `NextReceiptNumber` per branch, incremented inside the posting transaction; unique index `(BranchId, SaleNumber)` enforces it. Voids do NOT release sale numbers; they DO release invoice/OR numbers (spoiled paper logic).
9. **Snapshots on sales:** ProductName, prices, VAT split, customer name/address — recorded sales never change when master data changes later.
10. **Chart of accounts (seeded):** 1000 Cash on Hand · 1010 Cash in Bank · 1200 Inventory · 2000 VAT Payable · 3000 Owner's Equity · 4000 Sales Revenue · 4100 Sales Discounts · 5000 COGS · 5100 Cash Over/Short · 6000 Operating Expenses. Non-cash payments post to Cash-in-Bank as a placeholder until dedicated e-wallet accounts exist.
11. **Shift variance (Z-reading):** expected = float + cash sales + cash-in − cash-out − voided-cash; variance posts to 5100. One shift per store at a time (single-terminal assumption).
12. **IDs are GUIDs everywhere** — collision-free for future multi-branch sync.

## 5. UI/UX Design System

- **Direction:** keep MyPos light and task-focused. The VS Code reference informs navigation density and hierarchy, not a dark-theme requirement. The current WPF app uses MahApps.Metro's Light.Blue resources with local, readable styling.
- **Palette:** window background `#F4F7FB`, light surfaces, blue accent `#2878BD`, muted blue-gray borders, and restrained status colors. Receipt-mode yellow remains a distinct signal.
- **Global styles in `App.xaml`:** `MyPosButton`/`PrimaryButton`/`HeaderButton`, TextBox and PasswordBox, ComboBox, DatePicker/Calendar, and DataGrid. Controls use compact sizing and 6px corners; TextBox watermarks come from `Tag` through MahApps' watermark property. DataGrid selection is pale blue, with a subtler unfocused state and no black focus box.
- **Style/converter keys:** `RightCell` (right-align money columns), `UpperBox` (uppercase entry where required), `StockBrushConverter` (threshold-aware), `UpperTextConverter`. There is no `UpperCell` style in the current resources.
- **Icons:** Segoe Fluent Icons / Segoe MDL2 Assets (built into Win10/11, zero packages)
- **Attached behaviors:** `DataGridBehaviors.DeselectOnOutsideClick` handles click-away selection clearing where enabled.
- **Dialogs:** the current project still uses WPF `MessageBox` in multiple flows. There is no custom `MyPos.Desktop.MessageBox` class; any future shared dialog treatment must be implemented and checked across screens.
- **Selection spec:** hidden affordance → silent no-op; visible/attempted action → loud warning + audit log. Permission checks live at the **action layer** (`Permissions.RequireAdmin`), never only at hidden buttons.
- **Casing policy:** product/sale data entry = forced uppercase (frontend `UpperBox` + backend `.ToUpperInvariant()` normalization); user identity (usernames, full names) and settings/company fields = natural case; passwords always case-sensitive, never transformed.
- **UI acceptance target:** meaningful empty states, keyboard/focus behavior, accessible labels, no clipped fields or buttons, and usable layouts at common Windows scaling settings. **Color = signal, not decoration** (gray normal, red investigate). These need screen-by-screen QA rather than an unverified blanket claim.

## 6. Compliance Context (Philippines)

- Manual-receipt mode (Mode A) exists so stores adopt the software without BIR accreditation friction; system-receipt mode (Mode B) prints "THIS SERVES AS YOUR OFFICIAL RECEIPT" + ACCR/MIN/SN fields only when configured
- Old BIR software _accreditation_ was removed (~2018-era reforms), but computerized bookkeeping systems are generally still expected to be _registered_ with the store's RDO — stores must verify with their RDO; rules have changed multiple times, so verify current requirements rather than relying on old sources
- Senior/PWD discount law implemented per §4.3; if targeting drugstores, the ₱100/week medicine cap is a known follow-up
- Reconciliation report (POS sales vs receipts issued) exists partly to keep stores audit-clean

## 7. Known Limitations & Tech Debt (honest list)

- Cart `StockAvailable` is snapshotted at add-to-cart; service re-validates at commit (no oversell possible; UI staleness only)
- No item-level returns/refunds (void whole sale + re-ring is the current path)
- No purchases module yet → no input-VAT tracking, owner draws informal
- Held sales have no automatic expiry; old drafts remain until recalled or explicitly discarded
- Automatic backup retention/pruning is not implemented; the 14-day setting applies to logs, not database backup files
- Monthly VAT summary report (feeds BIR 2550) not built
- QTY cell affordance decision deferred until real-cashier soak testing
- No driver-level paper-out sensors (deliberate — mixed-brand ROI is poor; fail-visible + one-click-recover instead)
- Receipt spacing tuned in preview only until thermal hardware soak
- Phase 1 lock guards against opportunistic misuse, not technical attackers — local machine = local trust until Phase 2 server auth

## 8. Roadmap

### Phase 2 — Cloud API + Sync (next up)

**Build order: sync contract document first, then API skeleton, then sync, then dashboard.**

1. `MyPos.Api` — ASP.NET Core (.NET 10), PostgreSQL, JWT auth (owner accounts + device registration)
2. **Sync contract (the boundary both sides build against):**
   - **Catalog flows DOWN** (cloud = master): products, prices, users, settings, licenses
   - **Transactions flow UP** (POS = master): sales, journal entries, movements, shifts, audit logs — all append-only, GUID-keyed, **idempotent** (safe to retry)
   - **Stock never syncs as a value** — both sides derive from movements (conflict-free by design)
   - Server stamps `ServerReceivedAt` → audit-grade timestamps (local clock is display only)
   - Offline queue lives in SQLite; uploads drain on reconnect; failures retry, never block selling
   - **The store must never be blocked from selling by the cloud. Ever.**
3. Owner dashboard (Next.js + React, Tailwind, Recharts; deploy Vercel): daily sales per branch, inventory, receipt reconciliation, voids, shifts/variances, license management
4. Multi-branch data model (BranchId already everywhere; per-branch stock arrives here — add BranchId to movements)

### Phase 3 — Licensing (commercial protection)

- PC fingerprint = hash(motherboard serial + CPU ID + Windows Machine GUID)
- Activation: license key + fingerprint → server binds and returns RSA-signed token (expiry, features) stored locally (AES-256)
- **Grace period 7–14 days offline** before hard-lock (phone-home-every-boot generates support tickets)
- License transfer via owner portal (deactivate old fingerprint → activate new) — motherboards die; make it self-serve
- Remote deactivation for non-payment

### Phase 4/5 — Growth

- Purchases module (suppliers, input VAT → output−input VAT due), advanced reports (monthly VAT, top products, inventory valuation), and further backup/restore usability and off-site recovery validation
- GCash/Maya/card terminal integration, e-wallet accounts in CoA
- BIR e-invoicing when the store's RDO requires it

## 9. Working Rules for This Repo (hard-won)

1. **Keep the test suite green.** As checked on 2026-10-04, 20 tests pass (17 Core, 3 Desktop); the count should grow as behavior is added.
2. **Never use float for money.** Never edit/delete sales or journal entries. Always audit security-relevant actions.
3. **Always `ToList()` before in-memory filtering** of EF queries with decimals (SQLite TEXT storage).
4. **New UI screens:** follow the light MahApps-based `App.xaml` styles, use `RightCell` on money columns, provide empty states and clear focus behavior, and check layouts at different window sizes and display scaling. Keep XAML and C# readable; do not minify.
5. **Permission checks at the action, not just hidden buttons.** Cashier-visible features: selling, receive stock, held sales, shift. Admin-only: products CRUD, voids, users, settings, audit log, reports.
6. **New schema changes = EF migrations** (`dotnet ef migrations add X --project src/MyPos.Core --startup-project src/MyPos.Desktop`), commit the Migrations folder, and let the app apply them. Preserve the existing one-time legacy-database baseline path; do not add ad hoc ALTER statements for new changes.
7. **Money display:** `₱{0:N2}`, right-aligned. Uppercase for product/sale data.
8. **Commits after every screen/feature.** Small checkpoints beat big bangs — this project has survived lost-work incidents because of this rule.
9. **Dev workflow:** use `dotnet build MyPos.slnx` and `dotnet test MyPos.slnx` to verify changes even when IDE diagnostics are stale. Preserve uncommitted work and prefer small patches over whole-file replacement. Do not run the app and DB Browser against the same SQLite database simultaneously; save and close DB Browser first.
10. **Visual Studio Community setup:** install **.NET desktop development** for the current WPF application. Install **ASP.NET and web development** for the planned Phase 2 API; it is not needed just to run Phase 1. Open `MyPos.slnx` and use the `MyPos.Desktop` startup project. VS Code plus the .NET SDK remains a supported alternative.

---
