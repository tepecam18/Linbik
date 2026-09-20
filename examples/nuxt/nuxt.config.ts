// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  ssr: process.env.NUXT_SSR !== 'false',
  routeRules: { '/**': { headers: { 'cache-control': 'private, no-store' } } },
  runtimeConfig: {
    linbikBackendBaseUrl: 'http://localhost:5096',
    linbikProtectedPath: '/Test/Protected',
    public: {
      linbikWebOrigin: 'https://localhost:3000',
      linbikClientName: '',
    }
  }
})
