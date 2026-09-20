import { LinbikAuthError, LinbikPasetoAuthClient } from '@linbik/paseto-auth'
import { createServerTransport } from '@linbik/paseto-auth/server'

export default defineEventHandler(async event => {
  const config = useRuntimeConfig(event)
  const action = getRouterParam(event, 'action')
  setResponseHeader(event, 'Cache-Control', 'private, no-store')
  const methods: Record<string, string> = { session: 'GET', refresh: 'POST', logout: 'POST', protected: 'GET' }
  if (!action || !methods[action]) throw createError({ statusCode: 404 })
  if (event.method !== methods[action]) throw createError({ statusCode: 405 })
  // Only same-origin browser mutations; no caller-controlled upstream URLs/headers.
  if (event.method === 'POST' && (
    getRequestHeader(event, 'origin') !== new URL(config.public.linbikWebOrigin).origin ||
    getRequestHeader(event, 'x-linbik-request') !== '1'
  )) throw createError({ statusCode: 403, statusMessage: 'Invalid request origin' })

  const transport = createServerTransport({
    backendBaseUrl: config.linbikBackendBaseUrl,
    cookieHeader: getRequestHeader(event, 'cookie'),
    onSetCookie: cookie => { appendResponseHeader(event, 'set-cookie', cookie) }
  })
  const client = new LinbikPasetoAuthClient({ backendBaseUrl: config.linbikBackendBaseUrl, fetch: transport.fetch })
  try {
    if (action === 'session') {
      const user = transport.hasCookie('authToken') ? await client.getSession() : null
      if (!user) throw new LinbikAuthError('No session.', 401)
      return { isSuccess: true, data: user }
    }
    if (action === 'refresh') {
      if (!transport.hasCookie('linbikRefreshToken')) throw new LinbikAuthError('No session.', 401)
      return { isSuccess: true, data: await client.refreshToken() }
    }
    if (action === 'logout') {
      await client.signOut()
      return { isSuccess: true }
    }
    const response = await client.fetch(config.linbikProtectedPath)
    setResponseStatus(event, response.status)
    return await response.json()
  } catch (cause) {
    if (cause instanceof LinbikAuthError) {
      setResponseStatus(event, cause.status >= 400 ? cause.status : 502)
      return { isSuccess: false, friendlyMessage: { message: cause.status === 401 ? 'No session.' : 'Authentication request failed.' } }
    }
    throw createError({ statusCode: 502, statusMessage: 'Backend unavailable' })
  }
})
