# RetailForma migration sequence

Owner approved step 1 first, followed by step 2, on 2026-10-13. POS version is 1.2.0.

## Step 1: code/project rename

Use `RetailForma.slnx`, `src/RetailForma.*` and `tests/RetailForma.*`. Assemblies/namespaces, DbContext names, WPF pack/resource/style identities, references and developer commands follow these names. No production startup or database operation is required. Preserve database table mappings and migration IDs/history.

Verification: renamed solution built with zero warnings/errors; 124 .NET tests and 3 trial-API tests passed. POS, generator and technician packages were rebuilt with RetailForma assemblies. Published POS is 1.2.0 and the lifetime-test public trust hash is unchanged. Login/generator rendered fixtures confirm the renamed font/image/resource links load. Cleanup of IDE-locked generated folders remains pending editor reload.

Generated output cleanup is recoverable under `artifacts/retired-mypos-builds-*`. Some old obj folders are held by the IDE; reload the new solution and close references to old projects before final cleanup. Never delete the external test/private key folders, issuer history, or AppData database as build cleanup.

## Step 2: data and licensing identity migration — implemented, live cutover pending

Owner confirmed **migrate existing data**, not discard it, and **no customer licenses have been issued yet**. These decisions supersede the earlier open scope questions. Code and isolated tests are implemented; no live profile was migrated by the agent.

### Current behavior

- New active data is `%APPDATA%/RetailForma/retailforma.db`; new logs, archive names, receipt QR prefix and instance lock use RetailForma.
- On first launch under the existing Windows profile, the POS offers a Yes/No migration before opening EF. Original `%APPDATA%/MyPos` remains intact. Existing destination folders are never merged or overwritten.
- A recovery passphrase must already be saved in the old database **on that same Windows profile**. If it is missing or DPAPI cannot decrypt it, migration stops safely. Set it in the old app's Backup & Restore before cutover. No passphrase is requested in chat.
- SQLite backup captures committed WAL data into an isolated staging database. Validate schema/integrity, verify copied file hashes and unchanged source data version, preserve settings/backups/logs/pending restore, then create and verify an AES-256 Safety ZIP. Publish the complete folder atomically; failure deletes only the unpublished staging copy.
- New signed licenses/trial responses explicitly contain `Product: RetailForma.Basic`. Old tokens without this signed field or with another product are rejected, not edited. New cache envelope version is 2 with `RETAILFORMA-LICENSE-2` AES associated data.
- Old test activation files are copied into `legacy-activation/licensing`, not the active cache; originals and signing keys/issuer ledger are retained. Export a fresh device request and issue/import a new test license with the same existing public/private key pair. Do not reset trial start history.
- Existing password-encrypted backups containing `mypos.db` remain readable. New archives use `retailforma.db`; retention only prunes verified new managed automatic archives, not migration Safety/manual/old archives.
- A transitional old-client mutex remains solely to prevent simultaneous old/new use during migration; it is not the new app identity. Do not run the old client after cutover, because its original data is now a rollback copy, not a synchronized database.

### Live acceptance still required

Backup passphrase changes are now guarded: existing saved passphrases require the current value plus new/confirmation; first-time configuration needs only the new pair. Unreadable DPAPI secrets cannot be silently replaced. Old archived builds do not receive this source fix and must not be used after cutover. An authorized cross-profile/lost-passphrase recovery procedure is separate; it is not a bypass in normal Save.

Close the old POS, keep the same Windows profile, verify the saved recovery passphrase, launch the rebuilt package and explicitly approve migration. Confirm sales/journals/stock/users/settings against the old installation and test recovery from the encrypted safety ZIP. Activate with a fresh RetailForma test license. Keep originals until acceptance; do not delete AppData, keys or backups to remove brand strings.

- Existing data must be migrated. Record exact paths, not broad user/workspace roots.
- Stop the POS and verify a consistent encrypted backup, SQLite WAL state and restore on an isolated profile before moving `%APPDATA%/MyPos/mypos.db` to a RetailForma path. Preserve sales, journals, GUIDs, settings, pending recovery and backups; never initialize a blank database over existing records.
- Preserve the current Windows-user context for DPAPI. Old test caches are archived intact instead of being silently decrypted/re-encrypted into new active licenses.
- Changing signed product identity `MyPos.Basic` requires replacement signed tokens and invitations from the authorized issuer/server. Do not modify signed payloads in place or reset immutable trial starts to grant extra trial time. No private signing keys are requested in chat or embedded in the POS.
- Confirm whether a transition supports old licenses/backups or deliberately rejects them after replacement. Coordinate trial API deployment/records and already distributed client builds.
- Coordinate old/new single-instance locking during cutover, backup archive readers, receipt QR compatibility and technical setup. Keep a rollback package until verified acceptance.
- Do not delete old profiles, keys, backups or issuer journals merely to remove an old brand string. Retire validated duplicate installation/build files only after cutover acceptance.

No step 2 production data, cache, server or license mutation has been performed.

Final verification: 131 .NET tests passed (80 Core, 46 Desktop, 5 GUI/backend), 4 trial-API tests passed, and Release builds/publishes completed without warnings/errors. The paid public verification key hash remains unchanged. A status read during QA created an empty RetailForma/backups folder; status reads are now non-mutating, and the verified file-empty folder was recoverably moved to `C:/Users/User/AppData/Roaming/RetailForma-empty-before-migration-20261013`. No files/database were removed. The actual existing-company migration has not been launched.
