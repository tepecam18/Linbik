import { test } from 'node:test'
import assert from 'node:assert/strict'
import { createServer } from 'node:http'
import { randomBytes } from 'node:crypto'
import { spawn } from 'node:child_process'
import { setTimeout as delay } from 'node:timers/promises'
import { readdir, readFile } from 'node:fs/promises'

test('standalone Nitro login, callback, session, protected API, SSR/CSR and logout', async () => {
  const codes = new Map()
  const calls = []
  let identityOrigin
  const identity = createServer(async (req, res) => {
    const chunks = []
    for await (const chunk of req) chunks.push(chunk)
    const body = JSON.parse(Buffer.concat(chunks).toString() || '{}')
    calls.push(req.url)
    assert.equal(req.headers.apikey, 'fixture-api-secret')
    let data
    if (req.url === '/api/oauth/initiate') {
      const code = String(codes.size + 1)
      codes.set(code, body.codeChallenge)
      data = { redirectUrl: `${identityOrigin}/approve?code=${code}` }
    } else if (req.url === '/api/oauth/token') {
      const challenge = codes.get(req.headers.code)
      assert.ok(challenge)
      data = { userId: 'aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa', clientId: 'bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb', username: 'alice', displayName: 'Alice', codeChallenge: challenge,
        refreshToken: 'fixture-upstream-refresh-secret', integrations: [{ packageName: 'billing', token: 'fixture-integration-secret' }] }
    } else { res.statusCode = 404; return res.end() }
    res.setHeader('Content-Type', 'application/json')
    res.end(JSON.stringify({ isSuccess: true, data }))
  })
  await new Promise(resolve => identity.listen(0, '127.0.0.1', resolve))
  identityOrigin = `http://127.0.0.1:${identity.address().port}`
  const reservation = createServer()
  await new Promise(resolve => reservation.listen(0, '127.0.0.1', resolve))
  const port = reservation.address().port
  await new Promise(resolve => reservation.close(resolve))
  const origin = `http://127.0.0.1:${port}`
  const key = 'k4.local.' + randomBytes(32).toString('base64url')
  let output = ''
  const child = spawn(process.execPath, ['.output/server/index.mjs'], {
    cwd: new URL('..', import.meta.url), windowsHide: true,
    env: { ...process.env, PORT: String(port), HOST: '127.0.0.1', NUXT_PUBLIC_LINBIK_WEB_ORIGIN: origin,
      NUXT_LINBIK_API_BASE_URL: identityOrigin, NUXT_LINBIK_API_KEY: 'fixture-api-secret',
      NUXT_LINBIK_SERVICE_ID: 'cccccccc-cccc-4ccc-cccc-cccccccccccc', NUXT_LINBIK_CLIENT_ID: 'bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb',
      NUXT_LINBIK_SESSION_KEY: key, NUXT_LINBIK_ALLOW_INSECURE_HTTP: 'true' }
  })
  child.stdout.on('data', data => { output += data })
  child.stderr.on('data', data => { output += data })
  const jar = new Map()
  const cookieHeader = () => [...jar].map(([k, v]) => `${k}=${v}`).join('; ')
  const request = async (path, init = {}, updateJar = true) => {
    const res = await fetch(origin + path, { ...init, headers: { cookie: cookieHeader(), ...init.headers }, redirect: 'manual' })
    if (updateJar) for (const cookie of res.headers.getSetCookie()) {
      const pair = cookie.split(';')[0]
      const i = pair.indexOf('=')
      if (pair.slice(i + 1)) jar.set(pair.slice(0, i), pair.slice(i + 1)); else jar.delete(pair.slice(0, i))
    }
    return res
  }
  try {
    let ready = false
    for (let i = 0; i < 100; i++) {
      try { const res = await request('/'); if (res.ok) { ready = true; break } } catch {}
      await delay(100)
    }
    assert.ok(ready, output)
    assert.equal((await request('/api/Linbik/session')).status, 401)
    assert.equal((await request('/api/protected')).status, 401)
    assert.equal((await request('/api/Linbik/callback?code=unsolicited')).status, 400)
    assert.equal(calls.length, 0)
    const login = await request('/api/Linbik/login?returnPath=%2Fprotected')
    assert.equal(login.status, 302, await login.text())
    const code = new URL(login.headers.get('location')).searchParams.get('code')
    assert.ok(jar.has('linbik_flow'))
    const callback = await request('/api/Linbik/callback?code=' + code)
    assert.equal(callback.status, 302, await callback.text())
    assert.equal(callback.headers.get('location'), '/protected')
    assert.equal(jar.has('linbik_flow'), false)
    assert.match(jar.get('linbik_access'), /^v4\.local\./)
    assert.ok(callback.headers.getSetCookie().every(cookie => cookie.includes('HttpOnly')))
    assert.doesNotMatch(cookieHeader(), /fixture-api-secret|fixture-upstream-refresh-secret|fixture-integration-secret/)
    const session = await request('/api/Linbik/session')
    assert.equal((await session.json()).data.username, 'alice')
    const api = await request('/api/protected')
    assert.equal((await api.json()).user.username, 'alice')
    const page = await request('/protected')
    const html = await page.text()
    assert.equal(page.status, 200, html)
    if (process.env.NUXT_TEST_SSR !== 'false') assert.match(html, /Merhaba, Alice/)
    else assert.doesNotMatch(html, /Merhaba, Alice/)
    assert.doesNotMatch(html, /fixture-api-secret|fixture-upstream-refresh-secret|fixture-integration-secret|k4\.local\./)
    assert.equal(calls.filter(path => path.endsWith('/token')).length, 1)
    assert.equal(calls.filter(path => path.endsWith('/refresh')).length, 0)
    assert.equal((await request('/api/Linbik/logout')).status, 405)
    assert.equal((await request('/api/Linbik/logout', { method: 'POST', headers: { origin: 'https://evil.test', 'x-linbik-request': '1' } })).status, 403)
    // A missing/invalid access token may be restored using the server-held session.
    jar.set('linbik_access', 'expired')
    if (process.env.NUXT_TEST_SSR !== 'false') {
      const restored = await request('/protected')
      assert.equal(restored.status, 200)
      assert.match(await restored.text(), /Merhaba, Alice/)
      assert.match(jar.get('linbik_access'), /^v4\.local\./)
    }
    const refresh = await request('/api/Linbik/refresh', { method: 'POST', headers: { origin, 'x-linbik-request': '1' } })
    assert.equal(refresh.status, 200)
    const copiedCookies = cookieHeader()
    assert.equal((await request('/api/Linbik/logout', { method: 'POST', headers: { origin, 'x-linbik-request': '1' } })).status, 200)
    assert.equal(jar.size, 0)
    const revoked = await request('/api/Linbik/session', { headers: { cookie: copiedCookies } }, false)
    assert.equal(revoked.status, 401)
    assert.equal((await request('/api/protected')).status, 401)
    if (process.env.NUXT_TEST_SSR !== 'false') assert.equal((await request('/protected')).status, 302)
    for (const filename of await readdir(new URL('../.output/public/_nuxt', import.meta.url))) {
      if (filename.endsWith('.js')) {
        const bundle = await readFile(new URL('../.output/public/_nuxt/' + filename, import.meta.url), 'utf8')
        assert.doesNotMatch(bundle, /fixture-api-secret|fixture-upstream-refresh-secret|createLinbikAuth|MemorySessionStore/)
      }
    }
  } finally {
    if (child.exitCode === null) { child.kill(); await new Promise(resolve => child.once('exit', resolve)) }
    await new Promise(resolve => identity.close(resolve))
  }
})
