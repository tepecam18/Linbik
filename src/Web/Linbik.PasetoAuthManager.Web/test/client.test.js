import { test } from 'node:test';
import assert from 'node:assert/strict';
import { LinbikPasetoAuthClient, LinbikAuthError } from '../src/index.js';

const user = { userId: '123', username: 'ada', displayName: 'Ada', integrations: ['survey'] };
const ok = (data = user) => Response.json({ isSuccess: true, data });
const client = (options = {}) => new LinbikPasetoAuthClient({ backendBaseUrl: 'https://api.example.test/service', ...options });

test('SSR-safe import, encoded login parameters, backend prefix and navigation adapter', () => {
  let location;
  const auth = client({ clientName: 'Web & App', navigate: url => { location = url; } });
  auth.signIn('/dashboard?tab=a&b=2');
  const url = new URL(location);
  assert.equal(url.pathname, '/service/api/Linbik/login');
  assert.equal(url.searchParams.get('name'), 'Web & App');
  assert.equal(url.searchParams.get('returnPath'), '/dashboard?tab=a&b=2');
  assert.equal(client().getSignInUrl().includes('name='), false);
  assert.throws(() => client().signIn(), /browser/);
});

test('reject unsafe URLs and paths before sending credentials', async () => {
  for (const backendBaseUrl of ['file:///tmp', 'https://a:b@example.test', 'https://example.test?x=1']) {
    assert.throws(() => client({ backendBaseUrl }), TypeError);
  }
  const auth = client({ fetch: () => { throw new Error('Must not fetch'); } });
  for (const path of ['https://evil.test', '//evil.test', '/\\evil.test', '/../outside', '/%2e%2e/outside', '/\nevil']) {
    await assert.rejects(auth.fetch(path), TypeError);
  }
  for (const path of ['https://evil.test', '//evil.test', '/\\evil.test']) {
    assert.throws(() => auth.getSignInUrl(path), TypeError);
  }
});

test('refresh uses POST and cookies, returns user, deduplicates and permits subsequent refresh', async () => {
  const calls = [];
  const auth = client({ refreshPath: '/auth/renew', fetch: async (...args) => { calls.push(args); return ok(); } });
  const first = auth.refreshToken();
  assert.equal(auth.refreshToken(), first);
  assert.deepEqual(await first, user);
  assert.equal(calls.length, 1);
  assert.equal(calls[0][0], 'https://api.example.test/service/auth/renew');
  assert.equal(calls[0][1].method, 'POST');
  assert.equal(calls[0][1].credentials, 'include');
  await auth.refreshToken();
  assert.equal(calls.length, 2);
});

test('logout waits for refresh and blocks a new refresh until cookies are cleared', async () => {
  let finish;
  const calls = [];
  const auth = client({ fetch: async (url, init) => {
    calls.push(init.method);
    if (init.method === 'POST') return new Promise(resolve => { finish = resolve; });
    return ok(null);
  } });
  const refresh = auth.refreshToken();
  const logout = auth.signOut();
  assert.equal(auth.signOut(), logout);
  await assert.rejects(auth.refreshToken(), /Sign-out/);
  assert.deepEqual(calls, ['POST']);
  finish(ok());
  await Promise.all([refresh, logout]);
  assert.deepEqual(calls, ['POST', 'GET']);
});

test('HTTP errors, malformed data and unsuccessful envelopes are actionable', async () => {
  for (const [response, status, message] of [
    [new Response(null, { status: 401 }), 401, /401/],
    [Response.json({ isSuccess: false, friendlyMessage: { message: 'Session expired' } }), 200, /Session expired/],
    [new Response('<html>'), 200, /Invalid JSON/],
    [ok({}), 200, /Invalid user/],
    [Response.json({ data: user }), 200, /Authentication request failed/]
  ]) {
    await assert.rejects(client({ fetch: async () => response }).refreshToken(), error => {
      assert.ok(error instanceof LinbikAuthError);
      assert.equal(error.status, status);
      assert.match(error.message, message);
      return true;
    });
  }
});

test('failed refresh can be retried; logout still works after refresh fails', async () => {
  let count = 0;
  const auth = client({ fetch: async () => ++count === 1 ? new Response(null, { status: 401 }) : ok() });
  const refresh = auth.refreshToken();
  const logout = auth.signOut();
  await assert.rejects(refresh);
  await logout;
  assert.deepEqual(await auth.refreshToken(), user);
});

test('API calls preserve options, force cookie safety and never replay mutations', async () => {
  let count = 0;
  const auth = client({ fetch: async (url, init) => {
    count++;
    assert.equal(init.method, 'POST');
    assert.equal(init.body, 'payload');
    assert.equal(init.credentials, 'include');
    assert.equal(init.redirect, 'error');
    assert.equal(init.cache, 'no-store');
    return new Response(null, { status: 401 });
  } });
  assert.equal((await auth.fetch('/orders', { method: 'POST', body: 'payload', credentials: 'omit', redirect: 'follow' })).status, 401);
  assert.equal(count, 1);
});
