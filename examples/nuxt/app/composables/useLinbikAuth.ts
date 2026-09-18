import { LinbikAuthError, type LinbikUser } from '@linbik/paseto-auth'

export function useLinbikAuth() {
  const { $linbik } = useNuxtApp()
  const user = useState<LinbikUser | null>('linbik:user', () => null)
  const busy = useState('linbik:busy', () => false)
  const error = useState('linbik:error', () => '')

  async function run(action: () => Promise<void>) {
    if (busy.value) return
    busy.value = true
    error.value = ''
    try { await action() } catch (cause) {
      if (cause instanceof LinbikAuthError && cause.status === 401) {
        user.value = null
        error.value = 'Oturum bulunamadı. Linbik ile giriş yapabilirsiniz.'
      } else {
        error.value = cause instanceof Error ? cause.message : 'İşlem tamamlanamadı.'
      }
    } finally { busy.value = false }
  }

  return {
    user, busy, error,
    signIn: () => $linbik.signIn('/'),
    refresh: () => run(async () => { user.value = await $linbik.refreshToken() }),
    signOut: () => run(async () => { await $linbik.signOut(); user.value = null }),
    run
  }
}
