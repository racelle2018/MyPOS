# Phase 1 implementation and acceptance

**Release qualification pending:** latest source refinements require a clean rebuild, fresh full test run and spare-PC/VM acceptance. Existing development binaries/installers are not approved for distribution. No live company was migrated.

## Live database security review — October 15, 2026

The SQLCipher service/client cutover, service-owned ProgramData storage, separate developer recovery, new/legacy backup conversion, authenticated restore, signed account recovery, technician setup/import GUI and installer service integration are implemented in source. The desktop no longer initializes a plaintext company database; it requires the protected service. New company provisioning has no known starter Owner account. Local final ZIPs remain AppData. The updated paid-test installer compiled; package/native/public-trust checks passed. See [database security migration](database-security-migration.md) and [spare-PC/VM acceptance](database-security-acceptance.md). No live database, folder ACL or Windows service was changed on this development PC. Clean-machine service/standard-user/recovery acceptance, production signing and native qualification are still required; Phase 1 is not rollout-complete. Earlier counts/status paragraphs below are historical checkpoints.

## Installer source checkpoint — October 14, 2026

Current October 15 verification supersedes the older counts below: **218 Release tests passed** (91 Core/security, 81 Desktop, 32 encrypted-storage/service, 7 Technician, 7 Programmer GUI); transitive NuGet vulnerability audit reported none. The source cutover is implemented, not installed on this development PC. Use the spare-PC/VM checklist for actual service, standard-user and recovery qualification.

Prepared `tools/Build-Installer.ps1`, a self-contained Release payload/public-trust verifier, and `tools/installer/RetailForma.iss`. One stable `artifacts/RetailFormaSetup.exe` now defaults to Program Files with administrator approval, shortcuts/uninstall, no AppData deletion or automatic POS launch, active-POS guards and downgrade refusal. Customer-facing notes use concise release wording. The programmer tools/private keys/database/backups are excluded. The current development POS output remains `artifacts/RetailForma`; company data remains per Windows profile in AppData.

After explicit owner authorization, installed Inno Setup 6.7.3 and compiled the unsigned lifetime-test installer `artifacts/RetailFormaSetup.exe` (52,248,572 bytes). Self-contained Release/public-trust validation and two installer-policy tests passed. No POS installation, startup, database migration or restore was performed. Compiler commercial licensing, production signing and isolated fresh-install/upgrade/uninstall/retained-data acceptance remain pending. Do not mark rollout accepted from compilation/static checks. See [installer build and acceptance](tools/installer/README.md).

## RetailForma 1.2.0 migration checkpoint — October 13, 2026

Owner chose data migration and confirmed no customer-issued licenses. New AppData/database, archive/log/receipt QR and signed licensing identities use RetailForma. First-start copy-only migration verifies SQLite/WAL, copied files, pending restore and an encrypted Safety backup, preserves the source, and refuses destination conflicts. Old test activations are archived and must be reissued. Original encrypted backups remain readable. Final automated verification: 131 .NET tests and 4 trial-API tests passed, with zero build warnings/errors. See [migration plan](retailforma-migration-plan.md) for prerequisites; no live cutover has been executed. Earlier version/test counts below are historical checkpoints.

October 13, 2026. Single-computer Basic tier. This is an implementation checkpoint, **not a finished deployment or accounting certification**.

## Implemented in this working tree

