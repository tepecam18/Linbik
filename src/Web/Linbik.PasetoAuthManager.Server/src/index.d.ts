export interface LinbikUser { userId: string; username: string; displayName: string; integrations: string[] }
/** Values contain credentials: encrypt/protect persistent storage and never serialize into page payloads. */
export interface SessionStore {
  get(key: string): Promise<any | null>;
  set(key: string, record: { expiresAt: number; [key: string]: any }): Promise<void>;
  delete(key: string): Promise<void>;
  /** Must serialize operations for the key across every process using this store. */
  withLock<T>(key: string, action: () => Promise<T>): Promise<T>;
}
export class MemorySessionStore implements SessionStore {
  constructor(options?: { capacity?: number });
  get(key: string): Promise<any | null>;
  set(key: string, record: { expiresAt: number; [key: string]: any }): Promise<void>;
  delete(key: string): Promise<void>;
  withLock<T>(key: string, action: () => Promise<T>): Promise<T>;
}
export interface LinbikServerOptions {
  apiBaseUrl?: string;
  apiKey: string;
  serviceId: string;
  clientId: string;
  publicOrigin: string;
  /** PASERK k4.local key; generated with the package's keygen script. */
  sessionKey: string;
  store: SessionStore;
  authorizationOrigins?: string[];
  accessTtlSeconds?: number;
  refreshTtlSeconds?: number;
  /** Explicitly permit loopback HTTP for local development only. */
  allowInsecureHttp?: boolean;
  fetch?: typeof globalThis.fetch;
}
export interface LinbikAuthRequest {
  signIn(returnPath?: string): Promise<string>;
  callback(code: string): Promise<{ user: LinbikUser; returnPath: string }>;
  getSession(): Promise<LinbikUser | null>;
  restoreSession(): Promise<LinbikUser | null>;
  refreshToken(): Promise<LinbikUser>;
  signOut(): Promise<void>;
}
export class LinbikServerError extends Error {
  constructor(message: string, status?: number);
  status: number;
}
export function isLinbikServerError(error: unknown): error is LinbikServerError;
export function createLinbikAuth(options: LinbikServerOptions): {
  createRequest(options: { cookieHeader?: string; onSetCookie: (cookie: string) => void }): LinbikAuthRequest;
  assertSameOrigin(headers: HeadersInit): void;
  cookieNames: Readonly<{ access: string; refresh: string; flow: string }>;
};
