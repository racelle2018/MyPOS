import { body, database, fingerprint, hash, id, reply, signer, failure } from '../lib/runtime.mjs';
import { signed, trialClaims, validateTrial } from '../lib/protocol.mjs';
export default async function handler(req, res) {
  let connection;
  try {
    const data = await body(req);
    if (!id(data.LicenseId) || !fingerprint(data.Fingerprint) || !fingerprint(data.Challenge) || typeof data.ActivationKey !== 'string' || !/^[A-Za-z0-9_-]{43}$/.test(data.ActivationKey))
      return reply(res, 400, { Error: 'Invalid activation request' });
    const key = signer();
    connection = await database().connect(); await connection.query('BEGIN');
    const result = await connection.query('UPDATE license_trials SET started_at=COALESCE(started_at,date_trunc(\'milliseconds\',CURRENT_TIMESTAMP)) WHERE license_id=$1 AND fingerprint=$2 AND activation_key_hash=$3 AND NOT revoked RETURNING *',
      [data.LicenseId, data.Fingerprint, hash(data.ActivationKey)]);
    if (!result.rowCount) { await connection.query('ROLLBACK'); return reply(res, 403, { Error: 'Activation denied' }); }
    const record = result.rows[0];
    const now = (await connection.query("SELECT date_trunc('milliseconds',CURRENT_TIMESTAMP) AS now")).rows[0].now;
    const license = signed(trialClaims(record), key);
    const validation = validateTrial(license, record, data.Challenge, now, key);
    await connection.query("INSERT INTO license_trial_events(license_id,event_type) VALUES($1,'ActivationRequested')", [record.license_id]);
    await connection.query('COMMIT');
    return reply(res, 200, { License: license, Validation: validation });
  } catch (error) { if (connection) await connection.query('ROLLBACK').catch(() => {}); return failure(res, error); }
  finally { connection?.release(); }
}
