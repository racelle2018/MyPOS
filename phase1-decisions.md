# Phase 1 decisions and open questions

## Database security storage approval — October 15, 2026

- Owner approved service-owned `%PROGRAMDATA%\RetailForma\Data` for the working database, with restricted access. Local encrypted backup ZIPs remain in AppData.
- Owner also approved SQLCipher Community, built from maintained upstream source.
- Owner approved developer-assisted recovery: unique per-installation database keys, a recovery copy encrypted to a separate developer public key, authorized account-password resets, and developer recovery for future backups when the owner forgets the backup passphrase. The recovery private key stays outside the POS/installer/repository and is separate from licensing keys. Existing backups cannot gain this capability without being opened and recreated. Use controlled repairs, safety backups and audit records, not a shared master password.
- This is a planned storage/security migration, not a completed deployment. Existing data and permissions have not been changed. See [database security migration](database-security-migration.md) for approved policy and implementation/acceptance gates.

## Latest approval and implementation — October 13, 2026

- RetailForma rename sequence: owner approved internal code/project rename first, then data/licensing identity cutover. Owner explicitly chose **migrate existing data**, not discard it, and confirmed **no customer-issued licenses yet**. Copy-only migration with encrypted Safety backup is implemented/tested; live cutover and recovery acceptance remain pending. Old test licenses must be reissued with the existing key pair. See [migration plan](retailforma-migration-plan.md).

This section supersedes earlier checkpoints saying mappings/activation are unapproved or entirely unimplemented; history remains below.

- Owner explicitly approved the sourced [accounting mapping proposal](accounting-mapping-proposal.md). Prospective opening stock, carrying-cost adjustment and classified cash mappings are implemented with conditional safeguards. Independent accountant certification, historical repair, supplier credit and multi-branch valuation are not implied.
- Paid licenses are programmer-issued offline lifetime per device, with no internet renewal. Seven-day trials require online, signed server UTC; no local-clock/offline-grace extension. Owner portal remains separate Phase 2.
- Offline issuer/replacement ledger, distinct signing trust, encrypted activation/cache and Release business-write gate are implemented. Vercel trial API source is ready for deployment testing, not deployed. System Administrator access does not confer paid signing authority.
- Actual keys, authorized hosting/database and end-to-end acceptance remain required. Reports/recovery remain available when posting is denied. Old permanently offline replacement tokens cannot instantly be remotely revoked.
- Basic backup uses verified copies into Google Drive/Dropbox synced folders. Actual provider cloud completion and independent recovery must be verified externally.

See [implementation and acceptance](phase1-implementation.md). No live deployment, license transfer or production-data operation was performed.

Created: October 12, 2026 (Asia/Manila).

Latest status: owner answers and subsequent chat clarifications have been incorporated. Earlier answers are preserved below; the current decision summary and explicit clarification/status lines take precedence over the original recommendations.

This is the shared reference for the owner's decisions about finishing Phase 1. It records the owner's stated requirements, not automatic approval of every suggested implementation. Read it together with [Phase 1 implementation and acceptance status](phase1.md), [accounting review](report-accounting-review.md), and [notification audit](notification-audit.md).

## How to use this reference

- **Confirmed requirement:** explicitly stated or accepted by the owner.
- **Recommendation — not approved:** suggested by the assistant; still needs a decision.
- **Open question:** required scope or policy information is missing.
- **Implementation/acceptance status:** tracked in `phase1.md`; a confirmed requirement is not necessarily implemented or tested.
- Update the relevant decision/question when the owner answers, and add a dated entry to the decision history. Do not silently convert recommendations into approvals.
- Never put passwords, recovery keys, cloud tokens, or account credentials in this file.

## Confirmed owner decisions

