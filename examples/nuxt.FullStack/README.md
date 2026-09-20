# Linbik Nuxt FullStack

Frontend ve backend aynı Nuxt/Nitro uygulamasındadır. **ASP.NET veya ayrı API sunucusu
gerekmez.** Kimlik sağlayıcı olarak Linbik API kullanılır.

- Tarayıcı: `@linbik/paseto-auth`.
- Nitro: `@linbik/paseto-auth-server`, PKCE/callback, PASETO ve oturum deposu.
- SSR açıkken kullanıcı ilk HTML ile gelir; kapalıyken tarayıcı aynı Nitro uçlarını çağırır.

## Kurulum

Node.js 22+ ve pnpm gerekir. Repo kökünden:

```powershell
pnpm --dir src/Web/Linbik.PasetoAuthManager.Server install
pnpm --dir src/Web/Linbik.PasetoAuthManager.Server keygen
cd examples/nuxt.FullStack
pnpm install
Copy-Item .env.example .env
```

Üretilen `k4.local.…` anahtarını `.env` içindeki `NUXT_LINBIK_SESSION_KEY` alanına yazın.
Kayıtlı Linbik servisinizin API anahtarı, ServiceId ve Web ClientId değerlerini doldurun.
Bu sürüm otomatik/keyless servis kaydı yapmaz. Özel ayarlar `runtimeConfig.linbik`
altındadır; `public` config'e veya git'e konmaz.

Linbik platformunda Web istemcinizin callback URL'sini şu adrese kaydedin:

```text
https://localhost:3000/api/Linbik/callback
```

Sonra:

```sh
pnpm dev --https
```

Tarayıcıdan `https://localhost:3000` açın; geliştirme sertifikasını güvenilir hale
getirin. Ek proxy/CORS ayarı gerekmez. API'nin döndürdüğü onay sayfası API origin'inden
farklıysa gerçek onay origin'ini `NUXT_LINBIK_AUTHORIZATION_ORIGINS` listesine ekleyin.
Bilinmeyen yönlendirme origin'leri güvenlik için reddedilir.

HTTP ile yerel geliştirme gerekiyorsa `NUXT_PUBLIC_LINBIK_WEB_ORIGIN=http://localhost:3000`
ve `NUXT_LINBIK_ALLOW_INSECURE_HTTP=true` ayarlayıp `pnpm dev` çalıştırabilirsiniz.
Bu istisna sadece loopback hostları içindir; canlı ortam HTTPS kullanmalıdır.

## SSR / CSR

`.env` içinde:

```dotenv
NUXT_SSR=true
```

`false` seçilirse frontend tarayıcıda render edilir. Değişiklikten sonra dev sunucusunu
başlatın veya yeniden build alın. Her iki modda Nitro backend çalışmaya devam eder;
bu örnek tamamen statik `nuxt generate` dağıtımı için değildir.

## Sunucu uçları

| Yol | Metot | İşlem |
|---|---|---|
| `/api/Linbik/login` | GET | PKCE giriş işlemi oluşturur, Linbik'e yönlendirir. |
| `/api/Linbik/callback` | GET | Kodu değiştirir, PKCE doğrular, uygulama oturumunu açar. |
| `/api/Linbik/session` | GET | Token yenilemeden oturumu okur. |
| `/api/Linbik/refresh` | POST | Sunucudaki refresh tokenıyla yeniler. |
| `/api/Linbik/logout` | POST | Yerel oturumu iptal edip çerezleri siler. |
| `/api/protected` | GET | Nitro içinde oturum doğrulayan örnek iş API'si. |

POST oturum uçları aynı Origin ve `X-Linbik-Request: 1` başlığı ister; tarayıcı eklentisi
bunu otomatik gönderir. SSR'de sunucu eklentisi aynı isteğin oturum bağlamını doğrudan
çağırır; kendi sunucusuna HTTP isteği yapmaz. Dönen çerezler dış HTML yanıtına eklenir.
`/protected` sayfası route middleware ile, `/api/protected` ise ayrıca sunucuda korunur.

Linbik API anahtarı, refresh tokenı ve entegrasyon tokenları tarayıcıya gönderilmez.
Tarayıcıda HttpOnly erişim çerezi (PASETO v4.local) ve rastgele bir yerel refresh
tutamaç çerezi bulunur. Kullanıcı görünüm modeli Nuxt payload'ına aktarılır.

## Dosyalar

- `server/middleware/linbik.ts`: uygulama ömürlü yönetici/depo, istek başına cookie bağlamı.
- `server/api/Linbik/[action].ts`: Nitro giriş ve oturum uçları.
- `server/api/protected.get.ts`: iş API'si örneği.
- `app/plugins/linbik.server.ts`: SSR'de doğrudan sunucu işlemlerini çağıran SDK adaptörü.
- `app/plugins/linbik.client.ts`: tarayıcı SDK'sı.
- `app/composables/useLinbikAuth.ts`: SSR/CSR ortak oturum durumu.

## Üretim ve sınırlar

```sh
pnpm build
node .output/server/index.mjs
```

Üretim sunucusuna `.env.example` içindeki özel ayarları environment olarak sağlayın;
`.output` sunucusu `.env` dosyasını otomatik okumaz. Public origin ve Linbik'te kayıtlı
callback adresini gerçek HTTPS adresinizle güncelleyin.

Örnek `MemorySessionStore` ile **tek Node süreci** için hazırlanmıştır. Restart/HMR
sonrasında oturumlar kaybolur. Birden çok worker/instance veya serverless için paylaşımlı
kalıcı bir `SessionStore` ve dağıtık `withLock` uygulaması gerekir. Özel token içeren
verileri koruyun. Üretimde login/callback/refresh uçlarına hız sınırı uygulayın.
Kişisel sayfaları CDN cache/prerender içine almayın (`private, no-store` kullanılıyor).

Logout bu uygulamanın oturumunu kapatır; Linbik genel SSO oturumunu kapatmaz. İlk sürüm
PASETO v4.local kullanır; ASP.NET cookie formatıyla paylaşım, keyless provisioning,
key ring ve servisler arası token işlemleri kapsam dışıdır.

## Test

```powershell
pnpm --dir ../../src/Web/Linbik.PasetoAuthManager.Server test
$env:NUXT_SSR='true'
pnpm build
node --test test/fullstack.test.mjs
$env:NUXT_SSR='false'
pnpm build
$env:NUXT_TEST_SSR='false'
node --test test/fullstack.test.mjs
```

Uçtan uca test gerçek Nitro sunucusunu geçici portta açar; Linbik API yerine yerel
fixture kullanır. Giriş, callback, PKCE, cookie aktarımı, SSR/CSR yanıtları, korunan API,
CSRF ve logout sonrası kopyalanmış çerezlerin reddedilmesini doğrular. Gerçek Linbik
hesabıyla giriş/onay ayrıca kendi kayıtlı istemcinizle denenmelidir.

[Sunucu SDK](../../src/Web/Linbik.PasetoAuthManager.Server/README.md) ·
[Web SDK](../../src/Web/Linbik.PasetoAuthManager.Web/README.md) ·
[Ayrı ASP.NET backend kullanan örnek](../nuxt/README.md)
