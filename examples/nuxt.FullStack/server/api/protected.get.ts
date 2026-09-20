export default defineEventHandler(async event => {
  const user = await event.context.linbik.getSession()
  if (!user) throw createError({ statusCode: 401 })
  return { message: 'Bu yanıt Nitro backend tarafından üretildi.', user }
})
