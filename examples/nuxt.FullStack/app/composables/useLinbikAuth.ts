import { LinbikAuthError, type LinbikUser } from '@linbik/paseto-auth'

export function useLinbikAuth() {
  const { $linbik } = useNuxtApp()
  const user = useState<LinbikUser | null>('linbik:user', () => null)
  const loaded = useState('linbik:loaded', () => false)
  const busy = useState('linbik:busy', () => false)
  const error = useState('linbik:error', () => '')

  async function run(action: () => Promise<void>) {
    if (busy.value) return
    busy.value = true
    error.value = ''
    try { await action() } catch (cause) {
      if (cause instanceof LinbikAuthError && cause.status === 401) user.value = null
      else error.value = cause instanceof Error ? cause.message : 'İşlem tamamlanamadı.'
    } finally { busy.value = false }
  }

  async function loadSession(force = false) {
    if (loaded.value && !force) return
    await run(async () => {
      user.value = await $linbik.getSession()
      if (!user.value) user.value = await $linbik.refreshToken()
    })
    loaded.value = true
  }

  const config = useRuntimeConfig().public
  const loginUrl = new URL('/api/Linbik/login', config.linbikWebOrigin)
  if (config.linbikClientName) loginUrl.searchParams.set('name', config.linbikClientName)
  loginUrl.searchParams.set('returnPath', '/')

  return {
    user, busy, error, loaded, loadSession,
    loginUrl: loginUrl.href,
    refresh: () => run(async () => { user.value = await $linbik.refreshToken() }),
    signOut: () => run(async () => { await $linbik.signOut(); user.value = null }),
    run
  }
}
