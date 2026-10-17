# Live database security migration

**Release qualification pending:** final source refinements require a fresh full test run, verified rebuild and spare-PC/VM acceptance. Existing development outputs are not approved for distribution. Verification counts below refer to earlier completed checkpoints, not final release approval.

Status: service/client cutover, encrypted storage, backup/recovery, technician import and installer integration implemented in source, October 15, 2026. **Not installed or live-migrated; clean-machine security acceptance remains required.**

The updated desktop requires the authenticated local service and has no working-database fallback. The service owns SQLCipher storage under `%PROGRAMDATA%\RetailForma\Data`; final local backup ZIPs remain `%APPDATA%\RetailForma\backups`. No service was installed and no live company data or ACL was changed on this development PC.

## Current integration checkpoint — October 15, 2026

This section supersedes the earlier foundation/design log below.

- `RetailForma.Application` contains the server-side operation allowlist, credential verification, role/activity checks, rate-limited login/unlock, connection-bound sessions, forced password changes, and business-license enforcement. Neither actor IDs nor roles supplied by clients grant authority. Reports retain the existing cashier-readable daily/product/summary policy; audit/void events and privileged writes remain restricted.
- `RetailForma.LocalTransport` uses bounded local named-pipe messages, actual Windows identity/elevation, a service-owner check before credentials are sent, network/anonymous denial, and no client pipe-instance creation rights. Disconnects never automatically retry transactions or silently sign in again.
- `RetailForma.DataService` uses its virtual service identity, restrictive root/data ACL checks, a random installation key protected by service-profile DPAPI, and a separately wrapped developer recovery copy. Missing/mismatched keys, missing service and incomplete company storage fail closed. `--self-check` verifies embedded public trust/native cipher without opening company data.
- Desktop business/read/settings/report/login/lock/licensing paths now use typed service operations. The compatibility `App.Db` setter is restricted to the explicitly isolated WPF test host and in-memory databases. Plain SQLite native bundles are removed from desktop/technician project dependencies; historical migration fixtures remain tests, not an operational fallback.
- New-format ZIPs have bounded public owner/developer key envelopes plus an authenticated encrypted manifest and SQLCipher-encrypted snapshot. Owner and developer recovery are independent. The private recovery key stays outside the service, client, installer and repository. Existing two-entry AES ZIPs retain their original password; legacy raw `.db` snapshots are converted explicitly inside private service work storage. No archive-provided path is extracted.
- Backup transfer/restore calls are bounded, consecutive and tied to their originating connection/account. Restore validates a preview, creates and verifies an encrypted safety snapshot, replaces the closed/checkpointed database atomically, and revokes all prior sessions. Older DPAPI backup secrets are removed rather than copied across service installations. Portable ZIP restore requires its original passphrase; restoration must be confirmed, not inferred from a generic error.
- Normal passphrase changes verify the saved version/current phrase first, require new/confirmation, and audit without values. Automation secrets stay service-protected. The unprivileged client saves verified encrypted packages to AppData and verifies selected destination copies by hash; cloud completion is still external. Automatic archives retain 30 daily/8 weekly snapshots; pending/unverifiable/manual/legacy/safety copies are preserved.
- Elevated Technician Setup has Initial setup, Account recovery and Import company tabs. New company provisioning creates only the individual technical account, not a known starter Owner. Existing accounts are not promoted on import. Recovery requires a one-use, short-lived developer signature, a verified encrypted safety snapshot, and forces password change without changing roles/access. The programmer GUI creates separate encrypted recovery keys, signs reviewed recovery requests, and can re-protect supported new backups after owner-passphrase loss.
- Initial company import uploads a selected completed backup; verifies credentials, integrity and schema; uses a fresh installation key; keeps an encrypted safety ZIP; and publishes database/key state together without replacing an existing company. The original source is preserved. Retiring old plaintext copies requires a separate decision after successful recovery acceptance; deletion is not guaranteed SSD secure erasure.
- Installer source configures `RetailFormaData` as `NT SERVICE\RetailFormaData`, stops only its verified registration before binary replacement, creates restricted ProgramData storage, and preserves company/key/backup files on uninstall. Initial setup/activation/migration are never automatic installer actions. The technician/programmer tools remain separate controlled packages.
- Supplied recovery **public** PEM: `D:\MyPos-TestKeys\retailforma-recovery-keys\recovery-public.pem`, SPKI SHA-256 `A8BCD6561770615BD93C4D3B640EB6E00939C1E91E64E30315276FA53A69F186`. Paid test SPKI SHA-256 `93E2307EC160FC3DF66FB568D809CA9213AEDD61D3F0D962496EF86C0A244E69`. Only public PEMs were read. This paid-only qualification build has no configured trial endpoint.
- Inno's installed compiler DLL failed its signed size/hash check. Reinstalled the same official 6.7.3 release after verifying its Pyrsys B.V. Authenticode signature; the mismatched original was preserved in temporary diagnostic storage. Installer compilation then succeeded. No trust check was disabled.

