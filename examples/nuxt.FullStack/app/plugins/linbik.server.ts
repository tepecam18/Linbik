import { LinbikPasetoAuthClient } from '@linbik/paseto-auth'
import { linbikResult } from '../../server/utils/linbik-result'

export default defineNuxtPlugin(() => {
  const event = useRequestEvent()!
  const config = useRuntimeConfig().public
  const linbik = new LinbikPasetoAuthClient({
    backendBaseUrl: config.linbikWebOrigin,
    logoutMethod: 'POST',
    fetch: async (input) => {
      const url = new URL(String(input))
      const action = url.pathname.split('/').pop()!
      if (url.origin !== new URL(config.linbikWebOrigin).origin || !['session', 'refresh', 'logout'].includes(action)) throw new Error('Unsupported SSR auth operation.')
      // Reuse this HTTP request's cookie context; no loopback HTTP or shared user state.
      return linbikResult(event.context.linbik, action)
    }
  })
  return { provide: { linbik } }
})
