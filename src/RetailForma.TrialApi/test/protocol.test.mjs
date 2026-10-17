import test from 'node:test';
import assert from 'node:assert/strict';
import { generateKeyPairSync } from 'node:crypto';
import { signed, readSigned, trialClaims, validateTrial } from '../lib/protocol.mjs';
const { privateKey } = generateKeyPairSync('rsa', { modulusLength: 3072 });
const record = { license_id: '4d52164e-4f2e-4fd1-919b-a4801b67085f', fingerprint: 'A'.repeat(64), started_at: '2026-10-13T00:00:00.000Z', revoked: false };
test('trial ends exactly seven days after immutable server start', () => {
 const claims = trialClaims(record); assert.equal(claims.Kind, 1); assert.equal(claims.ExpiresUtc, '2026-10-20T00:00:00.000Z');
 const token = signed(claims, privateKey);
 assert.equal(readSigned(validateTrial(token, record, 'B'.repeat(64), '2026-10-19T23:59:59Z', privateKey), privateKey).Active, true);
 assert.equal(readSigned(validateTrial(token, record, 'B'.repeat(64), claims.ExpiresUtc, privateKey), privateKey).Active, false);
});
test('revoked record denies trial and response binds challenge', () => {
 const token = signed(trialClaims(record), privateKey);
 const result = readSigned(validateTrial(token, { ...record, revoked: true }, 'C'.repeat(64), '2026-10-14T00:00:00Z', privateKey), privateKey);
 assert.equal(result.Active, false); assert.equal(result.Challenge, 'C'.repeat(64));
});
test('tampered dates and paid-kind claims cannot extend trial', () => {
 assert.throws(() => validateTrial(signed({ ...trialClaims(record), Kind: 2 }, privateKey), record, 'B'.repeat(64), '2026-10-14T00:00:00Z', privateKey));
 assert.throws(() => validateTrial(signed({ ...trialClaims(record), ExpiresUtc: '2027-10-20T00:00:00Z' }, privateKey), record, 'B'.repeat(64), '2026-10-14T00:00:00Z', privateKey));
 const token = signed(trialClaims(record), privateKey); token.Payload = Buffer.from('{}').toString('base64'); assert.throws(() => readSigned(token, privateKey));
});

test('legacy or missing signed product identity cannot grant a RetailForma trial', () => {
 const claims = trialClaims(record);
 for (const Product of ['MyPos.Basic', undefined]) {
  const changed = { ...claims, Product };
  assert.throws(() => validateTrial(signed(changed, privateKey), record, 'B'.repeat(64), '2026-10-14T00:00:00Z', privateKey));
 }
});
