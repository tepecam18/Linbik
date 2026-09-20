# Linbik Web SDK — Nuxt SSR / CSR örneği

Aynı uygulama iki modda çalışır. `.env` içindeki `NUXT_SSR=true` sunucuda render,
`NUXT_SSR=false` tarayıcıda render seçer. Bu bir **derleme ayarıdır**: değiştirince
dev sunucusunu yeniden başlatın veya yeniden derleyin. Her iki mod Nitro sunucusunu
kullanır; `nuxt generate` ile tamamen statik dağıtım bu örneğin sunucu adaptörünü içermez.
Framework bağımsız SDK'nin doğrudan backend kullanımı ayrıca desteklenir.

## Mimari

```text
Tarayıcı → https://localhost:3000 (Caddy)
             /api/Linbik/login, callback → ASP.NET :5096
             diğer yollar                → Nuxt :3001
                                             /api/auth/* → ASP.NET :5096
```

Login ve callback aynı public origin üzerinden backend'e gider; PKCE ve oturum çerezleri
web hostunda kalır. Tarayıcı ayrı backend origin'ine istek yapmaz; bu örnek için CORS gerekmez.
Backend yalnızca güvenilen proxy/sunucu tarafından erişilebilir olmalıdır.

SSR'de istek başına oluşturulan SDK yalnızca Linbik çerezlerini backend'e taşır.
`GET /api/Linbik/session` access tokenı mevcut JWT/PASETO doğrulayıcısıyla kontrol eder.
Geçerli oturumda refresh çağrılmaz. Oturum geçersizse ve refresh çerezi varsa bir kez
refresh yapılır. Dönen Set-Cookie başlıkları ayrı ayrı HTML yanıtına eklenir; güncel
çerezler aynı isteğin sonraki backend çağrılarında da kullanılır. Tarayıcıya sadece
kullanıcı görünüm modeli aktarılır, tokenlar Nuxt payload'ına konmaz.

CSR'de aynı oturum kontrolü tarayıcı açıldıktan sonra `/api/auth/session` üzerinden
çalışır. SSR hydration sırasında işlem tekrarlanmaz. `/protected` sayfasının middleware'i
SSR'de HTML üretilmeden, CSR'de sayfa gösterilmeden oturumu kontrol eder. API yetkisi her
zaman backend tarafından ayrıca doğrulanır. Backend kesintisi giriş yapılmamış durumuyla
karıştırılmaz; korunan sayfa 503 verir.

## Yerel kurulum

ASP.NET örneğini `http://localhost:5096` üzerinde çalıştırın. Güncel kaynak kodundaki
session endpoint'ini içeren backend gerekir. Örnek şu anda JWT kullanır; PASETO için
`AddLinbikJwtAuth` / `UseLinbikJwtAuth` yerine PASETO karşılıklarını kullanabilirsiniz.
Her iki manager da aynı session endpoint'ini ekler.

```powershell
cd examples/nuxt
pnpm install
Copy-Item .env.example .env
pnpm dev --port 3001
```

Ayrı terminalde Caddy kurulu olmalı:

```sh
caddy run --config Caddyfile
```

Tarayıcıda **https://localhost:3000** açın. Caddy'nin yerel CA sertifikasını güvenilir
hale getirin. TLS doğrulamasını kapatmayın. `Secure` çerezler nedeniyle public uç HTTPS
kullanır; Nuxt ve ASP.NET arasındaki yerel bağlantı HTTP olabilir.

Backend `Linbik:Clients` içindeki Web istemcinizi şu şekilde ayarlayın:

```json
{
  "Name": "Web",
  "ClientId": "YOUR_REGISTERED_WEB_CLIENT_ID",
  "RedirectUrl": "https://localhost:3000",
  "ActionResultType": "Redirect"
}
```

Linbik platformundaki callback adresi **https://localhost:3000/api/Linbik/callback**
olmalıdır; backend'in özel portunu kullanmayın. Caddy public Host ve forwarded scheme
bilgilerini backend'e taşır. Backend `CookieDomain` ayarını boş veya public host ile
uyumlu tutun. Proxy yalnızca login/callback yollarını ASP.NET'e açar; refresh/logout
Nitro katmanından geçer. Public origin değişirse Caddyfile, client RedirectUrl,
platform callback ve `NUXT_PUBLIC_LINBIK_WEB_ORIGIN` birlikte güncellenmelidir.

## Dosyalar ve güvenlik sınırları

- `app/plugins/linbik.server.ts`: her SSR isteği için ayrı cookie transport.
- `app/plugins/linbik.client.ts`: aynı origin'e istek yapan tarayıcı istemcisi.
- `app/composables/useLinbikAuth.ts`: iki modun ortak oturum durumu.
- `server/api/auth/[action].ts`: yalnızca session, refresh, logout ve örnek protected çağrıları. Genel amaçlı açık proxy değildir.
- `app/middleware/auth.ts`: korunan sayfa kontrolü.
- `Caddyfile`: login/callback dahil aynı origin yerleşimi.

Refresh/logout public uçları POST ister; Origin ve özel istek başlığı doğrulanır.
Logout backend'in mevcut GET sözleşmesine sunucuda çevrilir. Token içeren yanıtlar ve
kişisel HTML `private, no-store` döner; bunları CDN'de cache/prerender etmeyin.
Backend adresi private runtime config'dedir. Cookie transport sadece bu backend'e
istek yapar, yönlendirmeleri izlemez, Domain'i kaldırıp çerezi public hosta bağlar;
Secure/HttpOnly/SameSite/expiry korunur. Bu adaptör aynı public host altında çalışan
tek uygulama içindir. Cookie adlarını paylaşan farklı uygulamalar ayrı host kullanmalıdır.

Bir SDK örneği eşzamanlı refresh'i birleştirir; farklı HTTP istekleri veya sekmeler arası
refresh yarışları backend refresh-token yöneticisinin sorumluluğundadır. `integrations`
listesi yalnızca arayüz ipucudur, yetki kanıtı değildir. Korunan API çağrıları otomatik
tekrarlanmaz.

## Doğrulama

```powershell
node --test ../../src/Web/Linbik.PasetoAuthManager.Web/test/*.test.js
$env:NUXT_SSR='true'
pnpm build
node --test test/session.test.mjs
$env:NUXT_SSR='false'
pnpm build
$env:NUXT_TEST_SSR='false'
node --test test/session.test.mjs
```

Testler yerel sahte backend ile kişisel HTML, kullanıcı izolasyonu, cookie yenileme,
çıkış, anonim yönlendirme, hata ayrımı ve CSRF sınırını kontrol eder. Gerçek Linbik
giriş/onay/callback akışı ayrıca kayıtlı istemciyle denenmelidir.

[SDK](../../src/Web/Linbik.PasetoAuthManager.Web/README.md) ·
[Nuxt cookie aktarımı](https://nuxt.com/docs/4.x/getting-started/data-fetching) ·
[Caddy reverse proxy](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy)
