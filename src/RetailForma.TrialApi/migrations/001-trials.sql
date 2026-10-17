-- Separate licensing database. Never run against the local/company RetailForma database.
CREATE TABLE IF NOT EXISTS license_trials (
  license_id uuid PRIMARY KEY,
  fingerprint text UNIQUE NOT NULL,
  company text NOT NULL,
  activation_key_hash text NOT NULL,
  started_at timestamptz,
  revoked boolean NOT NULL DEFAULT false
);
CREATE TABLE IF NOT EXISTS license_trial_events (
  id bigserial PRIMARY KEY,
  license_id uuid NOT NULL REFERENCES license_trials(license_id),
  event_type text NOT NULL,
  occurred_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);
