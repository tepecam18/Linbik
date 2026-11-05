import { test } from 'node:test';
import assert from 'node:assert/strict';
import { generateKeys } from 'paseto-ts/v4';
import { createLinbikAuth, MemorySessionStore } from '../src/index.js';

function options() {
  return { publicOrigin: 'https://app.test', apiKey: 'private-secret',
    serviceId: 'aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa', clientId: 'bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb',
    sessionKey: generateKeys('local'), store: new MemorySessionStore(), fetch: () => { throw new Error('Must not send a request'); } };
}

for (const [field, value] of [
  ['publicOrigin', undefined], ['apiBaseUrl', 'not a URL with private-secret'],
  ['apiKey', 42], ['apiKey', '   '], ['serviceId', 'package-name'], ['clientId', 'invalid'],
  ['sessionKey', 42], ['authorizationOrigins', 'https://identity.test'], ['fetch', 42],
  ['accessTtlSeconds', 0], ['refreshTtlSeconds', Number.MAX_SAFE_INTEGER],
  ['refreshTtlSeconds', 1], ['allowInsecureHttp', 'false']
]) {
  test(`invalid ${field} fails during configuration with no credentials in its message (${String(value)})`, () => {
    assert.throws(() => createLinbikAuth({ ...options(), [field]: value }), error => {
      assert.ok(error instanceof TypeError);
      assert.ok(error.message.includes(field));
      assert.ok(!error.message.includes('private-secret'));
      return true;
    });
  });
}

for (const capacity of [0, -1, NaN, Infinity, 1.5]) {
  test(`memory store rejects invalid capacity (${capacity})`, () => {
    assert.throws(() => new MemorySessionStore({ capacity }), /capacity.*positive safe integer/);
  });
}

test('memory store rejects records with invalid expiry instead of keeping them forever', async () => {
  const store = new MemorySessionStore();
  for (const record of [null, {}, { expiresAt: Infinity }, { expiresAt: 'tomorrow' }])
    await assert.rejects(store.set('session', record), /expiresAt.*finite Unix timestamp/);
  assert.equal(await store.get('session'), null);
});

test('memory store capacity error gives a recovery action and retains existing sessions', async () => {
  const store = new MemorySessionStore({ capacity: 1 });
  await store.set('one', { expiresAt: Date.now() + 60000 });
  await assert.rejects(store.set('two', { expiresAt: Date.now() + 60000 }), /Increase MemorySessionStore capacity/);
  assert.ok(await store.get('one'));
});
