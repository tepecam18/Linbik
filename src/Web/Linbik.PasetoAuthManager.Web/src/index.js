/** An HTTP, backend-envelope, or response-format error. Network errors pass through. */
export class LinbikAuthError extends Error {
  constructor(message, status, response) {
    super(message);
    this.name = 'LinbikAuthError';
    this.status = status;
    this.response = response;
  }
}

/** Framework-independent browser client. Tokens remain in backend HttpOnly cookies. */
export class LinbikPasetoAuthClient {
  #base;
  #options;
  #refresh;
  #logout;

  constructor(options) {
    this.#options = { ...options };
    this.#base = new URL(options.backendBaseUrl);
    if (!['https:', 'http:'].includes(this.#base.protocol) || this.#base.username || this.#base.password || this.#base.search || this.#base.hash) {
      throw new TypeError('backendBaseUrl must be an HTTP(S) URL without credentials, query or fragment.');
    }
    this.#base.pathname = this.#base.pathname.replace(/\/+$/, '') + '/';
  }

  #url(path) {
    if (typeof path !== 'string' || !path.startsWith('/') || path.startsWith('//') || /[\\\r\n\t]/.test(path)) {
      throw new TypeError('Use a backend-relative path starting with a single slash.');
    }
    const url = new URL(path.slice(1), this.#base);
    if (url.origin !== this.#base.origin || !url.pathname.startsWith(this.#base.pathname)) {
      throw new TypeError('The path must stay within backendBaseUrl.');
    }
    return url;
  }

  getSignInUrl(returnPath = '/') {
    if (typeof returnPath !== 'string' || !returnPath.startsWith('/') || returnPath.startsWith('//') || /[\\\r\n\t]/.test(returnPath)) {
      throw new TypeError('returnPath must be an application-relative path.');
    }
    const url = this.#url(this.#options.loginPath ?? '/api/Linbik/login');
    if (this.#options.clientName) url.searchParams.set('name', this.#options.clientName);
    url.searchParams.set('returnPath', returnPath);
    return url.href;
  }

  signIn(returnPath = '/') {
    const url = this.getSignInUrl(returnPath);
    if (this.#options.navigate) return this.#options.navigate(url);
    if (typeof window === 'undefined') throw new Error('signIn requires a browser or a navigate adapter.');
    window.location.assign(url);
  }

  /** Native Response; no automatic retry of potentially non-idempotent API requests. */
  async fetch(path, init = {}) {
    const url = this.#url(path);
    const transport = this.#options.fetch ?? globalThis.fetch;
    return transport(url.href, { ...init, credentials: 'include', cache: 'no-store', redirect: 'error' });
  }

  async #session(path, method) {
    const response = await this.fetch(path, { method, headers: { Accept: 'application/json' } });
    let body;
    try { body = await response.json(); } catch {
      throw new LinbikAuthError(response.ok ? 'Invalid JSON response.' : `Authentication request failed (${response.status}).`, response.status);
    }
    if (!response.ok || body?.isSuccess !== true) {
      const message = body?.friendlyMessage?.message;
      throw new LinbikAuthError(typeof message === 'string' ? message : `Authentication request failed (${response.status}).`, response.status, body);
    }
    return body.data;
  }

  /** Deduplicate concurrent refreshes because refresh tokens can rotate. */
  refreshToken() {
    if (this.#logout) return Promise.reject(new Error('Sign-out is in progress.'));
    if (!this.#refresh) {
      this.#refresh = this.#session(this.#options.refreshPath ?? '/api/Linbik/refresh', 'POST')
        .then(user => {
          if (!user || typeof user.userId !== 'string' || typeof user.username !== 'string' || !Array.isArray(user.integrations) || !user.integrations.every(value => typeof value === 'string')) {
            throw new LinbikAuthError('Invalid user response.', 200);
          }
          return { userId: user.userId, username: user.username, displayName: typeof user.displayName === 'string' ? user.displayName : user.username, integrations: user.integrations };
        })
        .finally(() => { this.#refresh = undefined; });
    }
    return this.#refresh;
  }

  signOut() {
    if (!this.#logout) {
      // Ensure an in-flight refresh cannot restore cookies after logout.
      this.#logout = (async () => {
        if (this.#refresh) await this.#refresh.catch(() => {});
        await this.#session(this.#options.logoutPath ?? '/api/Linbik/logout', 'GET');
      })().finally(() => { this.#logout = undefined; });
    }
    return this.#logout;
  }
}
