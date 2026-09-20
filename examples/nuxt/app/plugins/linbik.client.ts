import { LinbikPasetoAuthClient } from '@linbik/paseto-auth'

export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig().public
  return {
    provide: {
      linbik: new LinbikPasetoAuthClient({
        backendBaseUrl: config.linbikWebOrigin,
        clientName: config.linbikClientName || undefined,
        sessionPath: '/api/auth/session',
        refreshPath: '/api/auth/refresh',
        logoutPath: '/api/auth/logout',
        logoutMethod: 'POST',
        fetch: (input, init) => {
          const headers = new Headers(init?.headers)
          headers.set('X-Linbik-Request', '1')
          return globalThis.fetch(input, { ...init, headers })
        }
      })
    }
  }
})