| ID  | Area                 | Owner's decision                                                                                                                             | Boundary / status                                                                                                                                                                           |
| --- | -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D01 | Deployment | Phase 1 is the Basic tier: one computer per company, operating offline or online. A later Multi-computer tier will use a branch-local server. | Phase 1 scope is confirmed. Shared-terminal operation is later work, not an implemented capability. |
| D02 | Branches | A later Multi-branch tier will use a server at each branch and a central server receiving branch data; the owner can manage master data centrally. | Future product direction approved; synchronization, branch stock ownership and central-change distribution need a separate design and implementation. |
| D03 | Backups | Support both Dropbox and Google Drive. Admin may enable either or both, with separate upload status and explicit authorization of company accounts. | Provider selection behavior is confirmed. Cloud app registration, account connections, upload/verification/retry work are not implemented or authorized merely by this record. |
| D04 | Backup protection | Use AES-256 encrypted ZIP backups, a strong recovery password and the approved independent recovery/credential-handling approach. | The recovery approach is approved; implementation and restoration compatibility testing remain. Do not store secrets here or rely solely on the original Windows profile. |
| D05 | Setup checklist      | The checklist is admin-only, appears during first setup, remains accessible afterward, and must not block cashiers.                          | Existing starter-password change remains. The checklist itself is not yet implemented. It must not reset existing company data.                                                             |
| D06 | Roles | Add a full-application-access System Administrator role for the programmer. Owner manages business settings; licensing, protected installation settings and database maintenance/migration controls are System Administrator-only. Keep Cashier and the approved staged approach to later roles. | Role and settings-category split approved. Account provisioning/recovery, licensing workflow and granular technical controls still need definition. |
| D07 | Regional settings | Use PHP/peso only and Asia/Manila because the product targets the Philippine market. | This latest decision supersedes the earlier configurable currency/timezone requirement. Preserve historical records; do not implement foreign-currency or arbitrary timezone options. |
| D08 | Accounting approvals | Phase 1 supports distinct paid-purchase and opening-inventory workflows; supplier credit and partial supplier payments are deferred. Owner approval of policy and accountant/bookkeeper review follow the accepted recommendation. | Scope approved; exact posting/account mappings and the defects in the accounting review remain unresolved. No historical journal rewrite approved. |
| D09 | Scanner | Wired 1D laser scanner with programmable barcode input and Enter suffix, previously identified by the owner as Symbol Z5700V. | Actual connection mode/model identification and real scan/focus behavior still need acceptance testing. |
| D10 | Report printer       | The owner has an Epson L3110 for printing reports.                                                                                           | Physical report output and supported paper/scaling settings need acceptance testing.                                                                                                        |
| D11 | Receipt printer | Use Xprinter XP-58IIH initially and also support compatible 80-mm receipt printers through configuration. | Initial model selected; model-specific paper/printable width, connection and commands require physical acceptance. Changing a setting cannot change a printer's physical capabilities. |
| D12 | Cash drawer | A generic drawer with an RJ11-style connection may be used; integration can be added later. | Exact model, wiring, voltage and printer pulse compatibility remain unverified. Ordinary checkout must not depend on drawer hardware. |

## Already approved application baseline

Preserve the existing approved behavior while doing Phase 1 work:

- Owner-approved compact WinBox-inspired UI; default Inter, controlled appearance presets, no color customization, and 1-DIP borders.
- Report/regular-receipt/receipt-preview font family follows the saved selection, while output sizes stay fixed and thermal encoding stays native.
- Shared action-result dialogs with Windows sound-scheme feedback; ongoing field/status information stays inline.
- Login is 350 × 450 DIP with 280-DIP fields/button, centered branding and floating credential labels.
- Admin-configured Shared/Assigned drawer access and minimum counting denomination; cashiers do not change drawer policy while opening a shift.
- Admin approval for sale voids; reports reflect approved voids while original closing cash/variance snapshots remain preserved.
- QRPh is an additional payment method; GCash and Maya remain separate options.
- Existing operational reports, portrait paper options, direct PDF saving, historical snapshots and selected-filter presentation remain.

These are existing approved features, not confirmation that multi-computer operation, cloud backup or accounting correctness is complete. Use the current source and implementation checklist for evidence.

## Questions, original recommendations and owner answers

The original recommendation wording is retained as discussion history. Explicit owner responses and later clarifications below determine approval; a remaining “not approved” label in the original text does not override a subsequent approval. Approval of scope/policy does not mean implementation or acceptance is complete.

### Q01 — What must Phase 1 actually deliver?

Must **working simultaneous multi-computer and multi-branch operation** be delivered before Phase 1 is complete, or should Phase 1 finish the current local product and deliver those capabilities in the next stage?