- AES-256 password-encrypted ZIP database snapshots with an encrypted format/size/SHA-256 manifest; exact expected entries only, bounded extraction, SQLite integrity/schema checks and verified-before-publish files. Wrong passwords/damaged archives do not stage a replacement.
- Backup & Restore recovery-passphrase setup/confirmation. The automatic-backup secret is protected for the current Windows user using DPAPI; the owner-held archive passphrase remains portable. No recovery password is logged or included in documentation/audit details.
- New manual/scheduled backups and production pre-restore safety copies use encrypted ZIP. Existing `.db` backups are preserved and remain restorable. No existing archive was converted/deleted. New backups require a configured recovery passphrase; no silent plaintext fallback.
- Local/second-folder results remain separate. A copied file is **not** evidence of successful cloud upload. Local backup/restore checks free space with a 64 MB reserve; UNC quotas are checked by actual writes.
- Google Drive and Dropbox now have independent enabled/folder settings and browsing; either/both may be used alongside the existing additional-copy folder. Copies are verified before publication, collisions are never overwritten and one failed destination does not prevent another copy. Pending markers are created before backup publication; failed folder copies retry during scheduled checks and protect local archives from pruning. Provider-cloud completion and destination retention remain external/pending.
- Offline paid issuer, hardware requests, separate paid/trial RSA trust, encrypted per-profile license cache and System Administrator activation UI are implemented. Release business-write authorization uses lifetime offline verification or fresh online trial server UTC. Vercel-compatible trial API/PostgreSQL schema are source-ready, not deployed. See [licensing implementation](licensing-implementation.md).
- Automatic retention keeps the newest verified snapshot per 30 distinct Philippine days and 8 distinct Monday-start weeks. It considers only managed `retailforma-auto-*.zip` archives whose encrypted manifest declares Automatic; manual/safety/legacy/unverifiable archives and `.cloud-pending` markers are protected. A verified new recovery copy must exist before each deletion; a failed second-folder copy skips pruning. Future cloud upload code must register its pending marker before retention can run.
- Owner display name for stored `Admin=1`, unchanged `Cashier=2`, appended `SystemAdministrator=3`; active technical accounts receive Owner access. Ordinary user management cannot create/promote a technical account; Owners cannot edit/reset/deactivate one.
- Separate initial System Administrator setup tool: Windows elevation, explicit existing database, RetailForma-closed check, exclusive SQLite locking, individual credentials, minimum 16-character passphrase, audited first-account creation only. No default/master password, migration, Owner promotion, or existing-account reset.
- Admin-only System Utilities → Setup & Recovery: persistent audited review, guidance and emergency recovery locations. Appears before first Owner review, remains accessible, does not block cashiers. Checkmarks are Owner attestations, not automated certification.
- New business timestamps/date defaults use fixed Asia/Manila wall time independently of Windows timezone. Existing timestamps are not converted; UTC remains used for elapsed backup scheduling/archive metadata.
- Corrected **future paid-purchase** posting direction using existing accounts: debit Inventory, credit the selected payment account (Cash on Hand by default). Prior journals are not edited.

## Initial technical account

Build/publish `src/RetailForma.Setup/RetailForma.Setup.csproj`, then open its GUI and approve Windows elevation:

```powershell
RetailForma.TechnicianSetup.exe
```

Close RetailForma and keep an independent verified backup first. Browse to the exact existing database for the intended Windows profile, check its company/path, enter individual masked/confirmed credentials, then confirm creation. No account password is passed on the command line or logged. An existing System Administrator causes refusal, even if inactive; additional-account/recovery policy remains undecided. The GUI rechecks Windows elevation, target/company, running-instance mutexes and exclusive database access at execution. **The tool has not been run against production.**

## Recovery limits

Keep archive passphrases outside the original computer and retain older ones after rotation. Re-save automatic-backup credentials after recovery under another Windows profile. Live/staged databases and temporary SQLite snapshots are not encrypted databases; temporary files are cleaned on normal completion/failure, but abrupt termination can leave a plaintext snapshot. Windows access protection/drive encryption remain important. ZIP filenames are visible; a password-holder can repackage edited data. Integrity verification is not accounting provenance certification.

Existing automatic backups now require configuring a recovery passphrase; failures appear in backup status instead of silently creating plaintext. The 12-hour interval remains. Old backups are not deleted when their passwords differ from the current one.

## Required before closeout

