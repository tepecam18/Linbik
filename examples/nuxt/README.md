# Linbik Nuxt.Examples

Nuxt 4 / Node.js / Bun ile Linbik OAuth 2.1 entegrasyonu örneği.

## 🎯 Genel Bakış

Bu proje, JavaScript/TypeScript tabanlı client uygulamalarının Linbik ile nasıl entegre olacağını göstermektedir. 

> ⚠️ **Not**: Henüz resmi bir JavaScript/TypeScript kütüphanesi bulunmamaktadır. Bu örnek, Linbik OAuth 2.1 API'sini doğrudan kullanarak nasıl entegrasyon yapılacağını gösterir.

**Kullanım Alanları:**
- 🌐 Nuxt/Vue.js web uygulamaları
- 📱 Node.js backend servisleri
- ⚡ Bun runtime ile hızlı uygulamalar
- 🔧 Mevcut Linbik servislerini kullanan client'lar

## 📦 Proje Yapısı

```
nuxt/
├── app/
│   ├── app.vue                 ← Ana layout
│   └── pages/
│       ├── index.vue           ← Ana sayfa
│       ├── login.vue           ← OAuth callback & JWT doğrulama
│       └── [...all].vue        ← Catch-all route
├── server/
│   └── api/                    ← Server API routes (TODO)
├── composables/                ← Vue composables (TODO)
│   └── useLinbik.ts            ← Linbik auth composable
├── nuxt.config.ts              ← Nuxt yapılandırması
├── package.json
├── Dockerfile
└── README.md
```

## 🚀 Hızlı Başlangıç

### 1. Bağımlılıkları Yükle

```bash
cd examples/nuxt

# npm ile
npm install

# veya pnpm ile
pnpm install

# veya bun ile
bun install
```

### 2. Environment Ayarla

`.env` dosyası oluştur:

```bash
# Linbik Server URL
NUXT_PUBLIC_LINBIK_URL=http://localhost:5481
# veya production için
# NUXT_PUBLIC_LINBIK_URL=https://dev.linbik.com

# Service bilgileri (Linbik.App'ten alınır)
NUXT_LINBIK_SERVICE_ID=your-service-guid
NUXT_LINBIK_CLIENT_ID=your-client-guid
NUXT_LINBIK_API_KEY=lnbk_your_api_key

# Integration service public key (RS256 doğrulama için)
NUXT_LINBIK_PUBLIC_KEY=MIIBIjAN...

# App URL
NUXT_PUBLIC_APP_URL=http://localhost:3000
```

### 3. Development Server Başlat

```bash
# npm ile
npm run dev

# veya bun ile
bun run dev
```

Uygulama http://localhost:3000 adresinde başlayacak.

## 🔧 Linbik OAuth 2.1 Entegrasyonu

### Authorization Code Flow (Manuel)

JavaScript/TypeScript için kütüphane olmadan OAuth flow:

```
1. Kullanıcı "Linbik ile Giriş Yap" butonuna tıklar
   ↓
2. Client → Linbik redirect
   GET {LINBIK_URL}/auth/{clientId}
   ↓
3. Kullanıcı Linbik'te giriş yapar
   ↓
4. Linbik → Client callback (authorization code ile)
   GET {APP_URL}/auth/callback?code=xxx
   ↓
5. Client (server-side) → Linbik token exchange
   POST {LINBIK_URL}/oauth/token
   Headers: ApiKey, Code
   ↓
6. Linbik → Token response
   { userId, userName, integrations[], refreshToken }
   ↓
7. Client → Session oluştur (cookie/localStorage)
```

## 💻 Kod Örnekleri

### 1. Login Redirect (Client-Side)

```typescript
// composables/useLinbik.ts
export const useLinbik = () => {
  const config = useRuntimeConfig()
  
  const login = (returnUrl?: string) => {
    const linbikUrl = config.public.linbikUrl
    const clientId = config.public.linbikClientId
    
    // Optional: PKCE code challenge
    const codeVerifier = generateCodeVerifier()
    const codeChallenge = await sha256Base64Url(codeVerifier)
    
    // Store verifier for later validation
    sessionStorage.setItem('pkce_verifier', codeVerifier)
    
    // Redirect to Linbik
    const authUrl = `${linbikUrl}/auth/${clientId}/${codeChallenge}`
    window.location.href = authUrl
  }
  
  const logout = async () => {
    // Clear session cookie
    useCookie('session').value = null
    
    // Clear integration tokens
    const cookies = document.cookie.split(';')
    cookies.forEach(cookie => {
      const name = cookie.split('=')[0].trim()
      if (name.startsWith('integration_')) {
        useCookie(name).value = null
      }
    })
    
    navigateTo('/')
  }
  
  return { login, logout }
}
```

