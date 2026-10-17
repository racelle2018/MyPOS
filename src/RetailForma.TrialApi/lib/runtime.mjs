import pg from 'pg';
import { randomUUID, createHash, timingSafeEqual } from 'node:crypto';
import { privateSigner } from './protocol.mjs';
let pool;
export function database() {
  const url = process.env.TRIAL_DATABASE_URL;
  if (!url) throw new Error('License database not configured');
  const parsed = new URL(url);
  if (!['postgres:', 'postgresql:'].includes(parsed.protocol) || [...parsed.searchParams.keys()].some(k => k.startsWith('ssl')))
    throw new Error('Use a Postgres connection URL without overriding TLS options');
  return pool ??= new pg.Pool({ connectionString: url, ssl: { rejectUnauthorized: true }, max: 2,
    connectionTimeoutMillis: 5000, idleTimeoutMillis: 5000 });
}
export function signer() {
  if (!process.env.TRIAL_SIGNING_PRIVATE_KEY_PEM) throw new Error('Trial signing key not configured');
  return privateSigner(process.env.TRIAL_SIGNING_PRIVATE_KEY_PEM, process.env.TRIAL_SIGNING_KEY_PASSPHRASE);
}
export function hash(text) { return createHash('sha256').update(text, 'utf8').digest('hex'); }
export function authorized(req) {
  const secret = process.env.TRIAL_ADMIN_TOKEN;
  if (!secret || secret.length < 32) return false;
  const received = Buffer.from(req.headers.authorization ?? ''); const expected = Buffer.from('Bearer ' + secret);
  return received.length === expected.length && timingSafeEqual(received, expected);
}
export async function body(req) {
  if (req.method !== 'POST' || !(req.headers['content-type'] ?? '').startsWith('application/json')) throw new Error('POST JSON required');
  let input;
  if (req.body != null) input = typeof req.body === 'string' ? req.body : JSON.stringify(req.body);
  else { const chunks = []; let size = 0; for await (const chunk of req) { size += chunk.length; if (size > 16384) throw new Error('Request too large'); chunks.push(chunk); } input = Buffer.concat(chunks).toString('utf8'); }
  if (Buffer.byteLength(input) > 16384) throw new Error('Request too large');
  return JSON.parse(input);
}
export function id(value) { return typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value); }
export function fingerprint(value) { return typeof value === 'string' && /^[0-9A-F]{64}$/.test(value); }
export function reply(res, status, value) { res.setHeader('Cache-Control', 'no-store'); return res.status(status).json(value); }
export function failure(res, error) {
  console.error('Trial API failure', error.code ?? error.name); // No key, request body or database URL logging.
  return reply(res, 503, { Error: 'Trial validation unavailable', Incident: randomUUID() });
}
