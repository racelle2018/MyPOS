# Protected database upgrade — spare PC/VM acceptance

**Release qualification is pending.** Rebuild verified packages in a clean environment and record their hashes before starting this checklist. Existing development outputs are not approved distribution packages.

Use only disposable company data until every applicable check passes. A second Windows profile on the development PC is **not** a separate service installation. The owner selected a spare PC or VM for these tests; no remote test machine was connected or changed by the development work.

## Packages and prerequisites

- Installer: `D:\MyPos\artifacts\RetailFormaSetup.exe`.
- Technician: `D:\MyPos\artifacts\RetailForma.TechnicianSetup\RetailForma.TechnicianSetup.exe`.
- Programmer: `D:\MyPos\artifacts\retailforma-license-generator\RetailForma.LicenseGenerator.exe`; keep this on your controlled programmer PC, never with the client.
- Current qualification build uses the existing **paid test** public key plus the supplied separate recovery public key. No trial endpoint is configured in this package. Do not use test entitlements as customer production licenses.
- Keep private signing/recovery keys and their passphrases outside customer packages. Copy only installer and the controlled technician package to the test PC.
- Have an elevated Windows installer account and a separate **standard Windows user** for daily POS tests. RetailForma roles are separate from Windows permissions.

## 1. Fresh installation

1. Snapshot the disposable VM or record the spare-PC test state. Confirm no real company is present.
2. Install normally into `C:\RetailForma` with Windows administrator approval. The installer must not launch the POS, create a company, activate a license or import data.
3. Check Windows Services: `RetailFormaData` / RetailForma Protected Data is Running, Automatic, using `NT SERVICE\RetailFormaData`, not LocalSystem or the cashier account.
4. Run Technician Setup elevated; use Initial setup → Connect. Review the protected destination, enter a dummy company and individual technician credentials (16–256-character password), confirm the company and create it.
5. Confirm creating a second new company or replacing an existing technical account is refused. Do not create a company if testing import instead.
6. Open the POS normally as the standard Windows user. Sign in with the individual System Administrator and create an individual Owner, then a Cashier. `admin/admin123` must not be usable on a fresh service-provisioned company.
7. Restart Windows and repeat sign-in. This checks real service identity/profile loading and persistent DPAPI access; build tests alone do not prove it.

## 2. Lifetime licensing and normal work

1. System Administrator → Licensing → export device request. On the programmer PC issue a **test lifetime license** with the matching existing paid test signing key. Import the signed result on the POS.
2. Disconnect the test PC from the internet and restart it. Verify lifetime selling still works; reports and recovery must not require an online license renewal.
3. Create products, paid stock receipts, opening stock, a drawer/shift and a recognizable sale. Exercise held-sale recall, approved void, stock adjustment and close-shift counting.
4. Compare Daily Sales, product/summary, shift, cash-movement and audit outputs. Verify balances, quantities, identifiers and historical snapshots; do not substitute UI-only success messages for data checks.
5. Verify Cashier cannot manage accounts, technical activation, recovery phrases or privileged changes. Owner cannot grant or reset technical access. Disabling an account or changing its password must invalidate its old service session.

## 3. Standard-user security boundary

1. As the standard Windows user, attempt to browse/read/copy/change `%PROGRAMDATA%\RetailForma\Data\retailforma.db` and `key-state.json`. Access must be denied, including through the parent folder. Do not change ACLs to make a test pass.
2. Stop the service using the elevated Windows account, then start the POS normally. It must report the unavailable protected service, not create a database in AppData or start a blank company. Start the service again; do not delete business files.
3. Check installer/uninstaller refuse active POS processes in every Windows session. Complete work and close normally; never kill the POS during a posting.
4. Lock with a report/dialog open. Only the lock surface should be visible. Repeat with the service unavailable: prior screens must stay hidden; switch users/reconnect instead of exposing the previous session.
5. Local Windows administrators remain able to control the machine. Do not describe this as protection from an administrator or a security certification.

