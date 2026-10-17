# RetailForma Technician Setup

Technician Setup is for initial company provisioning, importing an existing company, or programmer-authorized account recovery. It requires the installed `RetailFormaData` service and must be run elevated.

## First-time setup on this PC

1. Create the paid-lifetime signing key pair and the separate developer-recovery key pair in License Generator. Keep their encrypted private keys and passphrases safe; the installer uses only public keys.
2. Build the installer with those public-key files, then run it and approve the Windows prompt. The default application folder is `C:\RetailForma`. Installation adds the protected data service but does not create a company or user account.
3. Open Technician Setup as administrator. From `D:\MyPos`, while developing, launch it with:

   ```powershell
   Start-Process -FilePath "D:\MyPos\src\RetailForma.Setup\bin\Release\net10.0-windows\RetailForma.Setup.exe" -Verb RunAs
   ```

   After installation, launch Technician Setup from its installed shortcut/location.
4. On **Initial setup**, select **Connect**. Review the service status and company information. Connecting only checks the service; it does not create or modify a company.
5. If the service reports no company, enter the requested **Company name** as it should appear in RetailForma. If a company is already configured, that field is not needed; the tool can provision its first System Administrator only when none exists.
6. Enter an individual **Administrator username** and **Administrator full name**. Choose a strong unique password between 16 and 256 characters, enter it again under **Confirm password**, and store it securely. This is the RetailForma login password, not a license-key or backup passphrase.
7. Confirm that the displayed company and installation are the intended target, then choose **Create account** and accept the confirmation. The tool creates the first System Administrator. It does not create the Owner or Cashier accounts.
8. Open RetailForma and sign in with that administrator account. Create individual Owner and Cashier accounts from user management.

## Import an existing company

Use **Import company** only when moving existing RetailForma data. Select the verified encrypted backup ZIP and enter its original backup passphrase. For a supported legacy `.db` file, choose a new recovery passphrase when prompted by the tool's import flow. Keep the original database and backup in a separate safe location, confirm the old POS is closed, and review the final confirmation carefully. Do not use this path to create a new empty company.

## Recover administrator access

Use **Account recovery** only for an existing company whose System Administrator cannot sign in. Enter the exact username and export the request. The programmer reviews and signs that request in License Generator using the separate recovery key. Select the returned signed authorization, enter and confirm a temporary password, then complete the reset. The authorization is one-use and expires after five minutes; the user must change the temporary password at next sign-in.

Never use account recovery to bypass normal account management or to change roles. A technician cannot sign an authorization without the programmer's private recovery key.