- Configure a company-controlled Google Drive or Dropbox synced backup folder and verify actual remote synchronization/independent recovery. Owner selected desktop sync instead of direct API integration for Basic; developer registrations are no longer a prerequisite for that workflow. No company data has been uploaded by RetailForma.
- Desktop-sync completion remains outside RetailForma verification. Automatic pruning affects local managed backups only, not the separate destination; destination/provider retention still needs definition. Any later direct upload integration must wire pending acknowledgments into retention. No production backup pruning was performed during implementation.
- Provision programmer-held paid/trial keys, Release public trust, Vercel trial database/HTTPS endpoint and packaging. Issuer, replacement ledger, activation/cache and online posting gate are implemented; live trial/lifetime/transfer acceptance remains outstanding. No production license was issued or installation locked/transferred. The optional portal remains Phase 2.
- Owner-approved mappings are implemented prospectively: distinct opening stock, carrying-cost adjustments and classified cash counterparts. Representative dataset/accountant reconciliation remains required. Input VAT, corporate-specific payments, supplier credit and historical journal repair are deferred.
- Fault/abrupt-termination drills; physical scanner, XP-58IIH/80-mm/L3110 and any drawer acceptance; external/cloud restore on another Windows profile/computer; receipt/compliance review.
- Actual store display/DPI review; automated WPF checks are not physical usability/printing acceptance.

See [decisions](phase1-decisions.md), [Phase 1 status](phase1.md) and [accounting review](report-accounting-review.md). No commit/push or production migration/restore is included in this checkpoint.

## Verification — previous checkpoint (October 12, 2026)

- Solution tests: **106 passed (70 Core, 36 Desktop)**, none failed/skipped. Includes the earlier backup/role/date/posting checks plus independent provider copies, collisions/partial failures/pending retries, audited folder-setting saves, signed lifetime/trial checks and authenticated encrypted-token packaging. Licensing tests do not implement or certify a deployed server/runtime gate.
- Reviewed rendered Backup & Restore and Setup & Recovery at 700-DIP width/570-DIP height; both are scrollable and preserve the compact styling. Updated provider-folder forms are captured in `C:/Users/User/AppData/Local/Temp/RetailForma-phase1-backup-folders-qa`; the UI skill kept standard fonts, compact buttons and 1-DIP borders. Automated layout also checks 1200 DIP. Actual store display/DPI and hardware acceptance remain pending.
- Temporary test/QA output: `C:/Users/User/AppData/Local/Temp/RetailForma-phase1-20261012` and `C:/Users/User/AppData/Local/Temp/RetailForma-phase1-20261012-qa`. Production startup/provisioning/migration/restore/pruning/upload/printing was not executed. Version remains 1.1.0; changes are uncommitted/unpushed.

Build: **0 warnings, 0 errors**. Git whitespace check passed.

## Implementation sources