### Remaining release acceptance

Verification: the full Release solution run passed **218 tests, zero failures/skips** (91 Core/security, 81 Desktop, 32 encrypted-storage/service, 7 Technician, 7 Programmer GUI). NuGet's current transitive vulnerability audit reported no known vulnerable packages for all 14 .NET projects. Native/public-trust self-check and installer compilation passed; technician layouts were rendered and visually checked. These checks do not substitute for real Windows service/standard-user/install/recovery acceptance.

Use the owner-selected spare PC/VM, not another Windows profile sharing this machine-wide service. Follow [security acceptance](database-security-acceptance.md). Verify actual SCM startup/reboot, service-profile DPAPI persistence, standard-user denial on directory/database/key/pipe authority, initial/imported company behavior, offline lifetime activation, restore/recovery, interrupted upgrades, and uninstall/reinstall retention before deploying a real store. Production signing, compiler commercial licensing, native upstream qualification/warning review and a live trial endpoint remain distinct gates. No administrator-proof claim or accounting/security certification is implied.

## Historical foundation and approved design log

The entries below describe the earlier pre-cutover checkpoint and approved implementation gates; statements that integration is missing are historical, not the current code inventory.

## Development checkpoint — October 15, 2026

- Added `RetailForma.Security`, independent of the POS and licensing tools: versioned developer RSA-OAEP-SHA256 key envelopes and owner PBKDF2-SHA256/AES-GCM envelopes for the same 256-bit resource key. Envelopes bind installation, resource/key generation and purpose; developer key IDs permit future rotation. These are key envelopes, not a completed new backup archive format.
- Added process-lifetime, one-use developer-signed password-reset authorization challenges with a pinned RSA public key, PSS signatures, exact target/installation binding, a bounded pending set and five-minute monotonic expiry. This verifies developer proof only. No reset endpoint, account mutation, service authentication or maintenance GUI has been enabled. The host still needs authorization, transactional reset/audit, forced password change and session revocation.
- Added a separate `RetailForma.EncryptedStorage` project using `Microsoft.Data.Sqlite.Core` 10.0.12 and the managed `SQLitePCLRaw.provider.sqlcipher` 3.0.5 adapter, not the deprecated bundle/native binaries. It must run in a separate service/test process; it is deliberately not referenced by the current desktop or technician GUI, whose plaintext provider remains unchanged.
- Encrypted connection code requires the reviewed cipher version and memory temp-store build, uses random 256-bit keys, authenticates page access, separates full integrity scans from routine connections, disables pooling, and refuses missing/empty/plaintext/wrong-key databases without fresh-company fallback. Creation is explicit and refuses existing files. Service-controlled paths, ACL application and key persistence are not yet integrated.
- Added a protected-directory ACL descriptor for the dedicated service, SYSTEM and Administrators only. Descriptor tests do not create or secure actual ProgramData folders.
- Installed Microsoft C++ Build Tools and Strawberry Perl with explicit owner approval. Added `tools/Build-SqlCipherCommunity.ps1` with reviewed source commit pins for SQLCipher Community 4.19.0 and OpenSSL 3.5.9 LTS, x64 static-crypto compilation, memory temp storage and disabled extension loading. Source/build cache is under `artifacts/native-security`, not a second customer POS package. The upstream generation step builds its bundled Jim interpreter; no separate Tcl installation is required. A Tcl source build was also exercised in the isolated cache during prerequisite investigation, not installed system-wide or shipped.
- Final Release solution run: **190 passed, zero failures/skips** (90 Core/security, 80 Desktop, 8 encrypted-storage/policy, 7 Technician, 5 License Generator). This includes 10 new recovery checks and eight storage-policy/native checks: four ACL-policy cases and four encrypted-storage cases. Native verification covers encrypted CRUD/WAL, rollback, developer-key recovery, unkeyed/wrong-key rejection, existing/missing/empty targets, plaintext rejection and corrupted-page detection. Full integrity verification also rejects an unencrypted connection. No real developer recovery private key was generated, read or provisioned; tests use disposable in-memory keys.
- Built the Windows x64 native DLL successfully at `artifacts/native-security/output/sqlcipher.dll`, SHA-256 `7318C9BAC24DEB0A949D9E3B710EFF304650E75425A6207F5BBBBCD305F522CA`. Inspected PE dependencies: Windows system DLLs only; OpenSSL and the C runtime are statically linked. Copied SQLCipher/OpenSSL notices alongside the test binary. This is a test-build fingerprint, not a production trust signature or a guarantee of bit-for-bit reproducible builds. OpenSSL upstream compilation emitted warnings; upstream crypto/cipher test suites and release qualification remain pending.

