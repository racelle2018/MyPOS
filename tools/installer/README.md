# RetailForma installer build and acceptance

**Release qualification pending:** complete a clean rebuild, fresh full verification and spare-PC/VM acceptance before distribution. The successful compilation described below is an earlier development checkpoint, not approval of current output packages.

Updated October 15, 2026: protected-service source and unsigned, self-contained, paid-test installer compile under Inno Setup 6.7.3. SQLCipher/public-trust payload checks pass. Service-owned ProgramData storage, authenticated technician setup/import/recovery, and safe service update/uninstall hooks are included. No installation or live migration was performed. Use a spare PC/VM for [acceptance](../../database-security-acceptance.md); a second Windows profile is not a separate machine-wide service. Resolve commercial compiler licensing and production signing before customer distribution. Compilation/static tests are not release certification.

## Build

Install Inno Setup 6.7.3 or a compatible newer compiler from the [official vendor](https://jrsoftware.org/isdl.php). Review commercial licensing before distribution. The compiler is a programmer tool, not part of the customer POS.

From `D:\MyPos` in PowerShell:

```powershell
& .\tools\Build-Installer.ps1 `
  -PaidLicensePublicKey 'D:\MyPos-TestKeys\Paid\mypos-paid-keys\paid-public.pem' `
  -DeveloperRecoveryPublicKey 'D:\MyPos-TestKeys\retailforma-recovery-keys\recovery-public.pem' `
  -CompilerPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

Compiler installed on this development profile: `C:\Users\User\AppData\Local\Programs\Inno Setup 6\ISCC.exe`. Use that path for the current machine.

These are TEST public keys, not production trust. Never supply a private key. Optional online trials require both `-TrialLicensePublicKey` (a different public PEM) and `-TrialLicenseServerUrl` (the approved HTTPS validation endpoint). Current paid-only test trust does not enable trials.

Without a compiler, validate the self-contained payload only:

```powershell
& .\tools\Build-Installer.ps1 -PaidLicensePublicKey 'D:\MyPos-TestKeys\Paid\mypos-paid-keys\paid-public.pem' -DeveloperRecoveryPublicKey 'D:\MyPos-TestKeys\retailforma-recovery-keys\recovery-public.pem' -ValidateOnly
```

The script publishes an untrimmed, self-contained win-x64 Release payload in temporary build space, scans for excluded files, checks RSA-3072+ embedded public trust and endpoint, then compiles one stable `artifacts\RetailFormaSetup.exe`. Temporary payloads are removed on normal completion/failure. No POS startup, live installation or data migration occurs. The developer POS folder remains `artifacts\RetailForma`; no additional version-named POS folder is created.

Use `-UpdateStablePackage` to copy the verified staged POS/service payload into that existing developer package after closing the POS. This does not install the service or migrate company data. Unreferenced old provider DLLs may remain in an in-place developer folder; current project dependencies no longer load them. The clean installer payload excludes plain-provider native binaries, and its fixed installer cleanup list targets only retired generated program files, never database/backups/keys.

## Current service installation behavior

- Installer creates/verifies `RetailFormaData` as `NT SERVICE\RetailFormaData`, with restricted `%PROGRAMDATA%\RetailForma` storage; the service explicitly provisions the encrypted Data directory after technician confirmation. No company fallback is created by POS startup.
- Service binary/native/public-trust self-check runs without opening company data. Verified existing service is stopped before replacement and restarted afterward. Active POS processes are refused, not killed.
- Uninstall preflight does not remove the service before the user confirms. Actual uninstall removes the service registration and program files, not ProgramData company/key/safety material or AppData/cloud backups.
- Technician and programmer tools remain separate controlled packages. Private keys never enter the installer. Source copies are not deleted during migration.
- Inno's mismatched compiler DLL was preserved in temporary diagnostic storage and repaired from the verified official same-version installer. No compiler integrity check was bypassed.

## Historical pre-service installation behavior

The older storage/profile statements below describe the October 14 build and are superseded by the current service behavior above and the linked acceptance checklist.

- Welcome page contains a short product introduction. Terms & Conditions requires acceptance in interactive setup; silent setup requires the explicit `/ACCEPTTERMS` parameter. Company/contact supplied by the owner: RetailForma Ltd., ellecarpacquiaban@gmail.com; no separate personal publisher label. Inno's `AppPublisher` metadata field stores the company name. This is installer acceptance, not license activation or blanket privacy consent. Before customer release, have the terms reviewed and supply the applicable licensing-service privacy notice. See [NPC transparency guidance](https://privacy.gov.ph/the-right-to-be-informed/).
- Machine-wide application installation under `C:\Program Files\RetailForma` by default, requiring Windows administrator approval. Start menu shortcut and optional desktop shortcut. The bundled .NET runtime avoids a separate runtime download/install. Company data and activation remain per Windows profile; installing shared binaries does not implement a shared database.
- Earlier per-user installations must be uninstalled before switching installation mode; the setup refuses to silently create a parallel installation. Company data is retained. Customer-facing notes are concise release wording; signing/licensing/test limitations stay in developer documentation.
- Same AppId and previous installation directory for updates. Running new/legacy POS mutexes block update/uninstall; no automatic or forced shutdown or restart. Newer installed versions block downgrades. Same-version fixes may reinstall.
- Company data remains in `%APPDATA%\RetailForma`. No installer/uninstaller data-deletion directives, no database copies, no automatic launch, migration, restore, account reset or activation.
- Uninstall removes tracked installed program files/shortcuts, not company data or external backup folders. Existing activation remains subject to the same device/Windows-profile rules when the POS is reopened.
- The installer excludes the License Generator, technician Setup, server, private keys, backup archives and databases. The technician tool is delivered separately through the approved controlled workflow.

## Acceptance before store distribution

Use an isolated Windows test profile or VM, never the live company profile. After a compiled installer is available:

A separate Windows profile isolates profile data/activation, not the machine-wide Program Files package or its uninstall entry. It is suitable for initial dummy-data and activation checks; prefer a VM or separate PC for full install/upgrade/uninstall and fault qualification, particularly once a production installation exists. Launch the POS normally under the test user, not using another user's elevated credentials.

1. Fresh install without a system-wide .NET runtime; check RF icon, shortcuts and independent uninstall entry. Install must not launch the app.
2. Configure an authorized test installation/license and a recognizable dummy dataset; create and independently verify an encrypted backup.
3. Test upgrade/reinstall with the app closed. Compare products, sales, users, settings, backup files and activation before/after. Installer must not reset or silently restore them.
4. With the POS open, confirm installer/uninstaller refuses to proceed until normal shutdown; test silent mode too. Do not force-close a sale.
5. Test downgrade refusal and interrupted installation/recovery. No atomic binary-upgrade guarantee is claimed by source checks alone.
6. Uninstall/reinstall: confirm retained profile data/backup hashes and license files are unchanged, then relaunch deliberately.
7. Review actual supported Windows versions, x64 hardware, security software/SmartScreen behavior and offline installation. Unsigned test setup is not a trusted production installer; obtain controlled code-signing credentials before commercial release. Never commit a signing certificate/private key.

Automatic self-update, multiple Windows-user deployment, server/portal installation and production signing are not implemented by this installer source. Manual upgrades must have an independent verified backup first.
