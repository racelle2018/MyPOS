# Local lifetime activation test

October 13, 2026 (Asia/Manila). Test-only preparation; not production acceptance.

## Prepared and verified

- User created test paid keys in `D:/MyPos-TestKeys/Paid/mypos-paid-keys` using the generator GUI.
- Only `paid-public.pem` was inspected by the assistant; no private key/passphrase was opened or requested.
- Lifetime-only win-x64 framework-dependent Release POS published to `D:/MyPos/artifacts/RetailForma` (the single stable POS output).
- Separate self-contained technician GUI package published to `D:/MyPos/artifacts/RetailForma.TechnicianSetup`.
- Published POS assembly inspection confirms Release, matching embedded RSA-3072 paid PUBLIC key and no embedded trial key. POS startup was not invoked by this diagnostic.
- Public trust SHA-256: `93E2307EC160FC3DF66FB568D809CA9213AEDD61D3F0D962496EF86C0A244E69`.
- Package filename scan found no issuer/private-key-named artifacts. The build was supplied only the public PEM.
- Compatible .NET 10 Windows Desktop Runtime is installed on this computer.
- Live POS database/profile, production keys and other published app folders were not modified or launched.

## Required next: isolate the Windows profile

Do not launch this test POS under the Windows account used by your existing/live RetailForma. The app stores data in the current profile's `%APPDATA%/RetailForma`, regardless of which application folder you launch it from. A separate executable folder alone does not isolate the database or license cache.

Use a separate Windows test account/profile (for example `RetailFormaTest`) or an isolated test machine. Create/sign into that account yourself using Windows; credentials and security settings are not automated. Ensure the test account can read the complete published test packages. Keep private signing keys accessible only to the programmer's normal controlled account, not the POS test account.

## Test sequence — still pending

1. Under the separate test profile, launch `D:/MyPos/artifacts/RetailForma/RetailForma.exe`.
2. On its new database, sign in as `admin` with the starter password `admin123`; complete the required password change immediately. Do not reuse production credentials.
3. Set a clearly marked test company in General Settings and configure/verify an encrypted test backup.
4. Close RetailForma. Record this test profile's actual absolute `%APPDATA%/RetailForma/retailforma.db` path.
5. Open `D:/MyPos/artifacts/RetailForma.TechnicianSetup/RetailForma.TechnicianSetup.exe`, approve elevation, and Browse to the explicit TEST database. If elevation uses another Windows account, do not select its APPDATA by mistake. Verify the company/path and create the first individual System Administrator with private masked credentials entered locally.
6. Reopen the test POS normally under the test profile and sign into that technical application account.
7. Export its device request from System Utilities → Licensing; transfer the request to the programmer's account.
8. Open the generator under the programmer's account. Select the TEST request and `D:/MyPos-TestKeys/Paid/mypos-paid-keys/paid-private.encrypted.pem`. Use a new test entitlement ID, no replacement, no portal flag.
9. Verify the test company/fingerprint, generate the signed JSON, and enter your private TEST key passphrase locally. Keep the private PEM on the programmer's side.
10. Return only the signed JSON to the test profile and import it through Licensing.
11. Confirm successful lifetime verification/storage. Test normal sale/shift operations using clearly marked test data.
12. Restart the test POS without internet and verify continued protected operation. Do not deliberately change production network settings or transact against the live database for this test.

Record actual outcomes before checking off activation/offline acceptance. No trial API/Vercel is involved in this lifetime-only pilot.

For detailed provisioning, activation and recovery instructions, see [complete guide](pos-installation-activation-guide.md).
# Backup passphrase verification fix (1.2.1)

Updated POS package: `D:/MyPos/artifacts/RetailForma/RetailForma.exe`. Close RetailForma normally before updating this same folder; do not create another POS package as a workaround. Existing data/license cache and public licensing trust are unchanged. First setup uses Set passphrase; Change passphrase verifies the current value before allowing a replacement. ZIP restoration asks for that archive's original passphrase in its own dialog. Existing archives retain their original password. This is a source/build fix, not approval to test restores against a live database.

