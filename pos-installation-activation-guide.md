# RetailForma installation and activation guide

**Release qualification pending:** rebuild and verify packages in a clean environment before following these installation steps. Existing development outputs are not approved distribution packages.

Updated: October 15, 2026 (Asia/Manila).

## Protected-service installation update — current workflow

This section supersedes the older per-profile database/first-launch migration instructions below. See the full [spare-PC/VM checklist](database-security-acceptance.md) before changing a real company.

1. Install `artifacts\RetailFormaSetup.exe` on the disposable test PC/VM with Windows administrator approval. The updated setup installs the protected local service under Program Files; it does not launch the POS, create accounts or migrate data automatically.
2. Run the separate `artifacts\RetailForma.TechnicianSetup\RetailForma.TechnicianSetup.exe` elevated. For a **new** company use Initial setup → Connect, individual technical credentials and explicit confirmation. For an **existing** company use Import company with a completed verified backup, review the preview and confirm. Preserve the original source; do not reopen the old POS after successful cutover.
3. Open RetailForma normally as the intended standard Windows user. Sign in with the individual System Administrator, create the individual Owner/Cashier accounts, and use System Utilities → Licensing to export the device request and import the programmer-issued signed lifetime license. Fresh service setup does not provide a known `admin/admin123` Owner account.
4. Working records/key material belong to `NT SERVICE\RetailFormaData` in `%PROGRAMDATA%\RetailForma\Data`; the desktop never receives that key or a password hash. Final local encrypted ZIPs remain `%APPDATA%\RetailForma\backups`. Different Windows profiles now share this machine-wide company service, not separate databases.
5. Owner → Backup & Restore → Set/Change passphrase and create a verified independent backup. New ZIPs support independent owner/developer recovery; old ZIPs need their original phrase. Recovery of a forgotten account uses the technician request → programmer signature → temporary-password workflow, not manual password-hash editing.

Current packages use paid **test** trust plus the supplied separate public recovery key. No trial endpoint or customer production signing was provisioned; paid lifetime remains offline after valid activation. No live company was migrated during development. Do not start the updated portable POS against the old plaintext AppData file; it requires the installed protected service.

## Earlier installation/licensing reference

Per-profile database and migration details in the older sections below describe the pre-service build and are retained only as historical context. The protected-service workflow above and linked acceptance checklist govern current installation/storage/recovery. The independent paid/trial licensing and key-separation principles still apply.

Local POS output: `D:/MyPos/artifacts/RetailForma/RetailForma.exe`. Routine fixes update this same folder after the app is closed; do not create version/feature-suffixed POS copies. The License Generator and Setup are separate tools, not duplicate POS versions.

Installer: `D:/MyPos/artifacts/RetailFormaSetup.exe`, default application directory `C:\Program Files\RetailForma`, requiring administrator approval. Company data/backups remain in `%APPDATA%\RetailForma`. Install/upgrade/uninstall acceptance and production signing are still developer release gates; customer-facing installer text does not contain test-only commentary.

Restore failures now distinguish a rejected passphrase, damaged/unreadable ZIP, unsupported backup format, backup from a newer app, invalid contents, insufficient space and file-access problems. Where encrypted authentication cannot distinguish the passphrase from data damage, the message explicitly says the exact cause cannot be determined. No failed verification stages a replacement.

This guide follows the current source code. It covers the Basic single-PC deployment, online seven-day trial, offline paid lifetime license, and authorized hardware replacement.

**Current status:** the source and isolated tests exist, but no live trial server, production signing keys or certified customer installer have been provisioned by this work. The present distribution method is a published application folder—not a finished MSI/setup wizard. Treat the commands below as instructions, not actions already performed. Use separate test keys/database/Windows profile before serving real customers.

### RetailForma 1.2.0 upgrade from the old installation

In the updated Backup & Restore page, first-time recovery-passphrase setup requires new/confirmation. Changing an existing one also requires the correct current passphrase. Blank new fields keep it unchanged. Preserve old passphrases independently: existing ZIP archives are not rewritten. A forgotten or unreadable secret requires separately authorized recovery, not a silent overwrite in normal Save.

Use the same Windows profile, close the old POS, and ensure its Backup & Restore recovery passphrase was saved on that profile. First launch offers a verified, copy-only migration from `%APPDATA%/MyPos/mypos.db` to `%APPDATA%/RetailForma/retailforma.db`. Original records/files remain intact. If a RetailForma destination folder already exists, stop and preserve/review it; never delete or merge databases to force migration. A missing recovery secret stops migration safely.