### 2. OAuth Callback (Server-Side API Route)

```typescript
// server/api/auth/callback.get.ts
import { H3Event } from 'h3'

export default defineEventHandler(async (event: H3Event) => {
  const query = getQuery(event)
  const code = query.code as string
  
  if (!code) {
    throw createError({
      statusCode: 400,
      message: 'Authorization code is missing'
    })
  }
  
  const config = useRuntimeConfig()
  
  // Exchange code for tokens
  const response = await $fetch(`${config.linbikUrl}/oauth/token`, {
    method: 'POST',
    headers: {
      'ApiKey': config.linbikApiKey,
      'Code': code,
      'Content-Type': 'application/json'
    },
    body: {}
  })
  
  if (!response) {
    throw createError({
      statusCode: 401,
      message: 'Token exchange failed'
    })
  }
  
  // Set session cookie
  setCookie(event, 'session', JSON.stringify({
    userId: response.user_id,
    userName: response.user_name,
    nickName: response.nick_name
  }), {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 7 // 7 gün
  })
  
  // Set integration token cookies
  if (response.integrations) {
    for (const integration of response.integrations) {
      setCookie(event, `integration_${integration.package_name}`, integration.access_token, {
        httpOnly: true,
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'lax',
        maxAge: 60 * 60 // 1 saat
      })
    }
  }
  
  // Set refresh token cookie
  if (response.refresh_token) {
    setCookie(event, 'linbikRefreshToken', response.refresh_token, {
      httpOnly: true,
      secure: process.env.NODE_ENV === 'production',
      sameSite: 'lax',
      maxAge: 60 * 60 * 24 * 14 // 14 gün
    })
  }
  
  // PKCE validation (optional)
  // Client should validate code_challenge with stored verifier
  
  return sendRedirect(event, '/')
})
```

### 3. Refresh Token (Server-Side API Route)

```typescript
// server/api/auth/refresh.post.ts
export default defineEventHandler(async (event: H3Event) => {
  const refreshToken = getCookie(event, 'linbikRefreshToken')
  
  if (!refreshToken) {
    throw createError({
      statusCode: 401,
      message: 'Refresh token not found'
    })
  }
  
  const config = useRuntimeConfig()
  
  const response = await $fetch(`${config.linbikUrl}/oauth/refresh`, {
    method: 'POST',
    headers: {
      'ApiKey': config.linbikApiKey,
      'RefreshToken': refreshToken,
      'Content-Type': 'application/json'
    },
    body: {}
  })
  
  // Update cookies with new tokens
  // ... (same as callback)
  
  return { success: true }
})
```

### 4. Protected API Route

```typescript
// server/api/protected/profile.get.ts
export default defineEventHandler(async (event: H3Event) => {
  const session = getCookie(event, 'session')
  
  if (!session) {
    throw createError({
      statusCode: 401,
      message: 'Not authenticated'
    })
  }
  
  const user = JSON.parse(session)
  
  return {
    userId: user.userId,
    userName: user.userName,
    nickName: user.nickName
  }
})
```

### 5. Integration Service Proxy

```typescript
// server/api/integration/[service]/[...path].ts
export default defineEventHandler(async (event: H3Event) => {
  const serviceName = getRouterParam(event, 'service')
  const path = getRouterParam(event, 'path') || ''
  
  // Get integration token from cookie
  const token = getCookie(event, `integration_${serviceName}`)
  
  if (!token) {
    throw createError({
      statusCode: 401,
      message: `No token for ${serviceName}`
    })
  }
  
  const config = useRuntimeConfig()
  const serviceConfig = config.integrationServices[serviceName]
  
  if (!serviceConfig) {
    throw createError({
      statusCode: 404,
      message: `Service ${serviceName} not configured`
    })
  }
  
  // Proxy request to integration service
  const response = await $fetch(`${serviceConfig.baseUrl}/${path}`, {
    method: event.method,
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    },
    body: event.method !== 'GET' ? await readBody(event) : undefined
  })
  
  return response
})
```

### 6. Auth Middleware

```typescript
// middleware/auth.ts
export default defineNuxtRouteMiddleware(async (to, from) => {
  const session = useCookie('session')
  
  if (!session.value && to.path !== '/login') {
    return navigateTo('/login')
  }
})
```

### 7. Vue Component Usage

```vue
<!-- pages/dashboard.vue -->
<script setup lang="ts">
definePageMeta({
  middleware: 'auth'
})

const { data: profile } = await useFetch('/api/protected/profile')
const { logout } = useLinbik()
</script>

<template>
  <div class="dashboard">
    <h1>Hoş geldiniz, {{ profile?.nickName }}!</h1>
    <p>User ID: {{ profile?.userId }}</p>
    
    <button @click="logout" class="btn-logout">
      Çıkış Yap
    </button>
  </div>
</template>
```