## 4. Backup, restore and independent recovery

1. Owner → Backup & Restore → Set passphrase. Use a disposable unique 16–256-character phrase; keep its independent record.
2. Change passphrase must ask for the correct old value before allowing new/confirmation. Wrong current value, stale dialog version and mismatching confirmation must leave the saved value unchanged.
3. Create a backup. Confirm a verified encrypted ZIP under `%APPDATA%\RetailForma\backups`; it must not contain a readable database. Enable either/both provider folders and confirm per-destination copy status. Check cloud completion in each provider's application separately.
4. Restore a recognizable test backup. Wrong phrase, truncated/archive damage, altered metadata and an incomplete upload must not change the live company. Use the actual error reported; unauthenticated ciphertext does not always distinguish a wrong phrase from damage.
5. Review the verified company/account/sale preview before accepting. Restore must preserve an encrypted safety snapshot, replace atomically, sign out every old service session, and require reopening/sign-in. Set the automation passphrase afterward; imported DPAPI secrets must not be reused.
6. On a second disposable installation, restore an original legacy two-entry ZIP using its **original** phrase, a new-format ZIP, and an old `.db` snapshot. New machine/profile automation secrets must be established locally. Verify records and accounting totals after each test.
7. For a forgotten new-format backup phrase, on the controlled programmer PC use Developer recovery → recover backup with the matching separate recovery private key. Produce a new ZIP/new owner phrase, preserve the original and restore the result on the test PC. Wrong/unavailable keys must not rewrite the input. Old-format ZIPs still need their original phrase.

## 5. Developer-authorized account reset

1. Technician Setup elevated → Account recovery → enter the exact active test username → Export recovery request.
2. Programmer GUI → Developer recovery → review company, account, installation and recovery-key ID → sign using the matching separate encrypted recovery private key. This file contains authorization, not the account's new password.
3. Within five minutes, select the signed response on the same technician connection, enter/confirm a new temporary password, review the default-No confirmation and reset.
4. Verify a checked encrypted safety snapshot was created **before** the reset, prior sessions were revoked, roles/activity were not changed, and next sign-in forces a new password.
5. Reject wrong key, changed request, expired/replayed authorization, a different target and an inactive account. Restart invalidates pending challenges. No shared/master technical password exists.

## 6. Import, update and uninstall

1. Start from a VM snapshot without configured protected company storage. Close the old POS and create an independent verified encrypted backup. Keep the original database and archive; do not test with the only surviving copy.
2. Technician → Import company → select the completed archive and original phrase, validate, review preview and confirm. For a legacy `.db` snapshot enter a **new** recovery phrase for the migration safety archive. Never select a live working database while its POS is open.
3. Confirm accounts/roles/history are preserved, no existing Owner is promoted, and an encrypted migration safety ZIP exists. If no technical account existed, create the first individual one through Initial setup after import. Do not reopen the old writer after cutover.
4. Reinstall the same-version test installer with all POS instances closed. Confirm the service stops for replacement and restarts; company/key/backup hashes and licensing remain consistent.
5. Test downgrade refusal, an interrupted update and an interrupted import/restore using VM snapshots. A failure must never silently substitute a fresh company. Do not promise an atomic whole-program upgrade from database file-replacement tests alone.
6. Cancel uninstall at its confirmation; the service should remain installed. Complete uninstall afterward and verify protected company/key/safety storage and AppData/external backups remain. Reinstall and verify service-profile key access and sign-in.

## Record the result

Record Windows version, package SHA-256, service account, test dataset, each result, failures and retained backup locations. Do not include passwords, license/recovery private keys or raw key-state contents. Store acceptance evidence with this checklist. Only after successful recovery acceptance should the owner decide how to retire old plaintext source copies; deleting a file does not guarantee secure erasure on an SSD.

Pending separately: production signing, commercial compiler licensing review, native crypto/cipher qualification and warnings, and a deployed online trial endpoint if trials are to be offered. The owner portal remains a separate later phase.
