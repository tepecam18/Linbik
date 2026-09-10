<script setup lang="ts">
const user = useState<{ name: string } | null>('linbik-user', () =>
  useRequestEvent()?.context.linbikUser ?? null,
)
const apiUrl = useRuntimeConfig().public.apiUrl
</script>

<template>
  <div>
    <h1 v-if="user">Hello {{ user.name }}</h1>
    <h1 v-else>Oturum bilgisi bulunamadı.</h1>
    <p>config: {{ apiUrl }}</p>
  </div>
</template>
