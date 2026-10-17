# RetailForma branding decisions

Approved product name: **RetailForma**.

Approved by the owner in chat on October 13, 2026 (Asia/Manila), replacing RetailForma/MyPOS as the intended public-facing brand.

## Proposed supporting identity

- Description: Configurable Retail Management.
- Tagline: Shaped around your business.
- Positioning: an integrated retail-management product with company-specific configuration, not merely a checkout application. Describe only supported functions as available; planned portal/LAN/multi-branch features remain Phase 2.
- Visual direction: retain the approved light UI language, Inter typography, compact controls and existing blue #2878BD. No color-customization feature is implied.
- Approved logo: Option 1, the blue RF monogram with a RetailForma wordmark; white RF on a blue rounded square for the application icon. Owner approved preparation and application to the POS and License Generator on October 13, 2026.

The product name and RF logo are approved. The descriptor and tagline remain proposals, not separately approved final copy.

## Implementation and compatibility boundary

Visible branding is implemented across application/login titles, version labels, messages, PDF/print metadata, generator/setup tools and guides. Public published launchers are `RetailForma.exe`, `RetailForma.LicenseGenerator.exe` and `RetailForma.TechnicianSetup.exe`. The approved logo is applied to the POS login/window icon and License Generator header/window icon, with embedded executable icons for both. Installer implementation remains pending. Step 1 also renames solution/projects/namespaces, DbContext classes, assemblies, WPF resources and developer commands to RetailForma; data/licensing identities remain compatible pending step 2.

Step 2 uses RetailForma data/licensing identities for new work and a copy-only, verified migration from the old profile. Owner chose migration and confirmed no customer licenses were issued. Preserve the old data as rollback, read old backups, and archive old test activations rather than editing signed tokens. Existing signing keys remain unchanged. See `retailforma-migration-plan.md` for prerequisites and acceptance. The full published folder must remain together.

The application's company/customer branding on reports and receipts remains distinct from the software vendor's RetailForma identity.

Verification on 2026-10-13: 124 .NET tests and 3 trial-API protocol tests passed. Published launchers carry RetailForma Windows product/title metadata; the lifetime-test POS still embeds the same paid public trust key. No production database, private key or installed license was modified.

Domain and trademark availability have not been cleared; owner selection is not trademark registration or clearance.
