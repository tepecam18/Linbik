import { createHash, randomBytes, timingSafeEqual } from 'node:crypto';
import { encrypt, decrypt } from 'paseto-ts/v4';
export { MemorySessionStore } from './store.js';

export class LinbikServerError extends Error {
  constructor(message, status = 400) { super(message); this.name = 'LinbikServerError'; this.status = status; }
}

/** Works across Nitro/SSR bundles that may contain distinct copies of this class. */
export function isLinbikServerError(error) {
  return error instanceof Error && error.name === 'LinbikServerError' && Number.isInteger(error.status) && error.status >= 400 && error.status <= 599;
}

const id = () => randomBytes(32).toString('base64url');
const hash = value => createHash('sha256').update(value).digest('base64url');
const validId = value => typeof value === 'string' && /^[A-Za-z0-9_-]{43}$/.test(value);
const uuid = value => typeof value === 'string' && /^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(value) && !/^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(value);
const same = (a, b) => validId(a) && validId(b) && timingSafeEqual(Buffer.from(a), Buffer.from(b));

function safeUrl(value, allowHttp) {
  const url = new URL(value);
  const local = ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname);
  if (url.username || url.password || url.search || url.hash || (url.protocol !== 'https:' && !(allowHttp && local && url.protocol === 'http:'))) {
    throw new TypeError('Use HTTPS URLs without credentials/query/fragment; development HTTP must be explicitly enabled on loopback.');
  }
  return url;
}

function safeReturnPath(path) {
  if (typeof path !== 'string' || !path.startsWith('/') || path.startsWith('//') || /[\\\x00-\x20]/.test(path)) throw new LinbikServerError('Invalid return path.');
  const url = new URL(path, 'https://local.invalid');
  if (url.origin !== 'https://local.invalid' || url.pathname.startsWith('/api/')) throw new LinbikServerError('Invalid return path.');
  return url.pathname + url.search + url.hash;
}

