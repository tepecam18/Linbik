<script setup lang="ts">
import { useRequestEvent } from '#app';
import { readBody, setCookie } from 'h3'

if (!import.meta.env.SSR) {
    console.log("Client side çalışıyor");
}

let tokenState = useState('token', () => '');

if (import.meta.env.SSR) {
    console.log("Sunucu tarafında çalışıyor");
    const jwt = await import('jsonwebtoken')


    function convertToPem(key) {
        // Fazladan boşluk veya yeni satır karakterlerini temizleyelim
        const trimmedKey = key.replace(/\n/g, '').trim();
        // 64 karakterlik parçalar halinde bölelim
        const chunks = trimmedKey.match(/.{1,64}/g);
        // Header ve footer ekleyerek PEM formatı oluşturalım
        return `-----BEGIN PUBLIC KEY-----\n${chunks.join('\n')}\n-----END PUBLIC KEY-----`;
    }

    try {
        const event = useRequestEvent();
        // Örneğin, header üzerinden token almak:
        const { token } = await readBody(event)
        const config = useRuntimeConfig();

        // Environment variable'den alınan key
        const publicKey = config.linbikAppSecret;
        const secret = convertToPem(publicKey);
        console.log("Decoded secret:", secret);
        try {
            const decoded = jwt.verify(token, publicKey, { algorithms: ['RS512'] })

            setCookie(event, 'session', JSON.stringify(decoded), {
                httpOnly: true,
                secure: process.env.NODE_ENV === 'production',
                sameSite: process.env.NODE_ENV === 'production' ? 'none' : 'lax',
                maxAge: 60 * 60 * 24 * 7
            })
            tokenState.value = decoded || 'Token bulunamadı';
            // return { success: true, user: decoded }
        } catch (error) {
            // return { success: false, message: 'Token doğrulanamadı.', error }
        }

    } catch (error) {

    }
}
else {
    console.log("Client side çalışıyor");
    console.log(tokenState.value);
    const session = localStorage.getItem('session');
    if (session) {
        tokenState.value = JSON.parse(session);
    }
}
const config = useRuntimeConfig();
const apiUrl = config.public.apiUrl;

</script>

<template>
    <div>
        <h1>Hello {{ tokenState?.name }}</h1>
        <h1>config: {{ apiUrl }}</h1>
    </div>
</template>