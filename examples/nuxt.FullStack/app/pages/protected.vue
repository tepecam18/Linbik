<script setup lang="ts">
definePageMeta({ middleware: 'auth' })
const { user } = useLinbikAuth()
</script>

<template>
  <section v-if="user">
    <h2>Korunan sayfa</h2>
    <p>Bu içerik gösterilmeden önce oturum doğrulandı. Merhaba, {{ user.displayName }}.</p>
    <NuxtLink to="/">Ana sayfaya dön</NuxtLink>
  </section>
</template>
