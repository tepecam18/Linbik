import { LinbikPasetoAuthClient } from '@linbik/paseto-auth'

export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig().public
  return {
    provide: {
      linbik: new LinbikPasetoAuthClient({
        backendBaseUrl: config.linbikBackendBaseUrl,
        clientName: config.linbikClientName || undefined
      })
    }
  }
})
