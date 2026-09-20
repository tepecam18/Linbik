export interface ServerTransportOptions {
  backendBaseUrl: string;
  cookieHeader?: string;
  /** Append each header separately to the outgoing HTTP response. */
  onSetCookie: (cookie: string) => void | Promise<void>;
  fetch?: typeof globalThis.fetch;
}
/** Request-scoped cookie forwarding to one trusted backend. Never create a global instance. */
export function createServerTransport(options: ServerTransportOptions): {
  fetch: typeof globalThis.fetch;
  hasCookie(name: string): boolean;
};
