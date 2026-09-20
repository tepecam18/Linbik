import { test } from 'node:test'
import assert from 'node:assert/strict'
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import { setTimeout as delay } from 'node:timers/promises'

// Test the built Nuxt server against a local backend fixture, never a live account.
test('Nuxt session boundary, rendering, refresh and CSRF', async () => {
  const calls = []
  const backend = createServer((req, res) => {
    const cookie = req.headers.cookie ?? ''
    calls.push([req.url, cookie])
    res.setHeader('Content-Type', 'application/json')
    if (req.url === '/api/Linbik/logout') {
      res.setHeader('Set-Cookie', ['authToken=; Max-Age=0; HttpOnly; Path=/', 'linbikRefreshToken=; Max-Age=0; HttpOnly; Path=/'])
      return res.end(JSON.stringify({ isSuccess: true }))
    }
    if (req.url === '/api/Linbik/refresh') {
      if (!cookie.includes('linbikRefreshToken=valid')) { res.statusCode = 401; return res.end() }
      res.setHeader('Set-Cookie', ['authToken=alice; Domain=backend.test; HttpOnly; Path=/', 'linbikRefreshToken=rotated; HttpOnly; Path=/'])
    } else if (!cookie.includes('authToken=alice') && !cookie.includes('authToken=bob')) {
      res.statusCode = cookie.includes('authToken=outage') ? 503 : 401
      return res.end()
    }
    const name = cookie.includes('authToken=bob') ? 'bob' : 'alice'
    res.end(JSON.stringify({ isSuccess: true, data: { userId: name, username: name, displayName: name, integrations: [] } }))
  })
  await new Promise(resolve => backend.listen(0, '127.0.0.1', resolve))
  const reservation = createServer()
  await new Promise(resolve => reservation.listen(0, '127.0.0.1', resolve))
  const port = reservation.address().port
  await new Promise(resolve => reservation.close(resolve))
  const origin = `http://127.0.0.1:${port}`
  let output = ''
  const child = spawn(process.execPath, ['.output/server/index.mjs'], {
    cwd: new URL('..', import.meta.url), windowsHide: true,
    env: { ...process.env, PORT: String(port), HOST: '127.0.0.1',
      NUXT_LINBIK_BACKEND_BASE_URL: `http://127.0.0.1:${backend.address().port}`,
      NUXT_PUBLIC_LINBIK_WEB_ORIGIN: origin }
  })
  child.stdout.on('data', data => { output += data })
  child.stderr.on('data', data => { output += data })
  try {
    let ready = false
    for (let n = 0; n < 100; n++) {
      try { await fetch(origin); ready = true; break } catch { await delay(100) }
    }
    assert.ok(ready, output)
    const request = (path, cookie = '', init = {}) => fetch(origin + path, { ...init, headers: { cookie, ...init.headers }, redirect: 'manual' })
    if (process.env.NUXT_TEST_SSR !== 'false') {
      const responses = await Promise.all(['alice', 'bob'].map(name => request('/protected', `authToken=${name}`)))
      const pages = await Promise.all(responses.map(response => response.text()))
      assert.ok(responses.every(response => response.status === 200), output)
      assert.match(pages[0], /Merhaba, alice/)
      assert.doesNotMatch(pages[0], /Merhaba, bob/)
      assert.match(pages[1], /Merhaba, bob/)
      assert.ok(!calls.some(([path]) => path === '/api/Linbik/refresh'))
      const anonymous = await request('/protected')
      assert.equal(anonymous.status, 302)
      assert.equal(anonymous.headers.get('location'), '/')
      const renewed = await request('/protected', 'authToken=expired; linbikRefreshToken=valid')
      assert.equal(renewed.status, 200)
      assert.match(await renewed.text(), /Merhaba, alice/)
      assert.equal(renewed.headers.getSetCookie().length, 2)
      assert.ok(renewed.headers.getSetCookie().every(cookie => !cookie.includes('Domain=')))
      const outage = await request('/protected', 'authToken=outage')
      assert.equal(outage.status, 503)
    } else {
      const page = await request('/protected', 'authToken=alice')
      assert.equal(page.status, 200)
      assert.doesNotMatch(await page.text(), /Merhaba, alice/)
      assert.equal(calls.length, 0, 'CSR must not inspect a session while generating the shell')
    }
    const session = await request('/api/auth/session', 'authToken=alice; unrelated=secret')
    assert.equal((await session.json()).data.username, 'alice')
    assert.equal(session.headers.get('cache-control'), 'private, no-store')
    assert.ok(calls.every(([, cookie]) => !cookie.includes('unrelated=')))
    const anonymous = await request('/api/auth/session')
    assert.equal(anonymous.status, 401)
    const rejected = await request('/api/auth/refresh', 'linbikRefreshToken=valid', { method: 'POST', headers: { origin: 'https://attacker.test', 'x-linbik-request': '1' } })
    assert.equal(rejected.status, 403)
    assert.equal((await request('/api/auth/logout')).status, 405)
    const refresh = await request('/api/auth/refresh', 'linbikRefreshToken=valid', { method: 'POST', headers: { origin, 'x-linbik-request': '1' } })
    assert.equal(refresh.status, 200)
    assert.equal(refresh.headers.getSetCookie().length, 2)
    const logout = await request('/api/auth/logout', 'authToken=alice', { method: 'POST', headers: { origin, 'x-linbik-request': '1' } })
    assert.equal(logout.status, 200)
    assert.equal(logout.headers.getSetCookie().length, 2)
  } finally {
    child.kill()
    await new Promise(resolve => child.once('exit', resolve))
    await new Promise(resolve => backend.close(resolve))
  }
})
