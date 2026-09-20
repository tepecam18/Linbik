import { createLinbikAuth, MemorySessionStore } from '@linbik/paseto-auth-server'

// Application-scoped store; request cookie state is never shared.
let auth: ReturnType<typeof createLinbikAuth> | undefined

export default defineEventHandler(event => {
  if (event.path.startsWith('/_nuxt/')) return
  const config = useRuntimeConfig(event)
  if (!auth) {
    const options = config.linbik
    if (!options.apiKey || !options.serviceId || !options.clientId || !options.sessionKey) {
      throw createError({ statusCode: 503, statusMessage: 'Configure private NUXT_LINBIK credentials and session key first.' })
    }
    auth = createLinbikAuth({
      ...options,
      publicOrigin: config.public.linbikWebOrigin,
      allowInsecureHttp: String(options.allowInsecureHttp) === 'true',
      authorizationOrigins: options.authorizationOrigins.split(',').map(value => value.trim()).filter(Boolean),
      store: new MemorySessionStore()
    })
  }
  setResponseHeader(event, 'Cache-Control', 'private, no-store')
  event.context.linbikAuth = auth
  event.context.linbik = auth.createRequest({
    cookieHeader: getRequestHeader(event, 'cookie'),
    onSetCookie: cookie => { appendResponseHeader(event, 'set-cookie', cookie) }
  })
})