/** Configure once on the server. Request state lives only in createRequest(). */
export function createLinbikAuth(options) {
  options = { ...options };
  const api = safeUrl(options.apiBaseUrl ?? 'https://api.linbik.com', options.allowInsecureHttp);
  const origin = safeUrl(options.publicOrigin, options.allowInsecureHttp);
  if (origin.pathname !== '/') throw new TypeError('publicOrigin must be an origin without a path.');
  if (!options.apiKey || /[\r\n]/.test(options.apiKey) || !uuid(options.serviceId) || !uuid(options.clientId)) throw new TypeError('apiKey, serviceId and clientId are required.');
  if (!/^k4\.local\.[A-Za-z0-9_-]{43}$/.test(options.sessionKey ?? '')) throw new TypeError('sessionKey must be a generated k4.local PASERK key.');
  const store = options.store;
  for (const method of ['get', 'set', 'delete', 'withLock']) if (typeof store?.[method] !== 'function') throw new TypeError(`Session store requires ${method}.`);
  const secure = origin.protocol === 'https:';
  const prefix = secure ? '__Host-linbik_' : 'linbik_';
  const names = { access: prefix + 'access', refresh: prefix + 'refresh', flow: prefix + 'flow' };
  const issuer = 'Linbik.Node';
  const audience = origin.origin;
  const accessTtl = options.accessTtlSeconds ?? 900;
  const refreshTtl = options.refreshTtlSeconds ?? 1209600;
  if (!Number.isInteger(accessTtl) || accessTtl < 1 || !Number.isInteger(refreshTtl) || refreshTtl < accessTtl) throw new TypeError('Invalid session lifetimes.');
  const allowedOrigins = new Set([api.origin, ...(options.authorizationOrigins ?? []).map(value => safeUrl(value, options.allowInsecureHttp).origin)]);
  const transport = options.fetch ?? globalThis.fetch;

  async function post(path, body, extraHeaders = {}) {
    let response;
    try {
      response = await transport(new URL(api.href.replace(/\/$/, '') + path), {
        method: 'POST', headers: { 'Content-Type': 'application/json', Accept: 'application/json', ApiKey: options.apiKey, ...extraHeaders },
        body: JSON.stringify(body), redirect: 'error', cache: 'no-store', signal: AbortSignal.timeout(10000)
      });
    } catch { throw new LinbikServerError('Linbik service is unavailable.', 502); }
    if (!response.ok) throw new LinbikServerError('Linbik request failed.', response.status === 401 || response.status === 400 ? 401 : 502);
    let result;
    try { result = await response.json(); } catch { throw new LinbikServerError('Invalid Linbik response.', 502); }
    if (result?.isSuccess !== true || !result.data) throw new LinbikServerError('Linbik request was not successful.', 502);
    return result.data;
  }

  function sessionRecord(data, previous) {
    if (!uuid(data.userId) || typeof data.username !== 'string' || !data.username || (data.clientId != null && (typeof data.clientId !== 'string' || data.clientId.toLowerCase() !== options.clientId.toLowerCase()))) throw new LinbikServerError('Invalid Linbik identity.', 502);
    if (previous && data.userId !== previous.user.userId) throw new LinbikServerError('Session identity changed.', 401);
    const upstreamRefresh = data.refreshToken || previous?.upstreamRefresh;
    if (typeof upstreamRefresh !== 'string' || !upstreamRefresh || /[\r\n]/.test(upstreamRefresh)) throw new LinbikServerError('Invalid refresh token.', 502);
    const now = Date.now();
    const expiry = (value, fallback) => {
      if (value == null) return fallback;
      if (!Number.isFinite(value) || value * 1000 <= now) throw new LinbikServerError('Expired Linbik token.', 401);
      return value * 1000;
    };
    const expiresAt = Math.min(expiry(data.refreshTokenExpiresAt, previous?.expiresAt ?? now + refreshTtl * 1000), now + refreshTtl * 1000);
    const accessExpiresAt = Math.min(expiry(data.accessTokenExpiresAt, now + accessTtl * 1000), now + accessTtl * 1000, expiresAt);
    const integrations = data.integrations ?? [];
    if (!Array.isArray(integrations) || integrations.some(value => !value || typeof value.packageName !== 'string' || typeof value.token !== 'string')) throw new LinbikServerError('Invalid integration response.', 502);
    return { expiresAt, accessExpiresAt, refreshedAt: now, upstreamRefresh, integrations,
      user: { userId: data.userId, username: data.username, displayName: typeof data.displayName === 'string' ? data.displayName : data.username, integrations: integrations.map(value => value.packageName) } };
  }

  function assertSameOrigin(headers) {
    const values = new Headers(headers);
    if (values.get('origin') !== origin.origin || values.get('x-linbik-request') !== '1') throw new LinbikServerError('Invalid request origin.', 403);
  }

  function createRequest({ cookieHeader = '', onSetCookie }) {
    if (typeof onSetCookie !== 'function') throw new TypeError('onSetCookie is required.');
    const cookies = new Map(cookieHeader.split(';').map(value => { const i = value.indexOf('='); return [value.slice(0, i).trim(), value.slice(i + 1).trim()]; }));
    function writeCookie(name, value, expiry = 0) {
      if (value) cookies.set(name, value); else cookies.delete(name);
      onSetCookie(`${name}=${value}; Path=/; HttpOnly; SameSite=Lax${secure ? '; Secure' : ''}; Max-Age=${Math.max(0, Math.floor((expiry - Date.now()) / 1000))}`);
    }
    function clear() { writeCookie(names.access, ''); writeCookie(names.refresh, ''); }
    const sessionId = () => validId(cookies.get(names.refresh)) ? hash(cookies.get(names.refresh)) : null;
    async function issue(record, sid) {
      const token = await encrypt(options.sessionKey, { iss: issuer, aud: audience, sub: record.user.userId, sid, purpose: 'access', exp: new Date(record.accessExpiresAt).toISOString() });
      writeCookie(names.access, token, record.accessExpiresAt);
      writeCookie(names.refresh, cookies.get(names.refresh), record.expiresAt);
    }
    async function readAccess() {
      const token = cookies.get(names.access);
      if (!token) return null;
      let payload;
      try { ({ payload } = await decrypt(options.sessionKey, token)); } catch { return null; }
      if (payload.iss !== issuer || payload.aud !== audience || payload.purpose !== 'access' || !validId(payload.sid) || Date.parse(payload.exp) <= Date.now() || !payload.exp) return null;
      return payload;
    }
    async function getSession() {
      const payload = await readAccess();
      if (!payload) return null;
      const record = await store.get('session:' + payload.sid);
      if (!record || record.accessExpiresAt <= Date.now() || record.user.userId !== payload.sub) return null;
      return record.user;
    }
    async function refreshToken() {
      const sid = sessionId();
      if (!sid) { clear(); throw new LinbikServerError('No session.', 401); }
      return store.withLock('session:' + sid, async () => {
        let record = await store.get('session:' + sid);
        if (!record) { clear(); throw new LinbikServerError('Session expired.', 401); }
        // Concurrent requests reuse the recently rotated upstream token result.
        if (record.accessExpiresAt <= Date.now() || Date.now() - record.refreshedAt > 5000) {
          try {
            const data = await post('/api/oauth/refresh', { serviceId: options.serviceId }, { RefreshToken: record.upstreamRefresh });
            record = sessionRecord(data, record);
            await store.set('session:' + sid, record);
          } catch (error) {
            if (error instanceof LinbikServerError && error.status === 401) { await store.delete('session:' + sid); clear(); }
            throw error;
          }
        }
        await issue(record, sid);
        return record.user;
      });
    }
    async function signOut() {
      const access = await readAccess();
      for (const sid of new Set([sessionId(), access?.sid].filter(Boolean))) {
        await store.withLock('session:' + sid, () => store.delete('session:' + sid));
      }
      const flow = cookies.get(names.flow);
      if (validId(flow)) await store.delete('flow:' + hash(flow));
      writeCookie(names.flow, '');
      clear();
    }
    async function signIn(returnPath = '/') {
      returnPath = safeReturnPath(returnPath);
      const verifier = id();
      const data = await post('/api/oauth/initiate', { clientId: options.clientId, codeChallenge: hash(verifier), extraData: { returnPath } });
      let redirect;
      try { redirect = new URL(data.redirectUrl); } catch { throw new LinbikServerError('Invalid authorization URL.', 502); }
      if (!allowedOrigins.has(redirect.origin) || redirect.username || redirect.password) throw new LinbikServerError('Untrusted authorization origin.', 502);
      const flow = id();
      const expiresAt = Date.now() + 300000;
      const old = cookies.get(names.flow);
      if (validId(old)) await store.delete('flow:' + hash(old));
      await store.set('flow:' + hash(flow), { verifier, returnPath, expiresAt });
      writeCookie(names.flow, flow, expiresAt);
      return redirect.href;
    }
    async function callback(code) {
      const flow = cookies.get(names.flow);
      writeCookie(names.flow, '');
      if (!validId(flow) || typeof code !== 'string' || !code || code.length > 4096 || /[\r\n]/.test(code)) throw new LinbikServerError('Login transaction is missing or invalid.');
      const transaction = await store.withLock('flow:' + hash(flow), async () => {
        const transaction = await store.get('flow:' + hash(flow));
        await store.delete('flow:' + hash(flow));
        return transaction;
      });
      if (!transaction) throw new LinbikServerError('Login transaction expired or was already used.');
      const data = await post('/api/oauth/token', { serviceId: options.serviceId }, { Code: code });
      if (!same(hash(transaction.verifier), data.codeChallenge)) throw new LinbikServerError('PKCE verification failed.');
      const record = sessionRecord(data);
      await signOut();
      const handle = id();
      const sid = hash(handle);
      await store.set('session:' + sid, record);
      cookies.set(names.refresh, handle);
      await issue(record, sid);
      return { user: record.user, returnPath: transaction.returnPath };
    }
    async function restoreSession() {
      const user = await getSession();
      if (user || !sessionId()) return user;
      try { return await refreshToken(); } catch (error) { if (error instanceof LinbikServerError && error.status === 401) return null; throw error; }
    }
    return { getSession, restoreSession, refreshToken, signIn, callback, signOut };
  }
  return { createRequest, assertSameOrigin, cookieNames: Object.freeze(names) };
}
