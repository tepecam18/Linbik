// Create once per incoming request. Never share this cookie jar between users.
export function createServerTransport({ backendBaseUrl, cookieHeader = '', onSetCookie, fetch: transport = globalThis.fetch }) {
  const base = new URL(backendBaseUrl);
  if (!['http:', 'https:'].includes(base.protocol) || base.username || base.password || base.search || base.hash) {
    throw new TypeError('Invalid backendBaseUrl.');
  }
  const prefix = base.pathname.replace(/\/+$/, '') + '/';
  const cookies = new Map();
  for (const entry of cookieHeader.split(';')) {
    const index = entry.indexOf('=');
    if (index > 0) {
      const name = entry.slice(0, index).trim();
      if (isLinbikCookie(name)) cookies.set(name, entry.slice(index + 1).trim());
    }
  }
  return {
    hasCookie: name => cookies.has(name),
    fetch: async (input, init = {}) => {
      const url = new URL(typeof input === 'string' ? input : input instanceof URL ? input.href : input.url);
      if (url.origin !== base.origin || !url.pathname.startsWith(prefix) || url.username || url.password) {
        throw new TypeError('Server transport only accepts the configured backend.');
      }
      const headers = new Headers(init.headers);
      headers.delete('cookie');
      headers.delete('authorization');
      if (cookies.size) headers.set('cookie', [...cookies].map(([name, value]) => `${name}=${value}`).join('; '));
      const response = await transport(url.href, { ...init, headers, redirect: 'error', cache: 'no-store' });
      for (const cookie of response.headers.getSetCookie()) {
        const parts = cookie.split(';');
        const index = parts[0].indexOf('=');
        const name = parts[0].slice(0, index).trim();
        if (index < 1 || !isLinbikCookie(name)) continue;
        const expired = parts.some(part => {
          const [key, ...value] = part.trim().split('=');
          return key.toLowerCase() === 'max-age' && Number(value.join('=')) <= 0
            || key.toLowerCase() === 'expires' && Date.parse(value.join('=')) <= Date.now();
        });
        if (expired) cookies.delete(name);
        else cookies.set(name, parts[0].slice(index + 1));
        // The public web host owns the session. Preserve Secure/HttpOnly/SameSite/expiry.
        const publicCookie = parts.filter((part, i) => i === 0 || !/^\s*(domain|path)\s*=/i.test(part)).join(';') + '; Path=/';
        await onSetCookie(publicCookie);
      }
      return response;
    }
  };
}

function isLinbikCookie(name) {
  return ['authToken', 'linbikRefreshToken', 'username', 'pkce_verifier'].includes(name) || name.startsWith('integration_');
}