### Still required before this protects a store

Complete service installation/identity and authenticated IPC, all desktop direct-database call replacements, service-side roles/licensing/sessions, protected key provisioning/recovery, new backup packaging and restore integration, developer recovery GUI/actions, technician integration, verified existing-data conversion and installer acceptance. Current backup ZIPs and their passphrase behavior are unchanged. Do not deploy this checkpoint as a completed security upgrade or move a live database manually.

### Developer build/test commands

```powershell
.\tools\Build-SqlCipherCommunity.ps1
dotnet test RetailForma.slnx -c Release
```

The encrypted-storage tests require the locally built `artifacts/native-security/output/sqlcipher.dll`; missing native prerequisites must fail rather than skip encryption checks. An explicit `-p:SqlCipherNativeDirectory=<full-path>` can select another verified test build. Do not copy the native DLL into the existing POS or change its data path as a substitute for service cutover. Upstream crypto/cipher suite acceptance, notices in the customer UI, clean-machine packaging and standard-user protection remain release gates.

## Approved design and remaining cutover work

1. Encryption distribution: **approved October 15, 2026 — SQLCipher Community**, built from maintained upstream source. Community has no SQLCipher purchase fee, but RetailForma must maintain and test the native build and include applicable notices. Do not ship the deprecated `SQLitePCLRaw.bundle_e_sqlcipher` package as the production solution.
2. Working storage: **approved October 15, 2026** — `%PROGRAMDATA%\RetailForma\Data`, owned by the service with a protected ACL. Local encrypted backup ZIPs remain in AppData. This approves the target architecture, not a claim of completed migration; move existing data only through the verified cutover below.
3. Recovery: **developer-assisted recovery approved October 15, 2026**, with a separate developer recovery key pair, not the licensing key. Implement and test the recovery paths below before migration; approval is not evidence that recovery is already available.

### Key ownership clarification

The owner clarified that the aim is preventing unauthorized raw-database access/editing, not excluding the developer from authorized repairs. Approved: a unique random database key per installation, protected for the service, with a recovery copy encrypted to a separate developer public key. Never use a shared developer password embedded in every installation. The application service necessarily needs access to the database key to operate offline; local administrator compromise remains outside this boundary.

### Developer-assisted recovery policy — approved, not implemented

- Keep the developer recovery private key outside the POS, installer and repository. Only its public counterpart may be distributed for creating recovery envelopes. Do not reuse paid/trial license signing keys.
- Account recovery resets an authorized account's password; it cannot reveal the original password hash's plaintext. Invalidate affected sessions and require a password change after recovery. Recovery must not become a public account-reset endpoint or weaken first-System-Administrator provisioning checks.
- Protect a per-installation database-key recovery copy with the developer public key. Identify the installation and recovery-key version so the maintenance tool can select the correct private key. Define authenticated envelope formats, limits and key rotation before implementation; a key identifier alone does not authenticate a recovery request.
- Future backup packages must support both owner-passphrase access and developer-key recovery without embedding the private key or requiring the forgotten owner passphrase for developer recovery. Simply putting an envelope inside a ZIP encrypted only with the owner's forgotten passphrase would not meet this requirement. Version the format and verify archive/payload authentication before restore.
- Existing backups retain their original passphrase requirements. They cannot gain developer recovery retroactively unless successfully opened and recreated. Existing database keys and passphrases have not been replaced.
- Developer maintenance requires explicit authorization and possession of recovery credentials, a verified safety backup before changes, and an audit of operator/action/target/outcome without credentials or key material. Prefer narrowly scoped recovery/repair operations to routine raw SQL editing. Technical Windows elevation alone is not proof of developer authorization.
- Losing the developer private key removes its recovery path for matching envelopes; theft could expose customer data protected by it. Preserve separate secure offline copies and plan rotation. The owner-passphrase route remains independent where available; do not promise recovery if every usable route is lost.

## Security boundary

- The desktop client must not receive the database key, password hashes, unrestricted SQL access or a writable database handle.
- A local Windows service owns the encrypted database and key. Use a dedicated service identity, restricted NTFS permissions and local-only authenticated IPC. Avoid a network listener for this single-PC release.
- Authenticate the actual Windows pipe client; authenticate POS credentials in the service. Bind sessions to their connection/client identity. Resolve current user activity, roles, permissions and license authorization in the service, not from a client-supplied role or actor ID.
- Expose explicit business operations with validated inputs and bounded messages. Never expose arbitrary SQL, generic entity updates, arbitrary filesystem operations or key export to the client.
- Restrict cashier Windows accounts to standard users. Someone with local administrator control can take over the service or inspect its memory; this design does not make a compromised administrator harmless.
- Database encryption is not full-disk encryption and does not by itself prevent an authorized user from abusing an exposed operation. It also does not certify accounting provenance.