The requirement to eventually support both is already confirmed. This question is about delivery timing, not removing the requirement.

**Recommendation — not approved:** finish a clearly labeled single-computer Phase 1 first, including backup/recovery safeguards, approved future-posting fixes and real-store acceptance. Deliver simultaneous terminals and multiple branches as the next architecture stage, with explicit company/branch/terminal identities and branch stock ownership. This preserves the confirmed scalable-product goal without claiming the current app already supports it. If multiple terminals or branches are needed for the first live deployment, expand Phase 1 and complete/test that architecture before sign-off instead.

**Answer:** Ok I approved you're recomendation we fucos on one computer per company first, then next phase or if these is already ok we do the multi computer per company like a local network and multi branch if the company have many branch on different location that will be the type of these pos tier like basic, multi computer and multi branch that customer can choose to avail.

**Current decision:** Approved. Basic/single-computer is Phase 1; LAN multi-computer and multi-branch are later product tiers. Pricing/licensing terms for these tiers are not yet defined.

### Q02 — What offline operation is required?

When internet access is lost, should checkout continue through a working branch-local server? Must each POS also continue independently if its connection to the branch server fails? Or may checkout stop until the connection returns?

These are different requirements. Independent offline selling needs synchronization, conflict handling, stock rules and invoice-numbering decisions; a cloud backup is not synchronization.

