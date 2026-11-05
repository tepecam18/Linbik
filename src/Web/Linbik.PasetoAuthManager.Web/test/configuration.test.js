import { test } from 'node:test';
import assert from 'node:assert/strict';
import { LinbikPasetoAuthClient } from '../src/index.js';
import { createServerTransport } from '../src/server.js';

for (const backendBaseUrl of [undefined, 42, 'invalid-url-with-private-secret']) {
  test(`browser and SSR identify invalid backendBaseUrl (${backendBaseUrl})`, () => {
    for (const factory of [options => new LinbikPasetoAuthClient(options), createServerTransport]) {
      assert.throws(() => factory({ backendBaseUrl, onSetCookie: () => {} }), error => {
        assert.ok(error instanceof TypeError);
        assert.ok(error.message.includes('backendBaseUrl'));
        assert.ok(!error.message.includes('private-secret'));
        return true;
      });
    }
  });
}

test('SSR requires a cookie forwarding callback before a refresh can rotate cookies', () => {
  let called = false;
  assert.throws(() => createServerTransport({ backendBaseUrl: 'https://backend.test', fetch: () => { called = true; } }), /onSetCookie.*function/);
  assert.equal(called, false);
});

test('invalid fetch and navigation adapters fail at construction', () => {
  assert.throws(() => new LinbikPasetoAuthClient({ backendBaseUrl: 'https://backend.test', fetch: 42 }), /fetch.*function/);
  assert.throws(() => new LinbikPasetoAuthClient({ backendBaseUrl: 'https://backend.test', navigate: true }), /navigate.*function/);
  assert.throws(() => createServerTransport({ backendBaseUrl: 'https://backend.test', onSetCookie: () => {}, fetch: 42 }), /fetch.*function/);
  assert.throws(() => createServerTransport({ backendBaseUrl: 'https://backend.test', onSetCookie: () => {}, cookieHeader: 42 }), /cookieHeader.*string/);
});