New paid/trial tokens explicitly sign `RetailForma.Basic`; old test activations require a fresh device request and reissuance using the existing separate issuer key pair. Do not edit signed files or reset trial history. The old cache is archived for recovery, not imported as active. Live migration and restore verification remain acceptance steps; see [migration plan](retailforma-migration-plan.md).

## 1. Who does what

- **Programmer/license issuer:** keeps private signing keys, prepares trusted Release builds, configures the trial server, registers invitations, verifies purchases and authorizes lifetime/replacement licenses.
- **System Administrator in RetailForma:** the technical application account used to export device requests, start trials and import signed licenses. Only this role sees System Utilities → Licensing.
- **Owner:** business settings/users/backups and normal business permissions; not paid signing authority.
- **Cashier:** permitted daily operations; not licensing access.
- **Windows account:** determines the local database and protected license-cache location. A RetailForma role is not a Windows administrator account.

Use the same intended Windows profile for ordinary POS operation and activation. Owner/Cashier/System Administrator may sign into RetailForma under that same Windows profile. Logging into another Windows profile is not merely switching RetailForma users.

The first-account setup tool requires Windows elevation. You do not need to run the everyday POS or generator elevated.

## 2. Understand the license files and secrets

| Item | Created by | Where it belongs |
| --- | --- | --- |
| Paid private key + passphrase | Generator → Signing keys → Paid lifetime | Programmer-controlled offline storage; never POS/Vercel/GitHub |
| Paid public PEM | Same paid key creation | Embedded in the POS Release build |
| Trial private key + passphrase | Generator → Signing keys → Online trial | Controlled trial server secrets; never customer POS/GitHub |
| Trial public PEM | Same trial key creation | Embedded in the POS Release build |
| POS device-request JSON | Customer POS → Export device request | Send to the programmer; contains company and a hashed fingerprint, not raw hardware identifiers |
| Trial invitation ID + activation key | Trial API's authenticated registration endpoint | Give privately to the intended device's activation technician |
| Signed lifetime-license JSON | Generator → Generate license | Deliver to the customer's technician and import into POS |
| TRIAL_ADMIN_TOKEN | Programmer's secure secret-generation process | Server secret and programmer's controlled registration client only |

**A signing key is not a customer license.** Creating Online trial keys in the generator does not start anybody's seven-day trial.

Never reuse the paid private key on the trial server. Never create a new signing key for each customer: customer licenses use your established key set and individual entitlement IDs. Changing keys does not automatically update trust inside installed POS builds.

## 3. One-time programmer preparation: signing keys

Do this on the programmer's controlled computer.

1. Open the published License Generator GUI:
   D:\MyPos\artifacts\retailforma-license-generator\RetailForma.LicenseGenerator.exe
2. Keep its full published folder together. Do not distribute it to customers.
3. Open Signing keys.
4. Select Paid lifetime — keep private key offline.
5. Choose a **new** absolute folder outside D:\MyPos and any customer POS installation, for example:
   C:\RetailFormaPrivateKeys\Paid
6. Click Create encrypted key pair, check the confirmation, and enter matching passphrases of at least 16 characters.
7. The folder receives:
   - paid-private.encrypted.pem
   - paid-public.pem
8. If offering trials, repeat in a **different new folder**, selecting Online trial — separate server key, for example:
   C:\RetailFormaPrivateKeys\Trial
9. That folder receives:
   - trial-private.encrypted.pem
   - trial-public.pem
10. Securely retain private keys, their passphrases and the paid issuer's later issuer-journal.jsonl. Keep an independent encrypted recovery copy. The public PEMs may go into the POS build.

These example folders are names, not folders already created for you. Existing folders are deliberately refused; choose another new folder rather than deleting/replacing an established key set.

Use clearly separate TEST and PRODUCTION sets. Test keys must not accidentally become commercial client trust.

If rebuilding the generator is necessary, from D:\MyPos:

~~~powershell
dotnet publish src/RetailForma.LicenseIssuer.Gui -c Release -o artifacts/retailforma-license-generator
~~~

The generator is GUI-only. Its signing backend is inside RetailForma.LicenseIssuer.Gui/Backend; there is no separate terminal issuer project.

