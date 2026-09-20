import { LinbikServerError, isLinbikServerError } from '@linbik/paseto-auth-server'
import { linbikResult } from '../../utils/linbik-result'

export default defineEventHandler(async event => {
  const action = getRouterParam(event, 'action')
  const methods: Record<string, string> = { login: 'GET', callback: 'GET', session: 'GET', refresh: 'POST', logout: 'POST' }
  if (!action || !Object.hasOwn(methods, action)) throw createError({ statusCode: 404 })
  if (event.method !== methods[action]) throw createError({ statusCode: 405 })
  const request = event.context.linbik
  try {
    if (event.method === 'POST') event.context.linbikAuth.assertSameOrigin({
      origin: getRequestHeader(event, 'origin') ?? '',
      'x-linbik-request': getRequestHeader(event, 'x-linbik-request') ?? ''
    })
    const query = getQuery(event)
    if (action === 'login') {
      if (query.returnPath !== undefined && typeof query.returnPath !== 'string') throw new LinbikServerError('Invalid return path.')
      return sendRedirect(event, await request.signIn(query.returnPath as string | undefined), 302)
    }
    if (action === 'callback') {
      if (typeof query.code !== 'string') throw new LinbikServerError('Authorization code is required.')
      const { returnPath } = await request.callback(query.code)
      return sendRedirect(event, returnPath, 302)
    }
    return sendWebResponse(event, await linbikResult(request, action))
  } catch (cause) {
    const status = isLinbikServerError(cause) ? cause.status : 500
    throw createError({ statusCode: status, statusMessage: status === 403 ? 'Invalid request origin' : 'Authentication request failed' })
  }
})
