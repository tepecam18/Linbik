// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  ssr: process.env.NUXT_SSR !== 'false',
  routeRules: { '/**': { headers: { 'cache-control': 'private, no-store' } } },
  runtimeConfig: {
    linbik: {
      apiBaseUrl: 'https://api.linbik.com',
      apiKey: '',
      serviceId: '',
      clientId: '',
      sessionKey: '',
      authorizationOrigins: '',
      allowInsecureHttp: false
    },
    public: {
      linbikWebOrigin: 'https://localhost:3000',
      linbikClientName: ''
    }
  }
})