**Recommendation — not approved:** for the future multi-terminal stage, keep checkout working when internet is down as long as the branch-local server and local network still work. Initially stop posting new sales when a terminal loses its branch-server connection; preserve the draft and offer a clear retry path. Add independent terminal-offline checkout only after synchronization, duplicate-post protection, invoice allocation and stock-conflict handling are implemented and tested. Keep database access behind the server rather than opening a shared SQLite file from each terminal. See [SQLite's network guidance](https://www.sqlite.org/useovernet.html).

**Answer:** Yes I approved these approach for 1 computer it can work offline and online but for multi computer they will a server and when the server is not reachable on local network the system cannot open on the client pc so that the system no trasaction open to prevent error, duplicate entry and etc. but the system can still open on the server. is these good decission? and multi branch they have a server each branch same dicission with the multi computer but these have a main server on the branch, that all branch server push the data to these server, where the owner can update, add and etc. what do you think?.

**Q02 status:** Approved future direction: clients must not enter normal transaction operation without a reachable branch server; the server workstation can operate if its service/database remain healthy. Each branch has a local server and sends data to the central server. A restricted disconnected/retry screen, mid-transaction duplicate protection and bidirectional master-data synchronization were recommended during review; detailed design remains later work. No independent client-offline posting is approved.

### Q03 — Which cloud provider and company account?

Which provider should hold backups: Google Drive, OneDrive, Dropbox, or another provider? Will each company use its own owner-controlled account?

Direct upload versus a synchronized folder can be proposed after the provider is selected. Do not connect an account or upload data based only on this document.

**Recommendation — not approved:** use the cloud provider the company already owns and trusts, under an owner-controlled company account, rather than a cashier's or programmer's personal account. Implement one selected provider first, with encrypted uploads, background retries and separate local-backup/cloud-upload status. Require evidence of remote upload before reporting cloud success; a file placed in a local sync folder is not sufficient by itself. Keep the provider replaceable later and keep checkout independent of cloud availability. A specific provider cannot be selected until the owner identifies the company's account/preference.

**Answer:** Dropbox and Google drive backup file should have a strong encryption, such as an AES-256 encrypted ZIP.

**Latest owner clarification:** “support both providers and allow the admin to enable either or both, with separate upload status. Company accounts will need an explicit authorization flow.”

**Current decision:** Approved. Both providers are supported targets; either or both may be enabled. The original suggestion to implement only one selected provider is superseded. Connecting accounts/uploading real data still requires the normal explicit authorization flow.

### Q04 — Which encrypted format and recovery responsibility?

Does the owner prefer an AES-256 encrypted ZIP, another encrypted archive, or a RetailForma-managed encrypted backup package? Who will hold the recovery password/key and an independent safe copy?

**Recommendation — not approved:** prefer an AES-256 encrypted ZIP to match the requested archive workflow, using a strong, unique recovery password and integrity checking, not legacy ZIP password protection. Choose a maintained implementation and test creation/restoration compatibility before adopting the format. Let the owner keep the recovery credentials in a secure password manager plus an independent safe recovery copy; protect any locally stored automation secret separately. Restore must work on another computer/profile, so do not rely solely on a machine-bound secret. Password/key rotation must preserve the ability to restore older backups. Missing recovery credentials may make backups unusable, and someone who can decrypt a backup can still edit its contents. See [WinZip's encryption guidance](https://kb.winzip.com/en/130831).

**Answer:** Ok i approved these recommendation.

**Current decision:** Approved: AES-256 encrypted ZIP and the recommended recovery/secret-handling approach. Do not solicit or record the actual recovery password in this file.

### Q05 — Backup schedule and retention

Is the existing **12-hour automatic backup interval** acceptable? Approve or change the proposed **30 daily and 8 weekly retained snapshots**, with manual and pre-restore safety copies excluded from automatic pruning.

The retention policy below has now been accepted by the owner. Local/cloud retention and low-space handling still need a safe, tested implementation; no existing backups have been deleted by recording this decision.

**Recommendation — not approved:** keep the current 12-hour automatic interval initially. Retain a verified daily snapshot for each of the latest 30 days and a verified weekly snapshot for each of the latest 8 weeks; a snapshot may satisfy both tiers. Exclude manual and pre-restore safety backups from automatic pruning and give the admin visibility of their storage use. Prune only app-managed, eligible snapshots after a new backup is verified, never the only good recovery copy. A failed cloud upload must not discard a pending second copy. Show low-space warnings and preserve separate local/cloud results. Make policy changes admin-controlled and audited; adding a final backup at shift close can be considered separately.

**Answer:** Ok I approved these recomendation.

**Current decision:** Approved: 12-hour interval, 30 daily/8 weekly retention, manual/pre-restore exclusions, verified-copy safeguards, protection of pending cloud copies and separate local/cloud status. Implementation remains pending; a shift-close backup is not included in this approval.

### Q06 — Purchases, supplier credit and accounting approval

For the first delivery, should stock receiving support only paid purchases and opening inventory, or also purchases on supplier credit? If credit is needed now, are partial supplier payments needed too?

Who will approve the posting/account mappings: the owner alone, or the owner with an accountant/bookkeeper's review?

**Recommendation — not approved:** start with distinct opening-inventory and paid-purchase workflows unless supplier credit is required immediately. Correct the known future-posting defects before relying on the ledger. Have the owner approve business policy and an accountant/bookkeeper review account mappings and inventory-cost treatment; inventory cost is an accounting policy, not just a payment-method label. Let authorized staff choose the business action and let approved mappings determine postings. Require reasons and owner/delegated approval for adjustments that change stock/value outside normal sales or receipts. If credit is needed now, include supplier bills, balances, due dates and payment allocations, including partial payments if required; do not expose an “On credit” option without those records. Preserve original historical journals and obtain separate approval for any correcting entries. See [IAS 2 inventory guidance](https://www.ifrs.org/issued-standards/list-of-standards/ias-2-inventories/).

**Answer:** Ok I approved these The known future-posting defects in `report-accounting-review.md` remain unresolved.

**Latest owner clarification:** Paid purchases and opening inventory now; supplier credit later — explicitly confirmed.

**Current decision:** Workflow scope and the recommendation are approved. Exact account mappings, focused posting corrections and acceptance are still outstanding; historical journals remain protected.

### Q07 — Additional roles and branch access

Should Branch Manager and read-only Auditor/Accountant roles be added now, later, or not at all? Which actions may a manager approve, and should staff be restricted to assigned branches?

**Recommendation — not approved:** retain Owner/Admin and Cashier for the initial local delivery, but make access permission-based rather than scattering role-name checks. Add Branch Manager when multi-branch operation is delivered, restricted to assigned branches and explicitly delegated approvals. Add a read-only Auditor/Accountant role when there is a real reporting-access need; do not imply financial reports are ready merely because the role exists. Owner/Admin keeps company-wide authority. Cashiers/managers must not automatically gain restore, cloud-credential, currency-policy or company-wide permission-management rights. Use individual accounts and record the actual approving user.

**Answer:** Ok I approved these but I need a new role for the programmer.

**Latest owner clarification:** Replace the proposed restricted Programmer role with **System Administrator**, with all application access for the programmer to manage protected settings, licensing and other technical functions. Some technical settings must not be editable by Owner.

**Current decision:** System Administrator and the settings-category split below are explicitly approved. Do not implement the previously proposed restricted support role. Full application access does not itself approve hidden credentials, deletion of audit evidence or automatic historical-data rewrites.

### Approved Owner and System Administrator settings split

**Approved role names:** **Owner** for the existing business Admin role, **System Administrator** instead of Super Admin for the full-access technical role, and **Cashier**. This is a naming clarification only; the approved permission split is unchanged. Existing application roles/data have not been renamed or migrated by updating this reference.

The owner explicitly approved this split in chat after the role clarification. System Administrator retains access to all Owner functions as well as the protected technical functions.

| Settings/functions | Owner may manage | System Administrator may manage |
| --- | --- | --- |
| Business details and products | Yes | Yes |
| Ordinary business-user management | Yes | Yes |
| Appearance and printer settings | Yes | Yes |
| Backups and drawer policies | Yes, through approved safety/authorization workflows | Yes, through the same safety/authorization workflows |
| Licensing | No | Yes |
| Protected installation settings | No | Yes |
| Database maintenance/migration controls | No | Yes |

Cashier permissions are not broadened by this approval. System Administrator account creation/promotion/recovery is a separate policy decision, not an implied permission for Owner to grant themselves the role. Naming maintenance controls does not authorize an actual migration, restore or historical-data rewrite without its required safeguards and scope.

### Q08 — Currency and timezone scope

Should there initially be one base currency per company, with a default timezone and optional branch timezone? Or must different branches use different currencies / accept foreign-currency payments immediately?

**Recommendation — not approved:** default PHP and Asia/Manila for this company. Start with one base currency per company and a company timezone default; support explicit branch timezone overrides when branch-aware reporting is delivered. Make these admin-controlled settings, with currency selected during setup and changes after monetary transactions exist requiring a controlled migration. Currency formatting, decimal precision and counting denominations must be consistent; preserve historical currency and business-day identity instead of relabeling/re-dating old records. Define new timestamp storage and legacy compatibility before implementing timezone changes. Defer foreign-currency tender, exchange rates and cross-currency consolidation until separately specified and tested.

**Answer:** Use peso only and use Asia/Manila because these sytem is for philiphine market.

**Current decision:** Approved: fixed PHP and Asia/Manila. This supersedes the original configurable currency/timezone request and the earlier recommendation for optional branch timezones.

## Remaining implementation-policy details

### Hosting/accounting clarification — October 12, 2026

- **Latest hosting choice:** Vercel for testing first, with a later upgrade for commercial rollout. No account/project/endpoint is provisioned and no paid subscription is authorized yet. Testing must use isolated data/test keys, and Hobby eligibility must be checked against Vercel's commercial-use restrictions. See [licensing implementation](licensing-implementation.md). This supersedes InfinityFree as the intended test host without marking licensing or the Phase 2 portal implemented.

- Owner can create a free InfinityFree domain, rather than providing an existing deployed license API. Provider research found free hosting blocks desktop/API access; actual API-capable hosting/endpoint is still needed. See [licensing implementation](licensing-implementation.md).
- Owner requested trustworthy online accounting references instead of supplying signed-off mappings. See [accounting proposal](accounting-mapping-proposal.md). Research/proposed codes are not approval; remaining accounting corrections stay pending review.

### Hardware-replacement license transfer — approved October 12, 2026

- A motherboard/computer failure permits transfer of the existing lifetime entitlement, without purchasing another license. This is replacement, not permission to use two devices simultaneously.
- The client supplies the existing license key and new device fingerprint. The programmer verifies ownership, authorizes replacement and issues a new signed token under the same license, retiring the old fingerprint/entitlement.
- The failed device does not have to connect or successfully deactivate. Single-PC Basic clients may contact the programmer directly; a future owner portal may accept requests, without granting clients license-signing authority.
- One active device per license remains the contractual restriction. A permanently offline old computer cannot be remotely disabled immediately; clients must cease using the replaced device. Retirement records must not be described as guaranteed offline token revocation.
- Policy approved; issuance, transfer records/auditing and enforcement have not been implemented. No actual license/device was transferred by this documentation update.

### Single-PC owner portal — confirmed October 12, 2026

- One POS computer may be configured **with or without** the optional Phase 2 owner portal; portal access is independent of terminal count.
- The portal lets the owner check uploaded sales and update approved master data, including prices, remotely. Internet is required for synchronization, not lifetime-license renewal.
- During an internet outage, the single-PC POS continues selling with its last synchronized data. Pending sales and owner updates synchronize when connectivity returns; historical invoice snapshots must remain unchanged and retries must not duplicate transactions.
- This does not broaden the future LAN-client offline policy: a client still requires its branch server for normal transactions. Detailed portal permissions, synchronization/conflict rules and commercial portal terms remain separate work.
- This is an approved requirement, not a claim that the owner portal or synchronization has been implemented.

### Latest implementation answers — October 12, 2026

- **Initial System Administrator:** approved separate local setup tool with individual credentials, no shared/default technical password and no automatic Owner promotion. See [implementation checkpoint](phase1-implementation.md). Additional-account/recovery policy is not implied.
- **License model:** programmer-issued, single-purchase lifetime license per device, with the approved hardware-replacement transfer above. Single-PC Basic works offline permanently after activation, with no renewal validation/expiry; the optional portal needs connectivity only for synchronization. Trial lasts 7 days and requires internet license validation; expired trials lock until paid activation. The earlier offline-grace/non-payment proposals do not apply to offline lifetime Basic. Activation infrastructure, signing-key custody and transfer workflow still need implementation.
- **Accounting mappings:** owner will provide accountant/bookkeeper mappings. Opening-inventory, adjustment and cash-movement mappings remain pending. The identified future paid-purchase reversal is corrected with existing accounts, without historical repair.

These latest answers supersede earlier bullets/history describing initial provisioning and license-model selection as unanswered. Phase 1 is not marked complete by recording them.

The eight original scope questions now have owner answers. The following details must not be silently assumed:

- **Granular technical controls:** the settings-category split above is approved. Define individual protected installation/maintenance actions and their safeguards before exposing controls; do not move additional business settings away from Owner without a new decision.
- **System Administrator provisioning/recovery:** initial separate local setup tool with individual credentials is approved and implemented; no ordinary Owner self-promotion or hidden/default password. Additional technical accounts and recovery policy remain undecided.
- **Licensing:** trial/lifetime and hardware-replacement policy are confirmed above. Only the programmer issues paid/replacement licenses; an application System Administrator role does not itself confer signing/issuance authority. Activation, trial timing/lock behavior and audited transfer implementation remain outstanding. No license enforcement has been implemented.
- **Accounting mappings:** specify/review the actual approved accounts and adjustment valuation rules before changing postings. Scope approval is not proof the current postings are correct.
- **Cloud backup:** latest owner choice is a dedicated encrypted-backup folder synchronized by Google Drive or Dropbox desktop software, rather than direct RetailForma API uploads for Basic. RetailForma may confirm a verified folder copy, not remote sync completion. Developer registrations/OAuth are not required for this chosen folder workflow; company sign-in/provider sync and an independent restore drill remain necessary. Direct integration is optional later work.
- **Hardware acceptance:** select/verify the exact 80-mm printer and drawer specifications. Cashier/Owner will perform acceptance, but the separate test computer/profile and responsible tester for the restore drill still need identification.

## Later hardware and acceptance details

These can be answered when hardware is selected, but must be verified before the corresponding feature is declared ready:

- Exact scanner model label and connection mode; barcode input and Enter-suffix behavior.
  Answer - Wired laser barcode scanner that can be programm barcode input and Enter-suffix.
- Thermal printer model, connection, supported command set, roll width and actual printable width.
  Answer - Xprinter 80mm or 50mm these can be change on the setting.
  Latest owner clarification - Xprinter XP-58IIH for now, with support for compatible 80-mm printers through settings.
- Cash drawer model/connection if drawer integration is brought into scope.
  Answer - It connection is via RJ11
- Who will perform the cashier, physical printing and independent restore acceptance tests, and which test computer/profile can be used.
  Answer - Cashier, Admin/ Owner account.

### What “restore on another computer/profile” means

Install RetailForma on another computer, or under a separate Windows user account, using an isolated test data location. Retrieve the cloud backup, decrypt it with the recovery credentials, restore it and compare products, sales, users, settings and reports with the expected test dataset. Do not overwrite the live installation.

This proves the backup can recover the business without relying on the original computer/profile. Downloading a file or passing a local integrity check alone does not complete this acceptance test. In a future server deployment, the restore drill must also cover the server database and required configuration.

## Future architectural design notes

The branch-server/central-server direction is approved for later tiers; the detailed mechanisms below are design safeguards, not a claim of delivered support or authorization to modify production infrastructure.

- Use an application server/shared transaction service for simultaneous terminals; do not let multiple computers directly open a live SQLite file on a network/cloud-sync share.
- Separate cloud backup from branch/terminal synchronization.
- Add explicit company/branch/terminal identities and branch inventory ownership before claiming multi-branch correctness.
- Decide offline behavior before choosing the deployment/synchronization architecture.
- Keep business settings under Owner permissions and reserve the agreed technical settings for System Administrator; audit changes without silently recasting historical records. PHP/Asia/Manila are fixed under the latest decision.
- Do not declare Phase 1 complete from test counts alone. Resolve the selected delivery scope, posting issues and acceptance checks.

## Decision history

### October 12, 2026

- Owner confirmed the scalable product direction, cloud/encrypted backups, admin-only reusable setup checklist, configurable timezone/currency and the available/planned hardware listed in D01–D12.
- Owner requested accounting/approval suggestions and an explanation of independent restoration; those recommendations and remaining questions are recorded separately from confirmed decisions.
- Owner requested this persistent reference. No new app feature, cloud connection, historical-data change or backup deletion was performed by creating this file.
- Q01–Q08 remain unanswered. Proposed server architecture, extra roles, encryption format, retention policy and accounting mappings have not been marked approved.
- Owner requested recommendations beneath every open question. Q01–Q08 now include a concrete recommended approach and its boundaries; all remain recommendations, not approved decisions.

### Later owner answers and clarifications — October 12, 2026

- Owner answered Q01–Q08 in this file; these later answers supersede the earlier history entries saying all questions were pending.
- Confirmed Phase 1 as single-computer Basic, followed by LAN multi-computer and multi-branch tiers; approved the future branch-server/central-server direction.
- Confirmed Dropbox and Google Drive support with either/both enabled, separate upload status and explicit company-account authorization.
- Approved AES-256 encrypted ZIP recovery approach and 12-hour/30-daily/8-weekly retention safeguards.
- Explicitly confirmed paid purchases/opening inventory now and supplier credit later; accounting fixes/mappings are still not implemented.
- Replaced the restricted Programmer proposal with a full-access Super Admin role and protected technical/licensing settings. Exact permission/provisioning/license policies remain to be agreed.
- Fixed the market scope to PHP and Asia/Manila, superseding earlier currency/timezone customization.
- Selected XP-58IIH initially, required 80-mm support and identified the drawer connection as RJ11-style.
- This update changes decision documentation only; no roles, licenses, cloud connections, backup pruning, accounting postings or application behavior were changed.

### Settings-category split approved — October 12, 2026

- Owner approved Owner/Admin management of business details, products, users, appearance, printers, backups and drawer policies.
- Owner approved Super Admin-only management of licensing, protected installation settings and database maintenance/migration controls; Super Admin also retains all ordinary application access.
- Super Admin provisioning/recovery, detailed license rules, accounting mappings and acceptance prerequisites remain unresolved. This approval did not implement the role or authorize a production-data operation.

### Role names approved — October 12, 2026

- Owner approved **System Administrator** in place of **Super Admin** following the naming recommendation. The business role is labeled **Owner** and the checkout role remains **Cashier** in the approved naming scheme.
- Permissions remain unchanged: System Administrator has all application access, including the exclusive technical/licensing controls; Owner retains the approved business-setting access.
- Earlier history uses the previous labels to preserve the decision sequence. Account provisioning/recovery and licensing rules remain unresolved. This update changes documentation only, not existing accounts, stored role values or application behavior.
