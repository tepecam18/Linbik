import { test } from 'node:test';
import assert from 'node:assert/strict';
import { generateKeys, encrypt, decrypt } from 'paseto-ts/v4';
import { createLinbikAuth, MemorySessionStore } from '../src/index.js';

const userId = 'aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa';
const clientId = 'bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb';
const serviceId = 'cccccccc-cccc-4ccc-cccc-cccccccccccc';

function fixture(overrides = {}) {
  const key = generateKeys('local');
  const store = new MemorySessionStore();
  let challenge;
  const calls = [];
  const data = { userId, clientId, username: 'alice', displayName: 'Alice', refreshToken: 'upstream-secret', integrations: [{ packageName: 'billing', token: 'private-integration' }] };
  const fetch = async (url, init) => {
    calls.push([url.pathname, init]);
    assert.equal(init.headers.ApiKey, 'private-api-key');
    assert.equal(init.redirect, 'error');
    if (url.pathname === '/api/oauth/initiate') {
      const body = JSON.parse(init.body);
      assert.equal(body.clientId, clientId);
      challenge = body.codeChallenge;
      return Response.json({ isSuccess: true, data: { redirectUrl: 'https://identity.test/approve' } });
    }
    assert.equal(JSON.parse(init.body).serviceId, serviceId);
    if (url.pathname === '/api/oauth/token') assert.equal(init.headers.Code, 'one-time-code');
    if (url.pathname === '/api/oauth/refresh') assert.equal(init.headers.RefreshToken, 'upstream-secret');
    return Response.json({ isSuccess: true, data: { ...data, codeChallenge: challenge, ...overrides.token } });
  };
  const auth = createLinbikAuth({ apiBaseUrl: 'https://identity.test', publicOrigin: 'https://app.test', apiKey: 'private-api-key', clientId, serviceId, sessionKey: key, store, fetch: overrides.fetch ?? fetch });
  const jar = new Map();
  const written = [];
  const request = () => auth.createRequest({ cookieHeader: [...jar].map(([k, v]) => `${k}=${v}`).join('; '), onSetCookie: cookie => {
    written.push(cookie);
    const pair = cookie.split(';')[0];
    const i = pair.indexOf('=');
    if (pair.slice(i + 1)) jar.set(pair.slice(0, i), pair.slice(i + 1)); else jar.delete(pair.slice(0, i));
  } });
  const login = async () => { await request().signIn('/protected?tab=1'); return request().callback('one-time-code'); };
  return { key, store, auth, jar, written, calls, request, login };
}

test('login binds PKCE and issues validated local PASETO; private tokens never reach the browser', async () => {
  const f = fixture();
  const result = await f.login();
  assert.equal(result.returnPath, '/protected?tab=1');
  assert.equal((await f.request().getSession()).username, 'alice');
  assert.match(f.jar.get(f.auth.cookieNames.access), /^v4\.local\./);
  assert.ok(f.written.every(cookie => cookie.includes('HttpOnly') && cookie.includes('Secure') && !cookie.includes('Domain=')));
  assert.doesNotMatch(JSON.stringify([result, f.written]), /upstream-secret|private-api-key|private-integration/);
  assert.equal(f.calls.length, 2, 'Session inspection must not call the identity provider');
});

test('callback without verifier, wrong PKCE, wrong client and replay fail closed', async () => {
  const missing = fixture();
  await assert.rejects(missing.request().callback('one-time-code'), /transaction/);
  assert.equal(missing.calls.length, 0);
  for (const token of [{ codeChallenge: 'wrong' }, { codeChallenge: null }, { clientId: serviceId }]) {
    const f = fixture({ token });
    await assert.rejects(f.login());
    assert.equal(await f.request().getSession(), null);
  }
  const f = fixture();
  await f.request().signIn('/');
  const replay = f.request();
  await f.request().callback('one-time-code');
  await assert.rejects(replay.callback('one-time-code'), /already used/);
});

test('forged, expired, wrong-audience and wrong-purpose tokens are rejected', async () => {
  const f = fixture();
  await f.login();
  const original = f.jar.get(f.auth.cookieNames.access);
  const { payload } = decrypt(f.key, original);
  f.jar.set(f.auth.cookieNames.access, original.slice(0, -4) + 'aaaa');
  assert.equal(await f.request().getSession(), null);
  for (const claims of [
    { exp: new Date(0).toISOString() }, { aud: 'https://other.test' }, { purpose: 'refresh' }
  ]) {
    const token = encrypt(f.key, { ...payload, ...claims }, { validatePayload: false, addExp: false, addIat: false });
    f.jar.set(f.auth.cookieNames.access, token);
    assert.equal(await f.request().getSession(), null);
  }
});

test('concurrent refreshes rotate once and logout revokes copied cookies', async () => {
  const f = fixture();
  await f.login();
  // Age the stored refresh result without waiting on wall-clock time.
  const { createHash } = await import('node:crypto');
  const sid = createHash('sha256').update(f.jar.get(f.auth.cookieNames.refresh)).digest('base64url');
  const record = await f.store.get('session:' + sid);
  record.refreshedAt = Date.now() - 6000;
  record.accessExpiresAt = Date.now() - 1;
  await f.store.set('session:' + sid, record);
  const requests = [f.request(), f.request(), f.request()];
  const users = await Promise.all(requests.map(request => request.refreshToken()));
  assert.ok(users.every(user => user.userId === userId));
  assert.equal(f.calls.filter(([path]) => path.endsWith('/refresh')).length, 1);
  const copied = f.request();
  await f.request().signOut();
  assert.equal(await copied.getSession(), null);
  await assert.rejects(copied.refreshToken(), error => error.status === 401);
});

test('return paths, authorization destinations and cross-origin mutations are constrained', async () => {
  const f = fixture();
  for (const path of ['//evil.test', '/\\evil.test', 'https://evil.test', '/api/Linbik/logout']) await assert.rejects(f.request().signIn(path));
  assert.throws(() => f.auth.assertSameOrigin({ origin: 'https://evil.test', 'x-linbik-request': '1' }), error => error.status === 403);
  assert.throws(() => f.auth.assertSameOrigin({ origin: 'https://app.test' }), error => error.status === 403);
  f.auth.assertSameOrigin({ origin: 'https://app.test', 'x-linbik-request': '1' });
  const evil = fixture({ fetch: async () => Response.json({ isSuccess: true, data: { redirectUrl: 'https://evil.test/' } }) });
  await assert.rejects(evil.request().signIn(), /Untrusted/);
});

test('backend failures remain errors; missing sessions stay anonymous', async () => {
  const f = fixture({ fetch: async () => { throw new Error('network'); } });
  assert.equal(await f.request().restoreSession(), null);
  await assert.rejects(f.request().signIn(), error => error.status === 502);
});

test('logout also revokes an access-only session and cancels a pending login', async () => {
  const f = fixture();
  await f.login();
  const copied = f.request();
  f.jar.delete(f.auth.cookieNames.refresh);
  await f.request().signOut();
  assert.equal(await copied.getSession(), null);
  await f.request().signIn('/');
  const callback = f.request();
  await f.request().signOut();
  await assert.rejects(callback.callback('one-time-code'), /already used/);
});
