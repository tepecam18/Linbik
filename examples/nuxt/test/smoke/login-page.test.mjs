import test from 'node:test'
import assert from 'node:assert/strict'
import { generateKeyPairSync } from 'node:crypto'
import { spawn } from 'node:child_process'
import { createServer } from 'node:net'
import { setTimeout as delay } from 'node:timers/promises'
import jwt from 'jsonwebtoken'

test('built login page accepts verified callbacks and isolates requests', { timeout: 20000 }, async () => {
  const { publicKey, privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 })
  const portProbe = createServer()
  await new Promise(resolve => portProbe.listen(0, '127.0.0.1', resolve))
  const port = portProbe.address().port
  await new Promise(resolve => portProbe.close(resolve))
  const child = spawn(process.execPath, ['.output/server/index.mjs'], {
    windowsHide: true,
    env: { ...process.env, PORT: String(port), HOST: '127.0.0.1',
      NUXT_LINBIK_APP_SECRET: publicKey.export({ type: 'spki', format: 'pem' }).toString() },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  let logs = ''
  child.stdout.on('data', chunk => { logs += chunk })
  child.stderr.on('data', chunk => { logs += chunk })
  const base = `http://127.0.0.1:${port}`
  try {
    let ready = false
    for (let attempt = 0; attempt < 80; attempt++) {
      try { const response = await fetch(base, { signal: AbortSignal.timeout(1000) }); await response.text(); ready = true; break }
      catch { if (child.exitCode !== null) break; await delay(100) }
    }
    assert.ok(ready, `Nuxt did not start: ${logs}`)
    const token = jwt.sign({ name: 'SmokeTestUser' }, privateKey, { algorithm: 'RS512', expiresIn: '1h' })
    const login = await fetch(`${base}/login`, {
      method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ token }),
    })
    assert.equal(login.status, 200)
    assert.match(await login.text(), /SmokeTestUser/)
    assert.match(login.headers.get('set-cookie'), /HttpOnly/i)
    const anonymous = await fetch(`${base}/login`)
    assert.equal(anonymous.status, 200)
    assert.doesNotMatch(await anonymous.text(), /SmokeTestUser/)
    const invalid = await fetch(`${base}/login`, {
      method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ token: 'invalid' }),
    })
    assert.equal(invalid.status, 401)
    assert.equal(invalid.headers.get('set-cookie'), null)
  } finally {
    child.kill()
    if (child.exitCode === null) await new Promise(resolve => child.once('exit', resolve))
  }
})
