import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServerTransport } from '../src/server.js';
import { LinbikPasetoAuthClient } from '../src/index.js';

test('server requests isolate users and only forward Linbik cookies to the trusted backend', async () => {
  const calls = [];
  const make = user => createServerTransport({
    backendBaseUrl: 'https://backend.test/service', cookieHeader: `authToken=${user}; unrelated=secret`,
    onSetCookie: () => {}, fetch: async (_, init) => { calls.push(init.headers.get('cookie')); return new Response(); }
  });
  const a = make('alice'), b = make('bob');
  await Promise.all([a.fetch('https://backend.test/service/session'), b.fetch('https://backend.test/service/session')]);
  assert.deepEqual(calls.sort(), ['authToken=alice', 'authToken=bob']);
  await assert.rejects(a.fetch('https://attacker.test/service/session'), TypeError);
  await assert.rejects(a.fetch('https://backend.test/outside'), TypeError);
});

test('multiple Set-Cookie headers reach the outer response and the current request jar', async () => {
  const received = [];
  let count = 0;
  const adapter = createServerTransport({ backendBaseUrl: 'https://backend.test',
    cookieHeader: 'authToken=expired; linbikRefreshToken=old; username=old', onSetCookie: cookie => received.push(cookie),
    fetch: async (_, init) => {
      if (count++ === 0) {
        const headers = new Headers();
        headers.append('set-cookie', 'authToken=new; Domain=backend.test; Path=/; HttpOnly; Secure; SameSite=Lax');
        headers.append('set-cookie', 'linbikRefreshToken=rotated; Expires=Wed, 01 Jan 2042 00:00:00 GMT; HttpOnly; Secure');
        headers.append('set-cookie', 'username=; Max-Age=0; Path=/');
        headers.append('set-cookie', 'unrelated=ignore; Path=/');
        return new Response('', { headers });
      }
      assert.equal(init.headers.get('cookie'), 'authToken=new; linbikRefreshToken=rotated');
      assert.equal(init.redirect, 'error');
      return new Response();
    } });
  await adapter.fetch('https://backend.test/refresh');
  await adapter.fetch('https://backend.test/session');
  assert.equal(received.length, 3);
  assert.ok(received.every(cookie => !cookie.includes('Domain=') && cookie.includes('Path=/')));
  assert.match(received[0], /HttpOnly; Secure; SameSite=Lax/);
  assert.match(received[1], /Wed, 01 Jan 2042/);
});

test('session reads do not rotate tokens; 401 is anonymous while other failures propagate', async () => {
  const user = { userId: 'id', username: 'alice', integrations: [] };
  let count = 0;
  const client = new LinbikPasetoAuthClient({ backendBaseUrl: 'https://backend.test', fetch: async (url, init) => {
    assert.equal(url, 'https://backend.test/api/Linbik/session');
    assert.equal(init.method, 'GET');
    count++;
    if (count === 1) return Response.json({ isSuccess: true, data: user });
    return new Response(null, { status: count === 2 ? 401 : 503 });
  } });
  assert.equal((await client.getSession()).username, 'alice');
  assert.equal(await client.getSession(), null);
  await assert.rejects(client.getSession(), error => error.status === 503);
});
