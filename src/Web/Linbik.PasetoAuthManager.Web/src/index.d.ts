export interface LinbikPasetoAuthOptions {
  /** Absolute HTTP(S) URL; an optional deployment path prefix is preserved. */
  backendBaseUrl: string;
  /** Omit to use the backend's default client in keyless mode. */
  clientName?: string;
  loginPath?: string;
  refreshPath?: string;
  logoutPath?: string;
  fetch?: typeof globalThis.fetch;
  navigate?: (url: string) => void;
}

export interface LinbikUser {
  userId: string;
  username: string;
  displayName: string;
  integrations: string[];
}

export class LinbikAuthError extends Error {
  constructor(message: string, status: number, response?: unknown);
  readonly status: number;
  readonly response?: unknown;
}

export class LinbikPasetoAuthClient {
  constructor(options: LinbikPasetoAuthOptions);
  getSignInUrl(returnPath?: string): string;
  signIn(returnPath?: string): void;
  refreshToken(): Promise<LinbikUser>;
  signOut(): Promise<void>;
  /** Cookie-bearing backend request. Check response.ok; HTTP errors do not throw. */
  fetch(path: string, init?: RequestInit): Promise<Response>;
}
