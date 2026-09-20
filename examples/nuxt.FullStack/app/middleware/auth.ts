export default defineNuxtRouteMiddleware(async () => {
  const auth = useLinbikAuth()
  const app = useNuxtApp()
  await auth.loadSession(import.meta.client && !app.isHydrating)
  if (auth.error.value) throw createError({ statusCode: 503, statusMessage: 'Oturum doğrulanamadı.' })
  if (!auth.user.value) return navigateTo('/')
})
