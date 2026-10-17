import { randomBytes, randomUUID } from 'node:crypto';
import { authorized, body, database, fingerprint, hash, reply, failure } from '../lib/runtime.mjs';
export default async function handler(req, res) {
  if (!authorized(req)) return reply(res, 401, { Error: 'Unauthorized' });
  let connection;
  try {
    const data = await body(req);
    if (!fingerprint(data.Fingerprint) || typeof data.Company !== 'string' || !data.Company.trim() || data.Company.length > 200)
      return reply(res, 400, { Error: 'Valid company and fingerprint required' });
    const key = randomBytes(32).toString('base64url'), licenseId = randomUUID();
    connection = await database().connect(); await connection.query('BEGIN');
    const result = await connection.query('INSERT INTO license_trials(license_id,fingerprint,company,activation_key_hash) VALUES($1,$2,$3,$4) ON CONFLICT(fingerprint) DO NOTHING RETURNING license_id',
      [licenseId, data.Fingerprint, data.Company.trim(), hash(key)]);
    if (!result.rowCount) { await connection.query('ROLLBACK'); return reply(res, 409, { Error: 'A trial already exists for this fingerprint; it cannot be reset' }); }
    await connection.query("INSERT INTO license_trial_events(license_id,event_type) VALUES($1,'Registered')", [licenseId]);
    await connection.query('COMMIT');
    return reply(res, 201, { LicenseId: licenseId, ActivationKey: key, Fingerprint: data.Fingerprint });
  } catch (error) { if (connection) await connection.query('ROLLBACK').catch(() => {}); return failure(res, error); }
  finally { connection?.release(); }
}