## ⚙️ Yapılandırma

### nuxt.config.ts

```typescript
export default defineNuxtConfig({
  future: {
    compatibilityVersion: 4
  },
  
  runtimeConfig: {
    // Server-side only (gizli)
    linbikApiKey: process.env.NUXT_LINBIK_API_KEY,
    linbikPublicKey: process.env.NUXT_LINBIK_PUBLIC_KEY,
    
    // Integration services config
    integrationServices: {
      'payment-gateway': {
        baseUrl: 'https://payment.example.com/api'
      },
      'survey-service': {
        baseUrl: 'https://survey.example.com/api'
      }
    },
    
    public: {
      // Client-side accessible
      linbikUrl: process.env.NUXT_PUBLIC_LINBIK_URL || 'http://localhost:5481',
      linbikClientId: process.env.NUXT_LINBIK_CLIENT_ID,
      appUrl: process.env.NUXT_PUBLIC_APP_URL || 'http://localhost:3000'
    }
  }
})
```

## 🔐 JWT Doğrulama (Integration Service Olarak)

Eğer bu uygulama bir integration service olarak çalışacaksa:

```typescript
// server/utils/validateToken.ts
import jwt from 'jsonwebtoken'

export function validateLinbikToken(token: string): LinbikClaims | null {
  const config = useRuntimeConfig()
  
  try {
    // Convert base64 public key to PEM
    const publicKey = `-----BEGIN PUBLIC KEY-----\n${
      config.linbikPublicKey.match(/.{1,64}/g)?.join('\n')
    }\n-----END PUBLIC KEY-----`
    
    const decoded = jwt.verify(token, publicKey, {
      algorithms: ['RS256'],
      issuer: 'Linbik'
    }) as LinbikClaims
    
    return decoded
  } catch (error) {
    console.error('JWT validation failed:', error)
    return null
  }
}

interface LinbikClaims {
  sub: string       // userId
  userName: string
  nickName: string
  aud: string       // serviceId
  iss: string       // "Linbik"
  exp: number
  iat: number
}
```

```typescript
// server/api/integration/protected.get.ts
export default defineEventHandler(async (event: H3Event) => {
  const authHeader = getHeader(event, 'Authorization')
  
  if (!authHeader?.startsWith('Bearer ')) {
    throw createError({ statusCode: 401, message: 'No token' })
  }
  
  const token = authHeader.substring(7)
  const claims = validateLinbikToken(token)
  
  if (!claims) {
    throw createError({ statusCode: 401, message: 'Invalid token' })
  }
  
  return {
    message: 'Protected data',
    userId: claims.sub,
    userName: claims.userName
  }
})
```

## 🔄 Bun Desteği

Bun runtime ile çalıştırmak için:

```bash
# Bun ile yükle
bun install

# Development
bun run dev

# Build
bun run build

# Production
bun run .output/server/index.mjs
```

## 🐳 Docker

```dockerfile
FROM oven/bun:1 AS builder
WORKDIR /app
COPY package.json bun.lockb ./
RUN bun install
COPY . .
RUN bun run build

FROM oven/bun:1
WORKDIR /app
COPY --from=builder /app/.output ./.output
EXPOSE 3000
CMD ["bun", "run", ".output/server/index.mjs"]
```

## 📋 API Karşılaştırma: AspNet vs Nuxt

| Özellik | AspNet (Kütüphane) | Nuxt (Manuel) |
|---------|-------------------|---------------|
| Login | `UseLinbikJwtAuth()` | `$fetch('/auth/{clientId}')` |
| Callback | Otomatik | Server API route |
| Token Storage | Cookie (otomatik) | Cookie (manuel) |
| Refresh | `app.UseLinbikJwtAuth()` | Server API route |
| Integration Proxy | `app.UseLinbikYarp()` | Server API route |
| Rate Limiting | `AddLinbikRateLimiting()` | Nuxt rate limit module |

## 🔮 Gelecek Planlar

- [ ] `@linbik/nuxt` - Nuxt module
- [ ] `@linbik/vue` - Vue plugin
- [ ] `@linbik/node` - Node.js SDK
- [ ] `@linbik/bun` - Bun SDK

## 📖 İlgili Dokümantasyon

- [Linbik.App README](../../src/Clients/Linbik.App/README.md) - OAuth 2.1 API referansı
- [AspNet.Examples](../AspNet/AspNet/README.md) - .NET ile karşılaştırma
- [Nuxt Documentation](https://nuxt.com/docs)

## 📄 Lisans

Bu proje özel bir lisans altında yayınlanmaktadır.

---

**Version**: 1.0.0  
**Last Updated**: 31 Ocak 2026
