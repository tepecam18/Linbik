// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  runtimeConfig: {
    public: {
      linbikBackendBaseUrl: 'https://localhost:7020',
      linbikClientName: '',
      linbikProtectedPath: '/Test/Protected'
    }
  }
})
