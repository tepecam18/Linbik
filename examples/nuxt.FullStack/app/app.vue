<script setup lang="ts">
const { user, busy, error, loginUrl, loadSession, refresh, signOut, run } = useLinbikAuth()
const { $linbik } = useNuxtApp()
const result = ref('')
const ready = ref(false)

await loadSession()
onMounted(() => { ready.value = true })

watch(user, () => { result.value = '' })

function callApi() {
  result.value = ''
  return run(async () => {
    const response = await $linbik.fetch('/api/protected')
    if (!response.ok) throw new Error(`API isteği başarısız: ${response.status}`)
    result.value = JSON.stringify(await response.json(), null, 2)
  })
}
</script>

<template>
  <main>
    <NuxtRouteAnnouncer />
    <p class="eyebrow">LINBIK · NUXT FULLSTACK</p>
    <h1>Tek hesap, güvenli oturum.</h1>
    <p>Linbik ile giriş yapın, oturumunuzu yenileyin ve korunan API'yi deneyin.</p>
    <section aria-live="polite">
      <h2>{{ user ? `Merhaba, ${user.displayName}` : 'Hoş geldiniz' }}</h2>
      <p v-if="user">@{{ user.username }}</p>
      <p v-else>Devam etmek için Linbik hesabınızı kullanın.</p>
      <p v-if="user?.integrations.length">Entegrasyonlar: {{ user.integrations.join(', ') }}</p>
      <div class="actions">
        <a v-if="!user" :href="loginUrl">Linbik ile giriş yap</a>
        <button :disabled="!ready || busy" @click="refresh">Oturumu yenile</button>
        <button v-if="user" :disabled="busy" @click="callApi">Korunan API'yi çağır</button>
        <button v-if="user" :disabled="busy" @click="signOut">Çıkış yap</button>
      </div>
      <p v-if="busy" role="status">İşlem sürüyor…</p>
      <p v-if="error" role="alert" class="error">{{ error }}</p>
      <pre v-if="result">{{ result }}</pre>
    </section>
    <NuxtPage />
  </main>
</template>

<style>
:root { font-family: system-ui, sans-serif; color: #17352d; background: #f4f7f4; }
body { margin: 0; }
main { max-width: 760px; margin: 10vh auto; padding: 24px; }
.eyebrow { letter-spacing: .16em; font-size: .8rem; font-weight: 700; }
h1 { font-size: clamp(2rem, 6vw, 3.6rem); line-height: 1.1; }
section { margin-top: 40px; padding: 28px; background: white; border: 1px solid #d5e0d9; border-radius: 20px; }
.actions { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 24px; }
button { padding: 12px 18px; border: 0; border-radius: 8px; background: #175e48; color: white; font: inherit; cursor: pointer; }
button:disabled { opacity: .5; cursor: wait; }
button:focus-visible { outline: 3px solid #cf8d27; outline-offset: 3px; }
.error { color: #a02727; }
pre { overflow: auto; background: #f4f7f4; padding: 16px; border-radius: 8px; }
</style>
