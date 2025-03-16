<script setup lang="ts">
const config = useRuntimeConfig();
const apiUrl = config.public.apiUrl;
const messtickApiBase = config.public.messtickApiBase;
const messtickApiUrl = config.public.messtickApiUrl;
console.log('config', config);

</script>

<template>
    <div>
        <h1>config: {{ apiUrl }}</h1>
        <h1>messtickApiBase: {{ messtickApiBase }}</h1>
        <h1>messtickApiUrl: {{ messtickApiUrl }}</h1>
    </div>
</template>