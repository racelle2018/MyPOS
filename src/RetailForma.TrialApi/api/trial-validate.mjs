import { body, database, reply, signer, failure } from '../lib/runtime.mjs';
import { readSigned, validateTrial } from '../lib/protocol.mjs';
export default async function handler(req, res) {
  try {
    const data = await body(req), key = signer(), claims = readSigned(data.License, key);
    const result = await database().query("SELECT *, date_trunc('milliseconds',CURRENT_TIMESTAMP) AS server_now FROM license_trials WHERE license_id=$1 AND fingerprint=$2", [claims.LicenseId, claims.Fingerprint]);
    if (!result.rowCount) return reply(res, 403, { Error: 'Trial denied' });
    return reply(res, 200, validateTrial(data.License, result.rows[0], data.Challenge, result.rows[0].server_now, key));
  } catch (error) { return failure(res, error); }
}
