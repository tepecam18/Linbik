# Linbik.PasetoAuthManager.Server

`@linbik/paseto-auth-server`, Node.js 22+ için sunucu kütüphanesidir. ASP.NET olmadan
Linbik giriş akışını ve uygulamanın kendi PASETO oturumunu yönetir. Nitro'ya bağımlı
olmayan çekirdek ve TypeScript tipleri içerir. Henüz npm'e yayımlanmamıştır.

Tarayıcıda mevcut `@linbik/paseto-auth` kullanılır. Bu paketi yalnızca sunucu kodunda
import edin; export koşulu `node` ile sınırlıdır. [Tam Nuxt örneği](../../../examples/nuxt.FullStack/README.md).

## Kurulum

```sh
cd src/Web/Linbik.PasetoAuthManager.Server
pnpm install
pnpm keygen
```

Üretilen `k4.local.…` anahtarını sunucunun gizli yapılandırmasına kaydedin. Anahtar
her açılışta yeniden üretilmez; değiştirmek mevcut erişim çerezlerini geçersiz kılar.

```ts
import { createLinbikAuth, MemorySessionStore } from '@linbik/paseto-auth-server'

// Sunucu ömrü boyunca bir kez. API anahtarı ve sessionKey yalnızca sunucuda kalır.
const auth = createLinbikAuth({
  apiBaseUrl: 'https://api.linbik.com',
  apiKey: process.env.LINBIK_API_KEY!,
  serviceId: process.env.LINBIK_SERVICE_ID!,
  clientId: process.env.LINBIK_CLIENT_ID!,
  sessionKey: process.env.LINBIK_SESSION_KEY!,
  publicOrigin: 'https://app.example.com',
  store: new MemorySessionStore()
})

// Her HTTP isteğinde ayrı cookie bağlamı.
const session = auth.createRequest({
  cookieHeader: request.headers.get('cookie') ?? '',
  onSetCookie: value => responseHeaders.append('set-cookie', value)
})
const user = await session.getSession()
```

## İşlemler

| İşlem | Davranış |
|---|---|
| `signIn(returnPath)` | PKCE oluşturur, Linbik initiate çağrısı yapar, kısa ömürlü işlem çerezi yazar ve izin verilen yönlendirme URL'sini döndürür. |
| `callback(code)` | Tek kullanımlık giriş işlemini tüketir, kodu değiştirir, dönen PKCE challenge'ını zorunlu doğrular; oturum oluşturup kullanıcı ve yerel dönüş yolunu döndürür. |
| `getSession()` | PASETO MAC, süre, issuer, audience, amaç ve sunucudaki oturum kaydını doğrular. Ağ isteği/yenileme yapmaz; geçersiz oturumda null döner. |
| `restoreSession()` | Geçerli oturumu okur; gerekirse sunucuda tutulan refresh tokenıyla yeniler. 401 için null döner, altyapı hatalarını gizlemez. |
| `refreshToken()` | Aynı oturum için kilit altında yeniler. Yakın zamanda yenilenmiş sonucu paylaşarak eşzamanlı isteklerde tokenın tekrar kullanılmasını önler. |
| `signOut()` | Sunucudaki yerel oturumu ve bekleyen giriş işlemini iptal eder, çerezleri siler. Kopyalanmış access çerezi de artık doğrulanmaz. |
| `assertSameOrigin(headers)` | POST oturum işlemleri için Origin ve `X-Linbik-Request: 1` kontrolü. HTTP adaptörü çağırmalıdır. |

`LinbikServerError.status` HTTP durumudur. Ayrı Nitro/SSR paketlerinden gelen hataları
`isLinbikServerError` ile tanıyabilirsiniz. HTTP katmanı hata detaylarını istemciye
süzerek göndermeli ve kişisel yanıtları `private, no-store` işaretlemelidir.

## Sözleşme ve oturum modeli

Linbik API sözleşmesi repodaki .NET istemcisiyle aynıdır:

- `POST /api/oauth/initiate`: ApiKey başlığı; clientId, codeChallenge, extraData.returnPath.
- `POST /api/oauth/token`: ApiKey ve Code başlıkları; serviceId gövdesi.
- `POST /api/oauth/refresh`: ApiKey ve RefreshToken başlıkları; serviceId gövdesi.

Bu sürüm kayıtlı service/client kimlikleri ister; keyless provisioning içermez. Callback
URL'si Linbik platformunda uygulamanızın Nitro callback adresine kaydedilmelidir.
API'nin döndürdüğü onay sayfası farklı bir origin kullanıyorsa `authorizationOrigins`
listesine tam origin'i ekleyin. Liste tahmin edilmez; bilinmeyen origin reddedilir.

Yerel erişim çerezi `paseto-ts` ile **v4.local** üretilir. Kriptografik algoritma yeniden
uygulanmaz. Linbik'ten gelen refresh ve entegrasyon tokenları sunucu deposunda kalır.
Tarayıcı refresh çerezi rastgele bir yerel tutamaçtır; depoda anahtar olarak SHA-256
özeti kullanılır. Access token bu kayıtla ilişkilidir; bütün kullanıcı bilgileri depo
içinden alınır. Bu cookie/store modeli ASP.NET çerezleriyle doğrudan değiştirilebilir değildir.

Varsayılan süreler access için 15 dakika, refresh için 14 gündür; API daha kısa süre
verirse o sınır uygulanır. `accessTtlSeconds` ve `refreshTtlSeconds` ile değiştirilebilir.
HTTPS'te çerezler `__Host-` önekli, Secure, HttpOnly, SameSite=Lax ve Path=/ olur.
`allowInsecureHttp: true` sadece loopback adreslerinde geliştirme içindir; burada
`linbik_` öneki ve Secure olmayan çerezler kullanılır.

## Depo ve dağıtım

`MemorySessionStore` **tek Node süreci için örnek depodur**. Yeniden başlatmada oturumlar
silinir. Varsayılan 10.000 kayıt sınırı ve süresi dolan kayıt temizliği vardır. Yatay
ölçekleme/serverless için aynı arayüzü uygulayan kalıcı/paylaşımlı depo gerekir:

- `get`, `set`, `delete`: `expiresAt` (milisaniye) süresine uyan kayıt işlemleri.
- `withLock(key, action)`: aynı key için bütün süreçler arasında karşılıklı dışlama.

Depo upstream refresh ve entegrasyon tokenları içerir; erişimini sınırlandırın ve
kalıcı depolamada koruyun. Oturum sorguları cache/prerender edilmemelidir. Üretimde
HTTP katmanına giriş/callback/refresh hız sınırı ekleyin. Logout uygulamadaki oturumu
kapatır; Linbik SSO hesabını veya sağlayıcıdaki refresh tokenını uzaktan iptal etmez.
Anahtar rotasyonu/key ring, v4.public ve servisler arası token uçları bu sürümde yoktur.

## Test

```sh
pnpm test
```

Testler PKCE eksikliği/uyuşmazlığı, callback replay, bozuk/süresi dolmuş token, yanlış
audience/amaç, eşzamanlı refresh, logout ve yönlendirme/CSRF sınırlarını kapsar.
PASETO uygulaması: [paseto-ts](https://github.com/auth70/paseto-ts).
