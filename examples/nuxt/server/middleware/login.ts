import { createError, defineEventHandler, getRequestURL, readBody, setCookie } from 'h3'
import { verifyLoginToken } from '../utils/verify-login-token.mjs'

export default defineEventHandler(async (event) => {
  if (getRequestURL(event).pathname !== '/login' || event.method !== 'POST') return

  const { token } = await readBody<{ token?: string }>(event) ?? {}
  const config = useRuntimeConfig(event)
  if (!config.linbikAppSecret) {
    throw createError({ statusCode: 500, statusMessage: 'Linbik public key is not configured' })
  }

  try {
    const claims = verifyLoginToken(token, config.linbikAppSecret)
    // Kept for compatibility with the existing example. This is display data,
    // not proof of authentication; protected endpoints must verify a signed token.
    setCookie(event, 'session', JSON.stringify(claims), {
      httpOnly: true,
      secure: process.env.NODE_ENV === 'production',
      sameSite: process.env.NODE_ENV === 'production' ? 'none' : 'lax',
      maxAge: 60 * 60 * 24 * 7,
    })
    event.context.linbikUser = { name: typeof claims.name === 'string' ? claims.name : '' }
  } catch {
    throw createError({ statusCode: 401, statusMessage: 'Login token could not be verified' })
  }
})
