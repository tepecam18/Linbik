import jwt from 'jsonwebtoken'

/** Accept the configured PEM public key or its base64 body. */
export function normalizePublicKey(key) {
  if (typeof key !== 'string' || !key.trim()) {
    throw new Error('Linbik public key is not configured')
  }
  const trimmed = key.trim()
  if (trimmed.startsWith('-----BEGIN PUBLIC KEY-----')) return trimmed
  const lines = trimmed.replace(/\s/g, '').match(/.{1,64}/g)
  return `-----BEGIN PUBLIC KEY-----\n${lines.join('\n')}\n-----END PUBLIC KEY-----`
}

/** Signature and expiration are checked before any claims reach the page. */
export function verifyLoginToken(token, publicKey) {
  if (typeof token !== 'string' || !token.trim()) {
    throw new Error('A login token is required')
  }
  const claims = jwt.verify(token, normalizePublicKey(publicKey), { algorithms: ['RS512'] })
  if (typeof claims !== 'object' || claims === null) {
    throw new Error('Invalid login claims')
  }
  return claims
}
