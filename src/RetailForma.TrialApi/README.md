# RetailForma trial API — deployment checklist

Source-only Vercel-compatible Node API for the online seven-day trial. Paid lifetime issuance stays offline and separate. No deployed service is included.

1. Use an authorized Vercel project/plan and Node 22+ runtime. Set this directory as the project root; functions are under api/. Run npm ci and npm test before deployment.
2. Provision a separate PostgreSQL database with trusted TLS; run migrations/001-trials.sql once using controlled database administration. Never run this migration against the POS/company SQLite database. Keep database recovery copies; deleting trial records would erase anti-reset history.
3. In the License Generator GUI, open Signing keys and select Online trial — separate server key, then create it in a new private folder. Deploy only the encrypted trial private PEM/passphrase and trial administration secret using the host's secret settings. NEVER upload the paid private key. Public trial PEM goes into the client build.
4. Set TRIAL_DATABASE_URL to the TLS PostgreSQL URL without ssl query overrides, TRIAL_SIGNING_PRIVATE_KEY_PEM to the trial PEM, TRIAL_SIGNING_KEY_PASSPHRASE to its passphrase and TRIAL_ADMIN_TOKEN to an independent unpredictable 32+ character secret. No secrets belong in source control, logs, command history or chat.
5. Add provider-side abuse/rate controls, restricted administration, monitoring and database backups. The API bounds request/response sizes and connection timeouts but does not replace provider-side rate limiting. Do not expose the administration bearer token to customers.
6. With a programmer-controlled client, POST JSON to /api/admin-register-trial using Authorization: Bearer <secret>. Body has Company and Fingerprint (uppercase SHA-256 hex). Capture returned LicenseId and ActivationKey securely and give that invitation only to the intended client. Duplicate fingerprint registration returns 409; it never resets the trial.
7. Build Desktop with separate public keys and TrialLicenseServerUrl=https://your-host/api/trial-validate. System Administrator starts the trial using the invitation. /api/trial-start returns the signed token and fresh validation; /api/trial-validate requires a fresh challenge and uses database UTC.
8. Test database persistence across redeploys, replay/tamper denial, exact expiry, lost internet, server outage and paid offline checkout. No trial test should involve real business records. Registration revocation currently requires controlled database administration; there is no owner portal/admin dashboard.

Requests/responses use PascalCase fields shared with .NET. RSA-PSS SHA-256 uses a 32-byte salt and Base64 envelopes. Trial Kind is 1; duration is exactly 7 × 24 hours from immutable activation UTC. Trial outage is reported as unavailable, not automatically classified as expired. Logs omit keys/request bodies/database URLs.

The three local Node protocol tests do not prove a deployed PostgreSQL/Vercel integration. Deployment, provider plan eligibility and production acceptance remain outstanding.
