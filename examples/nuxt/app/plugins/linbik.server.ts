import { LinbikAuthError, LinbikPasetoAuthClient } from '@linbik/paseto-auth'
import { createServerTransport } from '@linbik/paseto-auth/server'
import { appendResponseHeader, getRequestHeader, setResponseHeader } from 'h3'

export default defineNuxtPlugin(() => {
  const event = useRequestEvent()!
  const config = useRuntimeConfig()
  setResponseHeader(event, 'Cache-Control', 'private, no-store')
  const transport = createServerTransport({
    backendBaseUrl: config.linbikBackendBaseUrl,
    cookieHeader: getRequestHeader(event, 'cookie'),
    onSetCookie: cookie => { appendResponseHeader(event, 'set-cookie', cookie) }
  })
  const backend = new LinbikPasetoAuthClient({
    backendBaseUrl: config.linbikBackendBaseUrl,
    fetch: transport.fetch
  })
  // An anonymous page should not contact the backend or rotate a refresh token.
  const getSession = backend.getSession.bind(backend)
  backend.getSession = () => transport.hasCookie('authToken') ? getSession() : Promise.resolve(null)
  const refresh = backend.refreshToken.bind(backend)
  backend.refreshToken = () => transport.hasCookie('linbikRefreshToken')
    ? refresh()
    : Promise.reject(new LinbikAuthError('No session.', 401))
  return { provide: { linbik: backend } }
})
