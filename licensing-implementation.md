# Licensing implementation

October 13, 2026. Source implementation is ready for isolated deployment testing; production activation is not configured or accepted.

For the complete programmer/technician workflow, see [POS installation, trial and lifetime activation guide](pos-installation-activation-guide.md).

## Approved policy and implemented behavior

- Programmer-issued, single-purchase lifetime license per device; no expiry, internet requirement or periodic renewal check. Owner portal connectivity is separate and remains Phase 2.
- Hardware fingerprint hashes motherboard serial, CPU ID and Windows Machine GUID. Hardware collection is implemented but has not been run on the client's machine during this work.
- Separate RSA-3072+ paid and trial trust keys. The client receives public keys only. Paid private keys remain offline; the trial server cannot issue a trusted paid license.
- Signed tokens are stored with AES-256-GCM; the random storage key is protected for the current Windows profile by DPAPI. Signature/device verification remains mandatory; encryption alone grants nothing.
- Seven-day trials require fresh HTTPS, challenge-bound signed server UTC validation before protected business writes. No cached offline permission or local-clock extension. Connection failure means unavailable, not proven expired. Reports/login/recovery remain available; no business data is deleted.
- System Administrator-only activation page exports device requests, imports signed paid files and starts an online trial from a programmer-issued invitation. The application role does not confer license-signing authority.
- Financial operations check before mutation and again at save. Transaction rollback also restores tracked state so failed postings cannot leak into later saves.

## Offline issuer (separate programmer tool)

The only license-generator project is the standalone WPF GUI, `src/RetailForma.LicenseIssuer.Gui`. Its backend is contained in `Backend/IssuerWorkspace.cs`; the old `src/RetailForma.LicenseIssuer` project/folder is no longer needed. This tool does not belong in the customer installer. Use absolute paths outside this repository and client installation. No production keys were generated here.

From `D:\MyPos`, open the GUI with:

```powershell
dotnet run --project src/RetailForma.LicenseIssuer.Gui --configuration Release
```

Signing keys creates separate paid/trial encrypted key sets in a new folder, using a masked confirmed 16+ character passphrase. Generate license loads a POS-exported request, displays the company/fingerprint, selects an encrypted paid key and confirms ownership/payment before saving a signed lifetime JSON. Hardware replacement requires the existing entitlement and explicit approval; New ID is disabled in replacement mode. Issuance history is read-only and displays Philippine time. Passphrases are not persisted/logged; actions use the same close-only RetailForma success/warning/error/confirmation dialogs and sounds. Forms scroll with fixed action buttons, default Inter/12-DIP controls and centralized 1-DIP outlines. No POS database or customer-app startup is invoked by the generator.

To publish a double-clickable programmer tool (framework-dependent, requires .NET 10 Desktop Runtime):

```powershell
dotnet publish src/RetailForma.LicenseIssuer.Gui -c Release -o artifacts/retailforma-license-generator
```

Open `artifacts/retailforma-license-generator/RetailForma.LicenseGenerator.exe`. Keep the complete published folder together and separate from customer distribution. Trial activation still uses the online trial server; creating trial signing keys is not offline trial issuance.

Key creation requires a new folder and matching 16+ character passphrase entered in the GUI's masked dialog; files are never overwritten. Securely back up encrypted private keys, passphrases and the exclusive `issuer-journal.jsonl` separately from the POS.

After ownership verification, select Approved hardware replacement and enter the existing entitlement ID. The issuance ledger records the retired fingerprint and signed-file hash. Select Owner portal entitlement only when approved; it does not implement the portal. A dead PC need not deactivate, and an old permanently offline token cannot be remotely revoked immediately. One-device use remains contractual. File publication and ledger append are not a distributed atomic transaction: after an interrupted issuer operation, reconcile any output file/hash with the ledger before retrying or delivering it.

## Build/client provisioning

Publish Desktop with MSBuild properties `PaidLicensePublicKey` and, for trials, `TrialLicensePublicKey` pointing to public SPKI PEM files, plus `TrialLicenseServerUrl=https://your-approved-host/api/trial-validate`. The client rejects private PEMs, weak keys, identical paid/trial keys and non-HTTPS endpoints.

An unprovisioned Release build denies protected business writes; it still allows login/reports/recovery. Debug builds without configured keys permit development only and must never be delivered to a store. Cache lives under the current user's AppData RetailForma/licensing directory. Moving to another Windows profile requires reactivation/import; a machine replacement follows authorized transfer, not manual cache copying.

## Trial server

`src/RetailForma.TrialApi` implements Vercel-compatible Node functions and a separate PostgreSQL trial database. Programmer-authenticated invitation creation prevents repeated trials for the same fingerprint. First activation fixes the start timestamp in server/database UTC; reactivation never resets it. Revoked/expired records cannot authorize business use.

See [trial API setup](src/RetailForma.TrialApi/README.md). No project, domain, database, cloud account or live key was provisioned/deployed. No customer data was uploaded. Plan eligibility, rate limiting, secrets, database provisioning and live end-to-end acceptance remain required. Use an approved commercial-use hosting plan; the earlier choice of testing first does not itself establish free-plan eligibility.

## Acceptance still required

Provision separate keys outside source control; build a Release installer with public trust only; deploy the approved HTTPS endpoint/database; test fresh/repeated/expired/offline trials, permanently offline paid checkout, authorized replacement and recovery under another Windows profile. Test real hardware identifiers and installer packaging. Local protocol/client tests are not a deployment or security certification.
# RetailForma 1.2.0 identity cutover

New device requests, paid claims and trial claims/validation responses use the explicit signed product `RetailForma.Basic`. Old tokens missing that signed property or identifying another product are rejected; never edit a signed payload. Existing separate RSA key pairs remain valid issuer keys. Export a fresh request and issue/import a fresh test token after migration. Cache envelope version 2 uses `RETAILFORMA-LICENSE-2` AES associated data; old test cache files are archived intact, not silently activated under the new identity. Trial start/expiry history must not be reset. See [migration plan](retailforma-migration-plan.md).

