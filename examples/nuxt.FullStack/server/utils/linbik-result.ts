import { LinbikServerError, isLinbikServerError, type LinbikAuthRequest } from '@linbik/paseto-auth-server'

// Shared by Nitro routes and the SSR plugin; never contains browser-visible credentials.
export async function linbikResult(request: LinbikAuthRequest, action: string): Promise<Response> {
  try {
    let data
    if (action === 'session') {
      data = await request.getSession()
      if (!data) throw new LinbikServerError('No session.', 401)
    } else if (action === 'refresh') data = await request.refreshToken()
    else if (action === 'logout') await request.signOut()
    else throw new LinbikServerError('Not found.', 404)
    return Response.json({ isSuccess: true, data }, { headers: { 'Cache-Control': 'private, no-store' } })
  } catch (cause) {
    const status = isLinbikServerError(cause) ? cause.status : 500
    return Response.json({ isSuccess: false, friendlyMessage: { message: status === 401 ? 'No session.' : 'Authentication request failed.' } }, { status, headers: { 'Cache-Control': 'private, no-store' } })
  }
}
