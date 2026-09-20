# Linbik.PasetoAuthManager.Web

Framework bağımsız, bağımlılıksız ESM web istemcisi. Paket adı: `@linbik/paseto-auth`.
JavaScript ile çalışır ve TypeScript tiplerini içerir. Henüz npm'e yayımlanmamıştır;
yerel paket bağlantısı için [Nuxt örneğine](../../../examples/nuxt/README.md) bakın.

```ts
import { LinbikPasetoAuthClient, LinbikAuthError } from '@linbik/paseto-auth'

const auth = new LinbikPasetoAuthClient({
  backendBaseUrl: 'https://api.example.com',
  clientName: 'Web'
})

// Giriş düğmesinde: callback ve PKCE işlemleri backend'dedir.
auth.signIn('/dashboard')

// Önce oturumu oku; yalnızca gerektiğinde yenile.
try {
  const user = await auth.getSession() ?? await auth.refreshToken()
  console.log(user.displayName, user.integrations)
} catch (error) {
  if (error instanceof LinbikAuthError && error.status === 401) {
    // Giriş düğmesini gösterin.
  } else throw error
}

const response = await auth.fetch('/api/orders')
if (!response.ok) throw new Error(`HTTP ${response.status}`)
await auth.signOut()
```

## API

- `getSignInUrl(returnPath = '/')`: backend login adresini üretir; bağlantılarda kullanılabilir.
- `signIn(returnPath = '/')`: tarayıcıyı login adresine yönlendirir. Backend client ayarı `ActionResultType: "Redirect"` olmalıdır.
- `getSession()`: `/api/Linbik/session` üzerinden doğrulanmış kullanıcıyı okur; 401 için `null` döner. Token yenilemez. Diğer hataları çağırana iletir.
- `refreshToken()`: `/api/Linbik/refresh` adresine POST gönderir ve `LinbikUser` döndürür. Aynı istemcide eşzamanlı yenilemeler tek isteği paylaşır.
- `signOut()`: `/api/Linbik/logout` adresine GET gönderir; devam eden yenilemeyi bekler. Hataları çağırana iletir.
- `fetch(path, init)`: backend'e çerezli istek gönderir, standart `Response` döndürür. `response.ok` kontrolü çağırana aittir. Otomatik refresh/retry yapmaz; yazma işlemleri tekrarlanmaz.

`loginPath`, `sessionPath`, `refreshPath`, `logoutPath`, `logoutMethod` (varsayılan GET), standart Fetch uyumlu `fetch` ve
`navigate(url)` adaptörleri özelleştirilebilir. Yollar `/` ile başlar ve backend
adresinin isteğe bağlı dağıtım önekinin altında çözülür. Mutlak API adresleri kabul
edilmez. HTTP hata kodu `LinbikAuthError.status`, sunucu yanıtı `response` içindedir;
ağ/iptal hataları özgün halleriyle iletilir.

## Web ve SSR

PASETO üretimi/doğrulaması ve PKCE backend'e aittir. SDK token okumaz veya localStorage'a
yazmaz; tarayıcı HttpOnly çerezleri yönetir. İstekler `credentials: include`,
`cache: no-store`, `redirect: error` kullanır. Login için tam sayfa gezinme gerekir.

Paket SSR sırasında import edilebilir; varsayılan `signIn` tarayıcı gerektirir.
Node fetch tarayıcı çerezlerini taşımaz ve Set-Cookie yanıtlarını tarayıcıya aktaramaz.
SSR için `@linbik/paseto-auth/server` istek bazlı cookie transport sağlar:

```ts
import { createServerTransport } from '@linbik/paseto-auth/server'

// Her gelen HTTP isteğinde yeniden oluşturun.
const transport = createServerTransport({
  backendBaseUrl: 'http://internal-backend:5096',
  cookieHeader: request.headers.get('cookie') ?? '',
  onSetCookie: cookie => responseHeaders.append('set-cookie', cookie)
})
const auth = new LinbikPasetoAuthClient({
  backendBaseUrl: 'http://internal-backend:5096',
  fetch: transport.fetch
})
const user = await auth.getSession()
```

Adaptör yalnızca yapılandırılan backend'e Linbik çerezlerini taşır; redirect izlemez.
Set-Cookie başlıklarını ayrı ayrı iletir ve aynı isteğin cookie jar'ını günceller.
Domain kaldırılır, Path `/` yapılır; HttpOnly/Secure/SameSite ve sona erme bilgileri korunur.
Bu, backend'in tek public web hostu arkasında çalıştığı kurulum içindir. `onSetCookie`
sayfanın dış HTTP yanıtına başlık eklemelidir; tokenları HTML/payload'a koymayın.
Adaptörü global/singleton olarak paylaşmayın. Nuxt örneği SSR ve `ssr: false` modlarını
aynı oturum arayüzüyle destekler; SSR sırasında alınan kullanıcı hydration'da tekrar sorgulanmaz.
Tamamen statik uygulamalar tarayıcı SDK'sını doğrudan backend ile kullanabilir.

Ayrı origin kullanımında backend izin verilen web origin'lerine credentials destekli
CORS açmalıdır. HTTPS ve uygun SameSite ayarları gerekir; farklı sitelerde üçüncü
taraf çerez kısıtları geçerlidir. Üretimde aynı site/reverse proxy kurulumu tercih edin.
Çerezli yazma işlemlerinin CSRF koruması backend sorumluluğundadır. Yenileme koordinasyonu
tek istemci örneği kapsamındadır, sekmeler arası değildir.

## Test ve paketleme

```sh
node --test
npm pack --dry-run
```