## Implementation gates

### 1. Native provider and isolated storage tests

- Pin and document the supported SQLCipher/native crypto versions and redistribution notices.
- Remove conflicting plaintext SQLite bundles from encrypted hosts; verify which native provider actually loads in published Windows builds.
- Generate a random per-installation database key; protect it under the service identity and restricted storage. No embedded/shared database password and no cashier-readable key file.
- Verify cipher availability and encrypted database integrity before accepting connections. Fail closed if the provider/key is missing; never silently create a new plaintext database.
- Exercise encrypted CRUD, EF migrations, transactions, WAL, wrong-key rejection and database recovery using disposable data only.

### 2. Service and client cutover

- Move login, password verification, sessions, role checks and licensing enforcement into the service.
- Replace direct desktop EF access with operation-specific calls. Inspection found `App.Db` references in 33 desktop files; moving only the connection string is not a completed boundary.
- Cover sales, approved voids, products, stock, customers, shifts, cash movements, users, settings, audit and all report data paths. Keep existing domain transaction and accounting invariants.
- Update session lock/logout, password changes and account deactivation so service sessions cannot bypass them.
- Adapt lifetime/trial validation without changing approved offline lifetime or online seven-day trial policies.

### 3. Backup, restore and technician setup

- Create consistent service-side snapshots, including WAL-aware operation; do not expose an unencrypted temporary snapshot to the cashier.
- Preserve portable encrypted ZIP recovery and current/older archive passphrases. Review existing plaintext staging/temporary-file paths.
- Add versioned developer recovery envelopes for the database and new backups, with separate public-key provisioning. Preserve older-format restore support; no shared master password or licensing-key reuse.
- Validate destinations and handle user-owned AppData/cloud-sync copies without giving the service an unrestricted file-write API.
- Restore through the service with current recovery checks, verified safety backup and atomic replacement; support recovery on a replacement machine.
- Replace the technician GUI's direct file access with an elevated, authenticated bootstrap operation. Do not trust a client-supplied elevation flag. Preserve first-account-only checks and audits.

### 4. Installer and existing-data migration

- Install/update the service and secure storage as part of the existing stable installer/package; no feature-suffixed customer folders.
- Explicitly select and confirm the existing company/profile. Require exclusive access and a verified encrypted recovery backup before conversion.
- Preserve the original source during conversion; validate the encrypted destination and schema before publishing it. On failure, keep the source and report the actual failure; do not substitute a blank database.
- Plan the original plaintext source's retirement separately after successful recovery acceptance. Keeping that copy forever leaves a plaintext exposure; deleting it before acceptance risks data loss. File deletion is not guaranteed secure erasure on SSDs.
- Uninstall must preserve protected business data and recovery material. Missing service/key must show a recovery error, not recreate a fresh company.

### 5. Acceptance before claiming protection

- Existing automated regression suite plus isolated encrypted-storage and service authorization tests pass.
- Standard-user cashier cannot open/read/change the database or key, including through parent-folder ACLs, alternate desktop paths or generic IPC calls.
- Unauthorized/stale sessions, forged role/actor values, malformed/oversized requests and unauthorized technician requests are rejected.
- Offline paid selling and online trial enforcement retain their approved behavior.
- Backup, restore on another machine/profile, interrupted migration, upgrade/uninstall and normal close/restart succeed with disposable company data.
- Developer recovery succeeds for a forgotten account password and a new-format backup with a forgotten owner passphrase; wrong/untrusted keys, tampered envelopes and unauthorized reset attempts fail without changes. Older backups still require their original passphrase. Exercise private-key rotation and unavailable-key outcomes using test keys only.
- Only then update the published POS/installer and mark these two security features implemented. No security certification or administrator-proof claim is implied.

## Sources checked

- [Microsoft: SQLite encryption](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/encryption): default SQLite is not encrypted; adding a password requires an encryption-capable native library.
- [SQLitePCLRaw maintainer: current encryption distribution options](https://github.com/ericsink/SQLitePCL.raw/wiki/SQLite-encryption-options-for-use-with-SQLitePCLRaw): old free encryption bundles are deprecated; building upstream source and supported commercial packages are separate options.
- [SQLCipher editions](https://www.zetetic.net/sqlcipher/) and [license information](https://www.zetetic.net/sqlcipher/license/): Community source versus commercial prebuilt packages and attribution requirements.
