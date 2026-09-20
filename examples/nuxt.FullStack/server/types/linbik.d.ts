import type { createLinbikAuth, LinbikAuthRequest } from '@linbik/paseto-auth-server'
declare module 'h3' {
  interface H3EventContext {
    linbik: LinbikAuthRequest;
    linbikAuth: ReturnType<typeof createLinbikAuth>;
  }
}
export {}