## 4. One-time online trial-service deployment

Skip this section if you offer **paid lifetime only**. Lifetime does not need a deployed trial server.

### 4.1 Prepare the separate licensing database

1. Provision an authorized PostgreSQL database with trusted TLS.
2. Use a database-administration tool/SQL editor authenticated to that database.
3. Verify the database name and server first.
4. Run the contents of:
   src/RetailForma.TrialApi/migrations/001-trials.sql
5. Verify that license_trials and license_trial_events exist.
6. Arrange backups and access controls for this database.

Do not run this SQL against the POS's SQLite retailforma.db or an unrelated business database. Keep trial history: deleting it can erase the protection against repeated trials.

The backend checks TLS certificates. Do not disable certificate checks to work around a connection failure. Its URL validation rejects ssl query options; if your provider supplies such a URL, obtain/configure an equivalent compatible connection string with the provider rather than weakening TLS.

### 4.2 Create the Vercel project

On your authorized Vercel account:

1. Choose a plan permitted for your intended use. Hobby is for personal/non-commercial use; testing a commercial POS does not itself establish eligibility. See [Vercel Hobby rules](https://vercel.com/docs/plans/hobby).
2. Make sure the current TrialApi source is present in the repository/revision you will deploy. Local uncommitted changes are not available to a Git-based Vercel deployment. This guide does not commit or push them.
3. Import the intended repository and set Root Directory to:
   src/RetailForma.TrialApi
4. This is an API-only Node project, not the WPF POS or an owner-portal website. Use the appropriate Other/API project settings; do not configure a Next.js frontend build.
5. Choose a currently supported Node version satisfying the package's >=22 requirement.
6. Set npm ci as the install command if configuring it explicitly. There is no frontend build script or generated website output directory in this project.
7. Verify that api/admin-register-trial.mjs, api/trial-start.mjs and api/trial-validate.mjs are deployed as functions. If your chosen settings do not detect them, stop and correct the deployment configuration before building customer clients.

Official references: [Vercel project/root settings](https://vercel.com/docs/project-configuration/project-settings), [Node functions and .mjs support](https://vercel.com/docs/functions/runtimes/node-js), [supported Node versions](https://vercel.com/docs/functions/runtimes/node-js/node-js-versions).

Before deploying, you can run the protocol tests locally:

~~~powershell
Set-Location D:\MyPos\src\RetailForma.TrialApi
npm ci
npm test
~~~

These tests do not create a live database/server or certify deployment.

### 4.3 Configure Vercel secrets

Add these to the environment used by the deployment:

| Variable | Value |
| --- | --- |
| TRIAL_DATABASE_URL | Separate PostgreSQL connection URL compatible with the TLS checks above |
| TRIAL_SIGNING_PRIVATE_KEY_PEM | Complete contents of trial-private.encrypted.pem, with real line breaks |
| TRIAL_SIGNING_KEY_PASSPHRASE | The passphrase used to encrypt that trial key |
| TRIAL_ADMIN_TOKEN | An independently generated unpredictable secret, at least 32 characters |

Use your authorized secret settings, not a source-code file. Do not wrap the PEM in extra quotation marks or paste literal backslash-n sequences in place of real line breaks. Do not paste secrets into chat, GitHub, screenshots or terminal command arguments.

The server expects the encrypted **trial** PEM, not its local file path and not the paid private key. Use a proper cryptographically secure secret generator/password manager for the admin token; do not use a predictable phrase or entitlement ID.

Environment changes must be applied to the relevant deployment; create/redeploy it afterward. See [Vercel environment variables](https://vercel.com/docs/environment-variables).

### 4.4 Verify the endpoint and access protection

Record a stable HTTPS base URL such as:

https://YOUR-TRIAL-HOST.example

The POS will be built with the exact endpoint:

https://YOUR-TRIAL-HOST.example/api/trial-validate

Replace the example host with your real deployment. Do not use a changing per-commit URL as your long-term client endpoint.

The desktop client does not perform Vercel browser login, follow redirects, or send a Vercel protection-bypass credential. Use an explicitly authorized endpoint configuration that accepts the required machine-to-machine requests. Keep the application's admin-token checks and provider abuse/rate controls in place; do not globally disable protections just to get a browser page to load. See [Vercel deployment protection](https://vercel.com/docs/deployment-protection).

The current API does not provide a homepage or health-check screen. A root-page 404, or opening a POST-only API URL in a browser, is not a complete service test.

Use an isolated device/company fixture to verify registration, activation, signed validation, persistent database state, outages and expiry. Exact seven-day expiry should be qualified in a controlled test environment—not by changing a customer's production records or relying on the PC clock.

## 5. Prepare the customer POS distribution

Do this on the build/programmer computer with the .NET 10 SDK. Customers do not need the SDK.

The examples below create a **framework-dependent win-x64 Release folder**. Customers need the compatible .NET 10 Windows Desktop Runtime, available from [Microsoft's .NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

From D:\MyPos, for a POS supporting both trial and lifetime:

~~~powershell
dotnet publish src/RetailForma.Desktop/RetailForma.Desktop.csproj -c Release -r win-x64 --self-contained false "-p:PaidLicensePublicKey=C:\RetailFormaPrivateKeys\Paid\paid-public.pem" "-p:TrialLicensePublicKey=C:\RetailFormaPrivateKeys\Trial\trial-public.pem" "-p:TrialLicenseServerUrl=https://YOUR-TRIAL-HOST.example/api/trial-validate" -o artifacts/customer-pos
~~~

For a **lifetime-only** POS:

~~~powershell
dotnet publish src/RetailForma.Desktop/RetailForma.Desktop.csproj -c Release -r win-x64 --self-contained false "-p:PaidLicensePublicKey=C:\RetailFormaPrivateKeys\Paid\paid-public.pem" -o artifacts/customer-pos-lifetime
~~~

Publish the local first-System-Administrator provisioning tool separately:

~~~powershell
dotnet publish src/RetailForma.Setup/RetailForma.Setup.csproj -c Release -r win-x64 --self-contained false -o artifacts/technician-setup
~~~

Important checks:

- Replace all paths/URLs with your actual values.
- Supply **public PEMs only** to the POS build. Never put private keys/passphrases/admin tokens in build arguments.
- Do not deliver Debug. Its unconfigured developer bypass is not commercial license activation.
- An unprovisioned Release build does not acquire trust merely by placing a public PEM beside the executable; trust is embedded at build time.
- Include the paid public key even for a trial-capable client, so the same installation can later accept lifetime activation.
- Verify Windows architecture/OS compatibility for the actual computer. These commands assume win-x64.
- Keep the entire published folder, including dependencies, resources and font notices—not just the EXE.
- Inspect the customer package to ensure it contains no generator/private keys/server secrets or unrelated programmer files.
- Keep RetailForma.Setup in the technician's controlled kit, not as an everyday cashier shortcut.
- Archive which key set and endpoint were used for each distributed build.

A self-contained publish is another deployment option, but needs its own packaging/acceptance checks. It bundles the runtime; framework-dependent deployment relies on an installed runtime. See [Microsoft deployment overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/) and [dotnet publish](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish). Do not enable trimming/single-file options for this WPF product without testing them.

## 6. Install and bootstrap the customer PC

### 6.1 New installation

1. Select the intended Windows user profile that will normally run the POS.
2. Install the compatible .NET 10 Windows Desktop Runtime if using the framework-dependent package.
3. Copy the complete customer-pos published folder to the chosen application location. For example, C:\Apps\RetailForma, with appropriate permissions.
4. Create a shortcut to RetailForma.exe.
5. Run it normally under the intended Windows profile, not under another account's profile via Run as different user.
6. On first start, the app creates/migrates its local database and displays login.
7. On a genuinely new database, the current bootstrap Owner account is:
   - Username: admin
   - Temporary password: admin123
8. Sign in and complete the required initial password change immediately. Do not leave the default active or reuse shared technical credentials.
9. Open System Utilities → General Settings and save the real company details, especially Company name.
10. Configure Backup & Restore recovery-passphrase settings and create/verify an encrypted backup before technical provisioning. Keep the recovery passphrase separately.
11. Close **all** RetailForma windows.

This initial Owner account is not the System Administrator role. Licensing remains unavailable to it.

### 6.2 Locate the actual database

Under the ordinary POS Windows account, open this location using File Explorer or Windows+R:

%APPDATA%\RetailForma

The database is:

%APPDATA%\RetailForma\retailforma.db

Typically:

C:\Users\POSUSER\AppData\Roaming\RetailForma\retailforma.db

Copy the real absolute path. Do not assume the example username. When elevation uses another Windows account, its APPDATA may differ; always pass the POS user's **explicit database path** to setup.

Other local paths:

| Purpose | Location under the POS user's profile |
| --- | --- |
| Local database | %APPDATA%\RetailForma\retailforma.db |
| Backups | %APPDATA%\RetailForma\backups |
| Logs | %APPDATA%\RetailForma\logs |
| Protected license cache | %APPDATA%\RetailForma\licensing |

Do not copy a running SQLite database as a backup; use verified Backup & Restore, or a qualified offline recovery procedure with all connections closed.

### 6.3 Existing installations

Before upgrading, close RetailForma and take a verified independent backup. Preserve the correct Windows profile, AppData, company database and existing license files. Do not delete them or restore a blank database just to activate.

The starter-password instructions apply to a new database only. Use the actual account on an existing installation. Do not run the first-account tool to reset an existing System Administrator.

## 7. Create the first System Administrator

This is a technician task on the customer PC. **RetailForma.Setup is a separate GUI provisioning tool**, not part of the customer installer.

1. Verify RetailForma is closed and the independent backup is usable.
2. Open the published GUI and approve the Windows administrator prompt:

~~~powershell
& "C:\TechnicianTools\RetailForma.TechnicianSetup\RetailForma.TechnicianSetup.exe"
~~~

3. Click Browse and select `C:\Users\POSUSER\AppData\Roaming\RetailForma\retailforma.db` for the intended Windows profile. Verify the displayed company and path; do not accidentally select the elevated administrator's profile.
4. Enter an individual username with at least three letters/numbers, a full name, and matching 16–256-character passwords.
5. Confirm that you checked the target and kept an independent backup, then click Create account and confirm the displayed company/path.
6. The tool creates/audits only the first System Administrator. It does not create/migrate a database, promote the Owner, or reset an existing technical account.
7. Close the tool, restart RetailForma normally under the intended POS Windows profile and sign into the new System Administrator application account.

Development tool: `D:/MyPos/artifacts/RetailForma.TechnicianSetup/RetailForma.TechnicianSetup.exe`. Copy the entire self-contained folder to another PC. The old terminal tool is superseded; no database/password command-line arguments are needed.

If setup says one already exists, stop and use the established technical account/recovery process. Do not bypass it by editing the user table or deleting the account.

Secure technical credentials individually; do not give every client a shared programmer password.

## 8. Export the actual device request

On the customer PC:

1. Sign into RetailForma as System Administrator.
2. Open System Utilities → Licensing.
3. Click Export device request.
4. Save the JSON to a controlled exchange location.
5. Send that file privately to the programmer.
6. The programmer verifies the company/device ownership before issuing anything.

The fingerprint hashes motherboard serial, CPU ID and Windows Machine GUID. Do not manually edit it or export the programmer PC's fingerprint instead of the customer's.

If hardware identifiers are unavailable/generic, export may fail. Diagnose the actual hardware/WMI issue; do not substitute a universal fingerprint.

**At this point choose either section 9 (trial) or section 10 (paid lifetime).** A customer may buy lifetime directly; trial is optional.

## 9. Trial activation: programmer registers, technician activates

### 9.1 Create a customer invitation on the server

The generator GUI currently has no customer-trial-invitation form. Its Trial option creates **server signing keys only**.

Use a controlled API client or the following PowerShell example on the programmer's computer. It prompts for the administration secret instead of writing it into command history/source.

Run privately: no terminal transcription, screenshots or shared screen. Replace the URL/path. This operation creates a trial-registration record on your server.

~~~powershell
$trialRequest = Get-Content -LiteralPath "C:\CustomerRequests\retailforma-device-request.json" -Raw | ConvertFrom-Json
$trialAdminSecure = Read-Host "Trial server administration secret" -AsSecureString
$trialAdminPlain = [System.Net.NetworkCredential]::new("", $trialAdminSecure).Password
try {
    $trialBody = @{ Company = $trialRequest.Company; Fingerprint = $trialRequest.Fingerprint } | ConvertTo-Json
    $trialInvitation = Invoke-RestMethod -Method Post -Uri "https://YOUR-TRIAL-HOST.example/api/admin-register-trial" -ContentType "application/json" -Headers @{ Authorization = ("Bearer " + $trialAdminPlain) } -Body $trialBody
    $trialInvitation | Format-List LicenseId, ActivationKey
}
finally {
    $trialAdminPlain = $null
    $trialAdminSecure = $null
}
~~~

Success returns:

- LicenseId: the customer's trial invitation ID.
- ActivationKey: a confidential activation value for that invitation/device.

Store the returned pair securely and give it only to the intended activation technician. Never give the customer TRIAL_ADMIN_TOKEN, private trial PEM or its passphrase. Clearing variables is not a guarantee of memory erasure.

Registration does **not** start the seven days. The first successful server activation does. Repeated registration for the same fingerprint returns 409; do not delete its history to grant another trial. The current interface does not provide invitation-key recovery/reissue or a GUI administration dashboard.

### 9.2 Activate on the customer PC

1. Connect the customer PC to the internet.
2. Sign into RetailForma as System Administrator under the normal POS Windows profile.
3. Open System Utilities → Licensing.
4. Enter the returned LicenseId into Trial license ID.
5. Enter ActivationKey into Activation key.
6. Click Activate seven-day trial online.
7. Wait for successful online validation/storage.
8. The server binds the device and fixes the first activation timestamp in database UTC. Expiry is exactly seven × 24 hours from that time.
9. The POS verifies the signed license and fresh server response and stores the token encrypted locally.
10. Sign out of the technical account and return to the Owner/Cashier account for normal work.

Changing the computer clock/timezone does not extend this signed server-controlled trial. Re-entering the same invitation does not restart the period.

### 9.3 Trial operating behavior

- Protected business writes require fresh online validation, including the check before commit.
- There is no seven-day offline grace period.
- Offline, denied/revoked, expired or unavailable-server validation prevents those protected writes.
- Connection failure is not proof of expiry; investigate the service/network first.
- Login, reports and recovery remain available. The implementation is a protected-write gate, not a blanket fullscreen lock that hides all records.
- Existing business data is not erased by trial expiry.
- A customer's current Windows profile matters: local key protection is profile-specific.
- Preview deployments requiring browser login/redirect cannot be used directly by the current desktop client.

Run real end-to-end trials only after the server, keys, database and endpoint have been qualified. Node/.NET unit tests do not replace that qualification.

## 10. Lifetime activation: programmer GUI → signed file → customer POS

This can be done directly, without a trial or trial-server deployment.

### 10.1 Generate on the programmer's computer

1. Verify purchase/payment, company ownership and the customer's actual device request.
2. Open License Generator → Generate license.
3. Browse to the customer's exported request JSON.
4. Check Company and Device fingerprint.
5. Browse to your established paid-private.encrypted.pem.
6. For a new sale, use New ID and record the resulting lifetime entitlement ID.
7. Leave Approved hardware replacement unchecked for a first issuance.
8. Leave Owner portal entitlement unchecked unless that feature is explicitly approved. That flag does not implement a portal.
9. Click Generate lifetime license.
10. Review the confirmation and explicitly approve.
11. Choose a new output JSON filename/location.
12. Enter the paid key's passphrase in the masked dialog.
13. Wait for the success notification.
14. Verify the record in Issuance history. Securely retain the entitlement ID, company, device record, signed-file hash/ledger and purchase evidence.
15. Deliver **only the signed JSON license** to the activation technician.

Never send the customer your private PEM/passphrase or your generator. Existing output files are refused rather than overwritten.

If an operation reports failure after output creation, reconcile that file with the ledger before retrying/delivering it; two-file publication is not guaranteed crash-atomic.

### 10.2 Import on the customer PC

1. Sign into RetailForma as System Administrator under the normal POS Windows profile.
2. Open System Utilities → Licensing.
3. Click Import signed license.
4. Select the programmer's signed JSON file.
5. The POS checks trusted paid signature, format/features, device fingerprint and absence of expiry.
6. Wait for the successful verified/encrypted-storage notification.
7. Return to Owner/Cashier use.

Paid verification does not require contacting Vercel. The first paid import can be performed offline if the signed file is delivered by a controlled offline method.

A rejected/wrong-device/invalid-signature import must not be treated as activation. Correct the file/trust mismatch; do not edit JSON claims or bypass checks.

## 11. Convert trial to lifetime without losing sales

1. Keep the existing installation, normal Windows profile and company database.
2. Verify purchase and use that PC's device request.
3. Issue a **new lifetime entitlement** through the paid GUI workflow.
4. The trial invitation ID and lifetime entitlement ID are different identifiers; record their relationship in your customer records if useful.
5. Import the paid signed JSON through the POS Licensing page.
6. Successful import replaces the active local trial token with the paid lifetime token.
7. Reopen the POS offline and verify permitted business operation with a controlled acceptance transaction.
8. Preserve existing sales/products/users/settings and audit history; do not reinstall a blank database.

The client does not allow an incoming trial to replace an already installed lifetime license. A lifetime upgrade has no renewal/internet requirement afterward. Future owner-portal synchronization is independent and remains Phase 2.

Do not create another trial registration or delete expired-trial rows to simulate a purchase.

## 12. Hardware failure, Windows reinstallation and profile recovery

### 12.1 Motherboard/PC replacement

1. Recover the company's business database from a verified backup using the approved recovery process.
2. Install the compatible trusted POS build on the replacement PC.
3. Recover the established System Administrator from the restored database; provision the first one only if no technical account exists.
4. Export a fresh request from the replacement device.
5. The programmer verifies ownership and the original **paid entitlement ID** in the established issuer ledger.
6. In Generate license, load the replacement request and established paid key.
7. Check Approved hardware replacement. The GUI clears the ID and disables New ID.
8. Enter the original lifetime entitlement ID.
9. Verify feature entitlement and explicitly approve replacement.
10. Save a new signed JSON, check the retirement record, and import it on the replacement POS.
11. Require the client to stop using the old device.

The approved policy permits authorized replacement without another lifetime purchase or dead-PC online deactivation. It is not a second simultaneous device license. The old permanently offline token cannot be instantly remotely revoked. The ledger's retired fingerprint is a record, not proof the old offline PC has stopped working.

The generator refuses replacement without a prior matching entitlement record. Recover/reconcile your ledger rather than deleting it or inventing a new ID.

### 12.2 Another Windows profile on the same PC

The license token's storage key uses current-user DPAPI. Copying licensing files into a different Windows profile does not automatically make them usable.

Use the approved data recovery/profile procedure and re-import the original paid signed JSON if the device fingerprint remains unchanged. Trial requires online revalidation and retains the existing server start date; profile changes do not grant a new trial.

Also re-save the automatic-backup recovery credentials under the new Windows profile using the owner-held recovery passphrase. Those automation credentials are Windows-profile protected; database recovery alone does not make them portable. Do not discard older archive passphrases after rotation.

### Set or change the backup recovery passphrase

In System Utilities → Backup & Restore, select **Set passphrase** if none is saved, or **Change passphrase** if already configured. A separate close-only dialog opens. For a change, enter the current passphrase and click **Verify current passphrase** before the new and confirmation fields appear. Enter matching, case-sensitive 16–256-character values and click Save. Save verifies again and rejects a dialog made stale by another saved change; reopen it if warned. Cancel or closing without Save leaves the saved passphrase unchanged.

This dialog does not save pending folder/provider edits; use Save preferences for those separately. Future backups use the new passphrase, but existing ZIPs still require their original one. Keep both securely. A forgotten or unreadable saved passphrase cannot be silently reset through Change passphrase. This is independent of the license-signing-key passphrase.

To restore, click **Restore Backup** and choose the backup file. Selecting an encrypted ZIP opens a separate **Restore backup** dialog: check its filename, enter the passphrase used when that ZIP was created, then click Restore. After verification, confirm the replacement (default No). A successful action stages the backup; close RetailForma normally and reopen it to complete restoration. Cancel or failed verification does not stage a restore. The entered archive passphrase is not saved or used to replace your current recovery passphrase. Legacy unencrypted `.db` backups use the existing confirmation without a passphrase dialog. A current recovery passphrase must already be configured on this Windows profile for the pre-restore safety copy.

### 12.3 Windows reinstall or other fingerprint changes

The fingerprint includes Windows Machine GUID as well as motherboard/CPU identifiers. A reinstall can therefore require a fresh fingerprint/request and an authorized entitlement replacement even if the motherboard did not change.

Do not promise that a database restore alone restores activation on any machine/profile. Keep signed paid files and programmer issuance records separately from database backups.

## 13. Troubleshooting

| Symptom | Check / safe action |
| --- | --- |
| Licensing menu missing | Sign into the active System Administrator application role; Owner/Cashier cannot activate |
| Setup says a technical account exists | Use established credentials/recovery; the first-account tool cannot reset or promote |
| Setup targets the wrong company/profile | Stop before CREATE; obtain the actual POS user's full database path |
| Installer verification keys not provisioned | Rebuild Release with the correct public PEM properties; copying PEM beside EXE is insufficient |
| Trial server not configured | Rebuild with the actual HTTPS /api/trial-validate URL and matching trial public PEM |
| API 401 during invitation registration | Check the private admin bearer secret/environment; never place it in customer builds |
| API 409 during registration | Fingerprint already registered; preserve its existing start/history |
| API 403 during activation | Verify invitation/device/revocation; do not assume credentials for a different PC work |
| API 503 or database/TLS failure | Check Vercel/database configuration and authorized logs without exposing secrets |
| Browser login/HTML/redirect instead of JSON | Check the selected domain and deployment protection; current client does not follow redirects/login |
| Trial offline or expired | Protected writes are unavailable; restore connectivity/diagnose service or purchase lifetime |
| Invalid signature | Correct paid/trial trust mismatch or damaged file; do not alter signed claims |
| Wrong device | Export the correct customer's request or use approved replacement |
| Cannot open private key | Check encrypted PEM and passphrase; no master passphrase exists |
| Key folder/output already exists | Choose a new legitimate path; never overwrite/erase established keys/history |
| Hardware export unavailable | Investigate WMI/stable identifiers; do not use a shared fallback fingerprint |
| Cache cannot be decrypted after profile change | Qualified recovery and re-import; DPAPI cache is not portable activation |
| Import succeeded but recording its audit failed | Verify activation state and underlying DB failure; retain evidence rather than assuming all steps succeeded |
| Same output exists after issuer error | Reconcile output/hash/ledger before retrying or delivering |

A failed trial connection must not be described as confirmed expiry. An empty initial Licensing status does not itself prove which token is active; acceptance must verify actual protected operation, not merely read generic help text.

## 14. Acceptance checklist before handing over a customer installation

Use isolated fixtures for qualification; do not manufacture production sales or modify production expiry for tests.

- [ ] Correct supported Windows architecture/runtime and complete published folder.
- [ ] Release—not Debug—with approved public keys and correct stable endpoint.
- [ ] No paid private key, trial private key, passphrase, admin token or generator in the customer package.
- [ ] Starter Owner password changed; individual technical account secured.
- [ ] Actual company details and intended Windows data profile verified.
- [ ] Trial invitation/first activation tested against deployed PostgreSQL/Vercel.
- [ ] Trial reactivation does not reset start; duplicate registration denied.
- [ ] Offline/unavailable/expired trial cannot post protected business writes, with records/recovery preserved.
- [ ] PC clock changes do not extend trial in controlled qualification.
- [ ] Wrong-device/tampered/incorrect-key import denied safely.
- [ ] Paid import and repeated offline protected operation verified.
- [ ] Trial → lifetime preserves company data and stops online-license dependence.
- [ ] Same-entitlement hardware replacement and profile recovery qualified.
- [ ] Local encrypted backup, actual remote synced-folder completion and independent restore verified.
- [ ] Scanner/printer/drawer/display acceptance and representative accounting reconciliation completed.
- [ ] Issuer keys/passphrases/ledger and customer entitlement evidence backed up securely.
- [ ] Signed customer file and release/build identity retained for support.

Record results/date/operator; checked source/tests are not proof these field checks passed.

## 15. Short operational summary

**Trial:** establish separate trial server keys/database/HTTPS service → build trusted Release POS → install and provision technical account → export device request → programmer registers invitation on API → System Administrator activates online → seven-day period starts at first server activation → protected use requires online checks.

**Lifetime:** establish offline paid keys → build trusted Release POS → install and provision technical account → export device request → programmer issues paid JSON using GUI → System Administrator imports it → lifetime offline operation.

**Upgrade:** retain trial installation/database → programmer issues paid lifetime JSON → System Administrator imports it → same business data, no paid-license phone-home.

For implementation details see [licensing implementation](licensing-implementation.md), [trial API checklist](src/RetailForma.TrialApi/README.md) and [Phase 1 acceptance](phase1-implementation.md).

