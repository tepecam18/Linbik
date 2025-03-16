// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  future: {
    compatibilityVersion: 4
  },
  compatibilityDate: '2025-03-05',
  devtools: { enabled: true },
  runtimeConfig: {
    // Sadece server tarafında erişilebilen hassas veriler
    secretKey: process.env.SECRET_KEY,
    linbikAppSecret: '',
    linbikAppId: '',
    public: {
      apiUrl: process.env.NUXT_PUBLIC_API_URL || 'http://localhost:3000',
      messtickApiBase: process.env.messtickApiBase || 'https://api.messtick.com',
      messtickApiUrl: process.env.NUXT_PUBLIC_MESSTICK_API_URL || 'https://api.messtick.com',
    }
  }
})