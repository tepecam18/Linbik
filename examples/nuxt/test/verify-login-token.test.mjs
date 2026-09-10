import test from 'node:test'
import assert from 'node:assert/strict'
import { generateKeyPairSync } from 'node:crypto'
import jwt from 'jsonwebtoken'
import { normalizePublicKey, verifyLoginToken } from '../server/utils/verify-login-token.mjs'

const { publicKey, privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 })
const pem = publicKey.export({ type: 'spki', format: 'pem' }).toString()

test('verifies RS512 token using a PEM key or its base64 body', () => {
  const token = jwt.sign({ name: 'Alice' }, privateKey, { algorithm: 'RS512', expiresIn: '1h' })
  const body = pem.replace(/-----[^-]+-----/g, '').replace(/\s/g, '')
  assert.equal(verifyLoginToken(token, pem).name, 'Alice')
  assert.equal(verifyLoginToken(token, body).name, 'Alice')
})

test('rejects expired tokens and algorithms outside the configured contract', () => {
  const expired = jwt.sign({ name: 'Alice' }, privateKey, { algorithm: 'RS512', expiresIn: -1 })
  const wrongAlgorithm = jwt.sign({ name: 'Alice' }, privateKey, { algorithm: 'RS256' })
  assert.throws(() => verifyLoginToken(expired, pem))
  assert.throws(() => verifyLoginToken(wrongAlgorithm, pem))
})

test('rejects missing configuration and malformed input', () => {
  assert.throws(() => normalizePublicKey(''))
  assert.throws(() => verifyLoginToken(undefined, pem))
  assert.throws(() => verifyLoginToken('malformed', pem))
})
