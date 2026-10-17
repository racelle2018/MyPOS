import { createPrivateKey, createPublicKey, sign, verify, constants } from 'node:crypto';
export function privateSigner(pem, passphrase) {
  const key = createPrivateKey({ key: pem, passphrase });
  if (key.asymmetricKeyType !== 'rsa' || key.asymmetricKeyDetails.modulusLength < 3072)
    throw new Error('A separate RSA-3072 or stronger trial signing key is required');
  return key;
}
export function signed(value, key) {
  const payload = Buffer.from(JSON.stringify(value), 'utf8');
  return { Payload: payload.toString('base64'), Signature: sign('sha256', payload,
    { key, padding: constants.RSA_PKCS1_PSS_PADDING, saltLength: 32 }).toString('base64') };
}
export function readSigned(message, key) {
  if (!message || typeof message.Payload !== 'string' || message.Payload.length > 24000 ||
      typeof message.Signature !== 'string' || message.Signature.length > 2048) throw new Error('Invalid envelope');
  const bytes = Buffer.from(message.Payload, 'base64');
  if (!verify('sha256', bytes, { key: createPublicKey(key), padding: constants.RSA_PKCS1_PSS_PADDING, saltLength: 32 },
      Buffer.from(message.Signature, 'base64'))) throw new Error('Invalid signature');
  return JSON.parse(bytes.toString('utf8'));
}
export function trialClaims(record) {
  const issued = new Date(record.started_at);
  if (!record.started_at || !Number.isFinite(issued.getTime())) throw new Error('Trial has not started');
  return { Format: 1, Product: 'RetailForma.Basic', LicenseId: record.license_id, Fingerprint: record.fingerprint, Kind: 1,
    IssuedUtc: issued.toISOString(), ExpiresUtc: new Date(issued.getTime() + 7 * 86400000).toISOString(), Features: ['Basic'] };
}
export function validateTrial(message, record, challenge, serverUtc, key) {
  const claims = readSigned(message, key);
  const expected = trialClaims(record);
  if (!/^[A-F0-9]{64}$/.test(challenge) || claims.Product !== expected.Product || claims.Format !== 1 || claims.Kind !== 1 ||
      claims.LicenseId !== expected.LicenseId || claims.Fingerprint !== expected.Fingerprint ||
      Date.parse(claims.IssuedUtc) !== Date.parse(expected.IssuedUtc) || Date.parse(claims.ExpiresUtc) !== Date.parse(expected.ExpiresUtc) ||
      !Array.isArray(claims.Features) || claims.Features.length !== 1 || claims.Features[0] !== 'Basic') throw new Error('Incompatible trial');
  const now = new Date(serverUtc);
  const active = !record.revoked && now >= new Date(expected.IssuedUtc) && now < new Date(expected.ExpiresUtc);
  return signed({ Format: 1, Product: 'RetailForma.Basic', LicenseId: record.license_id, Fingerprint: record.fingerprint,
    Challenge: challenge, ServerUtc: now.toISOString(), Active: active }, key);
}