ZIP encryption uses [SharpZipLib AES support](https://icsharpcode.github.io/SharpZipLib/api/ICSharpCode.SharpZipLib.Zip.ZipEntry.html); local automation secrets use [Windows ProtectedData](https://learn.microsoft.com/dotnet/api/system.security.cryptography.protecteddata), not machine-bound archive encryption.

## October 13 implementation additions

- Owner approved the accounting proposal. Seed new accounts1005/3990/5010/3100 idempotently; distinct opening inventory does not create a paid purchase. Proprietor-only drawings/contributions require explicit legal-form setting; company transfers require ownership confirmation. Unknown costs/categories are rejected; documented known-zero cost remains supported. No historical journal repair or multi-branch valuation.
- Opening float is a single reserve-to-till transfer. Stock adjustment UI is Owner-only, receiving distinguishes paid/opening/contribution, and cash dialogs expose required destination/counterpart confirmations.
- Financial services restore EF tracked values and discard new posting rows on failure, as well as rolling back SQLite. Reloaded stock/price remains fresh for the next checkout. Trial validation failure cannot leave a failed journal waiting to be saved later.
- Separate offline issuer supports encrypted private-key initialization, individual lifetime issuance and explicit same-entitlement replacement with an exclusive local retirement/hash ledger. No paid private key enters the POS or server.
- Desktop activation stores encrypted signed tokens using a DPAPI-protected random key. Seven-day trial activation/validation is nonce-bound and server-UTC based; offline paid validation does not contact a server. Separate signing keys prevent trial-server paid-license forgery. Debug unprovisioned developer bypass is not permitted for store distribution; unprovisioned Release denies protected writes.
- Vercel-compatible trial API source records immutable first activation and unique fingerprint in separate PostgreSQL. HTTPS deployment, keys, remote database and provider rate controls have not been provisioned. Local Node protocol tests pass (3); no live API/database integration is claimed.

Final Release suite: **118 passed (78 Core, 40 Desktop)**, no failures/skips. Node trial protocol: **3 passed**. Includes late validation failure/retry, fresh stock/price, trial invitation/server-proof, offline paid, encrypted cache and role/UI coverage. Debug and Release builds have zero warnings/errors. Git whitespace check passed. New licensing (700/1200 DIP) and stock-adjustment renders were inspected under `C:/Users/User/AppData/Local/Temp/RetailForma-phase1-accounting-license-20261013-qa`. Version remains 1.1.0; no commit/push is included. The UI skill kept new screens on existing compact fonts, scrollable layout and 1-DIP borders.

## Release acceptance boundary

### Standalone issuer GUI — October 13, 2026

Added `RetailForma.LicenseIssuer.Gui`, the only programmer-facing license-generator project, with its backend consolidated into `Backend/IssuerWorkspace.cs`. Generate license, Signing keys and Issuance history use compact Inter/12-DIP typography, 1-DIP outlines, scrolling/fixed action buttons and linked RetailForma dialog chrome/notifications. Masked passphrases are not persisted or logged. The terminal interface and separate backend project were removed at the owner's request. The GUI retains bounded-file/no-overwrite/replacement guards, rejects private-key storage inside source/customer installation and rechecks requests against the displayed preview.

Launch/publish steps are in [licensing implementation](licensing-implementation.md). Customer POS/Core projects do not reference the generator. No production key, entitlement, license transfer or POS DB operation was performed. Isolated tests create and clean up test-only encrypted keys. New layouts are rendered at 740×600, 580×370 and 1346×700 content sizes in `C:/Users/User/AppData/Local/Temp/RetailForma-license-gui-20261013-qa`; actual store DPI/hardware acceptance remains separate.

Previous GUI verification: **123 Release tests passed (82 Core, 40 Desktop, 1 standalone GUI)**. The final GUI/layout/dialog test was rerun after visual corrections. Separate programmer GUI published to `D:/MyPos/artifacts/retailforma-license-generator/RetailForma.LicenseGenerator.exe`; the complete folder is required and must not accompany customer installation. No production secrets are in that output; source/publish changes are not pushed.

Latest consolidation: the backend and its four regression tests now live in the GUI/GUI-test projects. Removed the old source project and all active references; moved only its remaining bin/obj caches and obsolete published DLL/PDB into a recoverable temporary backup. The published generator depends directly on Core, not a separate issuer assembly. Existing key files, passphrases, signed licenses and issuer ledgers are unchanged.

Consolidated Release verification: **123 passed (78 Core, 40 Desktop, 5 GUI/backend)**, no failures/skips. Republish and Git whitespace checks passed. No production data or key operation was performed.

Source implementation is not store rollout acceptance. Remaining prerequisites are authorized key/server provisioning and end-to-end trial testing, Release installer packaging, a real cloud-synced backup/independent restore, hardware/print/DPI acceptance and representative accounting reconciliation. No production DB/startup/provisioning/restore/pruning/upload/printing was performed during this implementation. Owner portal and LAN/multi-branch are explicitly Phase2, not missing Basic features to invent here.

