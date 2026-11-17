<script setup lang="ts">
const route = useRoute()
const { loginUrl } = useLinbikAuth()
</script>

<template>
  <p v-if="route.query.error_code === 'authorization_expired' || route.query.error === 'authorization_expired'" role="alert">
    Giriş işleminin süresi doldu.
    <a :href="loginUrl">Tekrar giriş yap</a>
  </p>
  <p><NuxtLink to="/protected">Korunan sayfayı aç</NuxtLink></p>
</template>
