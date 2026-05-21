# Plan: Linbik Mobile SDK + Linbik.Mobil Authorize Flow

Linbik için, üçüncü taraf mobil uygulamaların kullanıcıyı Linbik üzerinden yetkilendirebileceği, "Sign in with Linbik" benzeri bir mobil akış. Akış: 3rd-party client SDK → Linbik.Server (PKCE initiate) → Linbik.Mobil app (Universal/App Link ile açılır, kullanıcı consent) → tek kullanımlık `access_code` → client backend'de token exchange. Linbik.Mobil yüklü değilse OS otomatik browser'a düşer; web tarafındaki mevcut OAuth akışı bu fallback'i karşılar. Önerilen yol: KMP tabanlı tek SDK (Android AAR + iOS XCFramework), `Linbik.JwtAuthManager`'da iki yeni endpoint, Linbik.Mobil app ptts repo'sunda Compose Multiplatform.

## Mimari Akış (Authorization Code + PKCE for Native, Server-Side Verifier)

```
[3rd-party Mobile App]                              [3rd-party Backend]                          [Linbik.Server]                           [Linbik.Mobil App]
        │                                                   │                                            │                                          │
        │  1. SDK.startLogin()                              │                                            │                                          │
        │ ─────────────────────────────► GET /api/Linbik/login?name=mobile                               │                                          │
        │                                                   │ 2. verifier = random()                     │                                          │
        │                                                   │    challenge = SHA256(verifier)            │                                          │
        │                                                   │ ─────────► POST /api/oauth/initiate        │                                          │
        │                                                   │            (code_challenge, clientId)      │                                          │
        │                                                   │ ◄────────── { sessionToken }               │                                          │
        │                                                   │ verifier'ı session-bound cache'e kaydet    │                                          │
        │ ◄──────────────────────── { sessionToken, deepLink }                                           │                                          │
        │                                                   │                                            │                                          │
        │  3. Universal/App Link'i aç:                                                                                                              │
        │     https://auth.linbik.app/m?token=...                                                                                                   │
        │                                                                                                                                          │
        │     ─ Linbik.Mobil yüklüyse → OS app'i açar (Translucent Activity / App Switch) ────────────────────────────────────────────────────────► │
        │     ─ Yüklü değilse        → OS browser'ı açar → mevcut web auth akışı çalışır                                                            │
        │                                                                                            ┌─────────────────────────────────────────────┘
        │                                                                                            │
        │                                                                                            │ 4. Linbik.Mobil:
        │                                                                                            │    Android: caller package + SHA-256 hash
        │                                                                                            │    iOS:     caller redirect URI + Universal Link domain
        │                                                                                            │    DB: clients tablosunda doğrula
        │                                                                                            │    Oturum yoksa veya mail onaylı değilse
        │                                                                                            │      → tam onboarding (login/register + mail verify)
        │                                                                                            │      → consent
        │                                                                                            │    access_code = oneTimeCode()
        │                                                                                            │
        │ ◄─── 5. callback deep link: clientscheme://auth/cb?code=ACCESS_CODE ─────────────────────────┘
        │                                                                                            
        │  6. SDK.handleCallback(code)                       │                                                                                       
        │ ──────────────────────────► POST /api/Linbik/callback                                                                                      
        │                              { code }              │                                                                                       
        │                                                   │ 7. cache'ten verifier'ı al                                                            
        │                                                   │ ─────────► POST /api/oauth/token                                                       
        │                                                   │            (code, code_verifier, ApiKey, ServiceId)                                    
        │                                                   │ ◄──── LinbikTokenResponse (auth + integration JWTs)                                    
        │                                                   │ Cookies / mobile session establish                                                    
        │ ◄──────── { authenticated: true, profile, integrations } ────────                                                                          
```

## Ana Premiseler

1. **PKCE verifier 3rd-party backend'de üretilir ve saklanır** — client mobile app'e asla gönderilmez; sadece kısa ömürlü `sessionToken` (initiate response'u) ve `accessCode` (callback) telde gider. Linbik.Server `code_challenge`'ı kendi DB'sinde tutar, token exchange'de `SHA256(verifier) == challenge` doğrular. Mevcut `/api/oauth/token` zaten bu modeli destekliyor.
2. **Linbik.Mobil hem Android hem iOS'ta aynı `https://auth.linbik.app/m?token=...` Universal/App Link'i intercept eder.** Yüklü değilse OS browser'a düşer; bu durumda Linbik.Server'ın mevcut web auth akışı (`/oauth/authorize` + consent UI) devreye girer, callback redirect URI üzerinden client uygulamaya geri döner. Tek deep-link, tek kod yolu, OS karar verir.
3. **Client kimlik doğrulaması platform-spesifiktir** ve `clients` tablosunda saklanır (zaten mevcut `Service`/`Client` modeli üzerinden):
   - **Android**: `LocalPackageManager` ile çağıran activity'nin package name + signing certificate SHA-256 fingerprint'i alınır, DB'deki `clients.android_package_name` ve `clients.android_sha256_fingerprints` (yeni kolonlar) ile karşılaştırılır.
   - **iOS**: Çağıran uygulamanın bundle id alınamaz (iOS sandbox). Doğrulama redirect URI + Universal Link associated domain üzerinden yapılır; AASA dosyası sadece kayıtlı bundle id'lere izin verir. Kayıt: `clients.ios_bundle_id` + `clients.ios_redirect_uris` (yeni kolonlar).
4. **SDK Kotlin Multiplatform'dur**, ortak modül + iki actual binding. Çıktı: Android için AAR (Maven), iOS için XCFramework (Swift Package Manager). API yüzeyi her iki platformda eş.
5. **Linbik.JwtAuthManager** üçüncü taraf backend'de host edilir ve iki yeni endpoint barındırır: `/api/Linbik/login?name=mobile` (initiate proxy) ve `/api/Linbik/callback` (token exchange proxy). Mevcut `/api/Linbik/login` (web) bozulmaz; `name` query param ile mobile-aware branching.
6. **`Linbik.Mobil` ptts repo'sunda** Compose Multiplatform tabanlı bir uygulamadır (kullanıcının ana Linbik mobil deneyimi). KMP shared SDK'yı tüketir, ek olarak server tarafıyla konuşan kendi auth UI'sına sahiptir.
7. **Linbik.Mobil oturumsuz veya mail-doğrulanmamış kullanıcı durumunda tam onboarding'e zorlar** — SDK akışından gelen kullanıcı, Linbik.Mobil'de oturum yoksa veya `EmailConfirmed = false` ise consent ekranına geçmeden önce zorunlu olarak login/register + email verify akışına alınır. Kullanıcı bu adımları tamamlamadan callback dönüşü yapılmaz; iptal/geri tuşunda client SDK'ya `error=user_cancelled` ile callback edilir.

## Phases / Steps

### Faz 1 — Server tarafı (Linbik.Server, ptts repo)
1. **DB migration**: `clients` tablosuna 4 yeni kolon: `android_package_name TEXT NULL`, `android_sha256_fingerprints TEXT NULL` (CSV — birden çok release/debug imza), `ios_bundle_id TEXT NULL`, `ios_redirect_uris TEXT NULL` (CSV).
2. **Yeni endpoint: `POST /api/oauth/mobile/verify-caller`** — Linbik.Mobil app içinden çağrılır; payload `{ clientId, platform: "android"|"ios", packageName?, sha256?, bundleId?, redirectUri? }` → `{ valid: bool, clientName, serviceName }`. Sunucu DB lookup yapıp imza/bundle eşleşmesini doğrular. Bu, Linbik.Mobil'in DB'ye direkt erişmek yerine merkezi doğrulama yapmasını sağlar.
3. **Mevcut `/api/oauth/initiate` genişletmesi**: payload'a opsiyonel `flowType: "web"|"mobile"` alanı; mobile flow'da `RedirectUri` doğrulaması iOS için Universal Link domain'i + custom scheme'i de kabul edecek şekilde gevşetilir.
4. **`/api/oauth/token` değişmiyor** — verifier+code akışı zaten destekleniyor.
5. **`/api/oauth/mobile/issue-code` endpoint'i**: Linbik.Mobil onboarding+consent tamamlandıktan sonra `sessionToken`'a karşılık tek kullanımlık `access_code` üretir. Çağrı yapan kullanıcının `EmailConfirmed = true` olduğunu zorunlu doğrular; aksi halde `403 email_not_verified` döner (ek savunma katmanı; ilk savunma Linbik.Mobil UI'ında).

### Faz 2 — Linbik.JwtAuthManager (NuGet package, Linbik repo)
1. Yeni controller: `LinbikMobileAuthController` (route prefix: `/api/Linbik`).
2. **`GET /api/Linbik/login?name=mobile&clientHint=...`**:
   - `code_verifier` üret (RFC 7636 — 43–128 char, URL-safe random).
   - `code_challenge = base64url(SHA256(verifier))`.
   - `LinbikAuthClient.InitiateAsync(code_challenge, flowType="mobile")` çağır.
   - `verifier`'ı server-side cache'e (IDistributedCache veya yeni `MobileSessionStore`) `sessionToken` anahtarıyla, 5 dakika TTL ile yaz.
   - Response: `{ sessionToken, deepLink: "https://auth.linbik.app/m?token={sessionToken}" }`.
3. **`POST /api/Linbik/callback`** (body: `{ code: string }`):
   - Cookie veya body'den `sessionToken`'ı oku → cache'ten `verifier` çek.
   - `LinbikAuthClient.ExchangeCodeAsync(code, verifier)` (mevcut metod imzasına `verifier` parametresi eklenecek).
   - Başarılıysa `JwtAuthManagerExtensions.SetAuthCookies` mevcut akışıyla cookie'leri kur.
   - Response: client backend'in mobile uygulamasına dönmesi için profile + integrations.
4. **`MobileSessionStore` servisi**: Default `IMemoryCache` implementation; production için `IDistributedCache` (Redis) DI ile değiştirilebilir.
5. `LinbikAuthClient.ExchangeCodeAsync`'a `code_verifier` parametresi ekle; mevcut çağrılar geriye dönük uyumlu kalır (verifier null ise eski davranış).

### Faz 3 — KMP SDK (`Linbik/src/Mobile/Linbik.Sdk.Mobile/`)
1. **Project skeleton**:
   - Gradle KMP project, targets: `androidTarget`, `iosArm64`, `iosSimulatorArm64`, `iosX64`.
   - Modules: `commonMain` (DTOs, orchestration), `androidMain` (Translucent Activity launcher, App Link helper), `iosMain` (UIApplication open helper, ASWebAuthenticationSession fallback hook).
2. **Public API (commonMain interface)**:
   - `LinbikSdk.configure(backendBaseUrl: String, customScheme: String)` — uygulama başlangıcında.
   - `LinbikSdk.startLogin(activity/UIViewController, onResult: (LinbikAuthResult) -> Unit)`.
   - `LinbikSdk.handleCallback(uri: String)` — Android `onNewIntent` / iOS `application(_:open:)` içinden çağrılır.
3. **Android binding** (androidMain):
   - `LinbikLoginActivity` (Translucent theme: `Theme.AppCompat.Translucent.NoTitleBar`).
   - Backend'in döndüğü `deepLink`'i `Intent.ACTION_VIEW` ile başlat. `Intent.FLAG_ACTIVITY_NEW_TASK` + Linbik.Mobil package query (manifest `<queries>`).
   - Callback: client app'in custom scheme'ini intercept eden `LinbikCallbackActivity` (manifest `intent-filter`'ı build sırasında üretilir).
4. **iOS binding** (iosMain):
   - `UIApplication.openURL(deepLink)` ile App Switch tetikle.
   - Yüklü değilse OS otomatik fallback yapacak; SDK `canOpenURL` çağırmaz (App Store gizlilik manifest gereksinimi).
   - Callback: AppDelegate / SceneDelegate `application(_:open:options:)` → `LinbikSdk.handleCallback(uri)`.
5. **HTTP layer**: Ktor client (KMP-native). Tek bir `MobileAuthApiClient` her iki platformda paylaşılır.
6. **Build outputs**:
   - Android: `linbik-sdk-mobile-{version}.aar` + Maven publish (GitHub Packages veya MavenCentral).
   - iOS: `LinbikSdkMobile.xcframework` + Swift Package manifest.
7. **Sample integration test app**: `Linbik/src/Mobile/SampleClientApp/` (KMP sample); Linbik.Mobil olmadan build edilebilir, fallback web yolunu test eder.

### Faz 4 — Linbik.Mobil app (`ptts/src/Clients/Linbik.Mobil/`)
1. Compose Multiplatform skeleton (KMP shared UI). Linbik.Sdk.Mobile bağımlılığı YOK — bu uygulama auth provider tarafı.
2. **Android tarafı — caller verification**:
   - App, `https://auth.linbik.app/m?token=...` Universal Link'ini intent-filter ile intercept eder.
   - `Activity.callingActivity` veya `Activity.launchedFromPackage` (API 22+) ile çağıran package name'i al.
   - `PackageManager.getPackageInfo(packageName, GET_SIGNING_CERTIFICATES)` → `signingInfo.apkContentsSigners` → SHA-256 fingerprint hesapla.
   - `POST /api/oauth/mobile/verify-caller` ile sunucuya gönder (token + package + sha256). Yanıt geçersizse UI'da "Yetkisiz uygulama" hatası göster.
3. **iOS tarafı — caller verification**:
   - Universal Link intercept (Associated Domains entitlement). Bundle id alınamadığı için doğrulama redirect URI ve domain (AASA) üzerinden.
   - `POST /api/oauth/mobile/verify-caller` (token + redirectUri).
4. **Onboarding gate (zorunlu)** — verify-caller başarılı olduktan sonra ve consent UI'sından ÖNCE:
   - Yerel oturum yoksa → tam **login / register** akışına yönlendir (mevcut Linbik.Mobil giriş ekranları). Misafir/incognito mod yok.
   - Oturum var ama `currentUser.EmailConfirmed = false` ise → tam **e-posta doğrulama** akışına yönlendir (kod gönder + doğrula). Doğrulanana kadar consent ekranı açılmaz.
   - Bu adımlar sırasında kullanıcı uygulamadan çıkar / geri tuşuna basarsa → SDK callback'i `clientscheme://auth/cb?error=user_cancelled` ile döner; hiçbir `access_code` üretilmez.
   - Onboarding tamamlandıktan sonra orijinal `sessionToken` korunur ve consent ekranına geçilir (yeni initiate gerekmez).
5. **Login + consent UI**: Mevcut Nuxt web app'in login/consent ekranlarının native muadili. Onboarding gate'i geçmiş kullanıcıya consent doğrudan göster.
6. **Access code üret + callback**: Sunucuya `POST /api/oauth/mobile/issue-code` → tek kullanımlık `code` al → `Intent` (Android) / `UIApplication.open` (iOS) ile client app'in custom scheme callback URL'ine dön.

### Faz 5 — Doğrulama & Distribution
1. End-to-end test senaryoları (sample client app + Linbik.Mobil ile):
   - Happy path Android (Linbik.Mobil yüklü, oturum açık, mail doğrulu).
   - Happy path iOS (Linbik.Mobil yüklü, oturum açık, mail doğrulu).
   - Fallback Android (Linbik.Mobil yok → Custom Tabs / browser).
   - Fallback iOS (Linbik.Mobil yok → ASWebAuthenticationSession / Safari).
   - Onboarding gate Android: oturumsuz kullanıcı → register → mail verify → consent → callback.
   - Onboarding gate iOS: oturum açık ama mail doğrulanmamış → mail verify ekranı → consent → callback.
   - Onboarding iptal: kullanıcı login/verify ekranında geri tuşu → `error=user_cancelled` callback.
   - Sahte caller (yanlış SHA-256, yanlış bundle) → reddedilmeli.
   - Süresi dolmuş `sessionToken` → 401 + clear UX hata.
   - `code` replay attack → ikinci kullanımda reddedilmeli.
   - `issue-code` mail doğrulanmamış kullanıcıda 403 (server-side savunma).
2. Distribution:
   - SDK: GitHub Releases (initial) → MavenCentral (Android) + Swift Package Manager (iOS).
   - Linbik.Mobil: TestFlight + Google Play Internal Testing → public store.
3. Dokümantasyon: README + entegrasyon kılavuzu (`Linbik/src/Mobile/Linbik.Sdk.Mobile/README.md`) — sample Kotlin + Swift kod blokları, gerekli manifest/Info.plist girdileri, AASA/assetlinks.json örnekleri.

## Approaches Considered

### A) Tek shared KMP SDK + Linbik.JwtAuthManager'da iki endpoint (ÖNERİLEN)
- Tek kod tabanı, iki platforma çıktı. Mevcut `JwtAuthManager`'a minimum invaziv ekleme.
- Risk: KMP iOS XCFramework setup ekibe yeni; ilk build pipeline biraz öğrenme gerektirir.
- Effort: M.

### B) Native ayrı SDK'lar (Kotlin AAR + Swift Package, KMP yok) + aynı server tarafı
- KMP'siz, her iki SDK ayrı kod tabanı. Daha tanıdık her iki platformda.
- Maliyet: Aynı Ktor client / DTO'lar iki kez yazılır; iki test paketi; sürüm sapması riski.
- Effort: L (kod fazla, sürüm yönetimi ek yük).

### C) Linbik.Mobil'i atla, sadece "Sign in with Linbik" web (Custom Tabs / ASWebAuthSession)
- Native Linbik.Mobil app'inden vazgeç, tüm auth Custom Tabs üzerinden web ile.
- Kullanıcı isteğine ters (Translucent Activity / App Switch + caller package SHA-256 doğrulaması istiyor). Sadece referans olarak listelendi.
- Effort: S, ama ürün vizyonunu karşılamıyor.

**RECOMMENDATION: A** — KMP tek SDK; mevcut OAuth altyapısını yeniden kullanır, kullanıcının istediği native handoff + caller doğrulamasını destekler, kod tekrarını minimize eder.

## Relevant Files

- `Linbik/src/AspNet/Linbik.JwtAuthManager/Extensions/JwtAuthManagerExtensions.cs` — `SetAuthCookies` reuse; mobile callback'ten sonra cookie kurmak için.
- `Linbik/src/AspNet/Linbik.JwtAuthManager/Extensions/LinbikJwtAuthExtensions.cs` — `MobileSessionStore` ve `LinbikMobileAuthController` DI registration.
- `Linbik/src/AspNet/Linbik.Core/Services/LinbikAuthClient.cs` — `ExchangeCodeAsync` imzasına opsiyonel `code_verifier` ekle; `InitiateAsync`'a `flowType` ekle.
- `Linbik/src/AspNet/Linbik.Core/Models/LinbikModels.cs` — `LinbikInitiateRequest`, `LinbikTokenRequest` DTO'larına yeni alanlar.
- `ptts/src/Services/Linbik.Service/Linbik.Api/Controllers/OAuthController.cs` — yeni `mobile/verify-caller` ve `mobile/issue-code` endpoint'leri; `initiate` ve `token` mevcut metodlarına minimal değişiklik; `issue-code` içinde `EmailConfirmed` zorlaması.
- `ptts/src/Services/Linbik.Service/Linbik.Api/Migrations/` — yeni migration: `clients` tablosu kolonları.
- `Linbik/src/Mobile/Linbik.Sdk.Mobile/` — **YENİ KMP project**; `commonMain`, `androidMain`, `iosMain`.
- `ptts/src/Clients/Linbik.Mobil/` — **YENİ Compose Multiplatform app**; mevcut Nuxt login/consent UI'sının native muadili + onboarding gate.
- `Linbik/examples/nuxt/app/pages/login.vue` — referans olarak kullanılacak (web fallback akışı bunun üzerinden gider).

## Verification

1. **Server unit tests**: `MobileSessionStore` TTL davranışı, verify-caller endpoint sahte SHA-256 reddi, code replay reddi, `issue-code` mail-doğrulanmamış kullanıcı reddi.
2. **SDK instrumented tests** (Android): Translucent Activity launcher → fake intent dönüşü → `LinbikSdk.handleCallback` doğru parse ediyor mu.
3. **SDK XCTest** (iOS): Universal Link açma → AppDelegate callback → `handleCallback` flow.
4. **End-to-end manuel**: Sample client app → Linbik.Mobil → token return; Linbik.Mobil silinmiş cihazda fallback → Custom Tabs / Safari; oturumsuz / mail-doğrulanmamış kullanıcı için onboarding gate.
5. **Security check**: Sahte SHA-256'yla istek (Android), yanlış bundle id (iOS) → 403; süresi geçmiş `sessionToken` → 401; replay edilen `code` → 400; mail doğrulanmamış kullanıcıdan `issue-code` çağrısı → 403.
6. **Build verification**: `./gradlew :linbik-sdk-mobile:assembleRelease` → AAR; `./gradlew :linbik-sdk-mobile:linkReleaseFrameworkIosArm64` → XCFramework.

## Decisions

- **PKCE verifier yeri**: 3rd-party backend (Linbik.JwtAuthManager içindeki `MobileSessionStore`) — telde gitmez, Linbik.Server sadece `code_challenge` görür.
- **App-not-installed fallback**: Universal Link / App Link "app-first" — OS karar verir, SDK manuel `canOpenURL`/package query yapmaz.
- **Repo layout**: SDK Linbik repo'sunda (`Linbik/src/Mobile/Linbik.Sdk.Mobile/`); Linbik.Mobil ptts repo'sunda (`ptts/src/Clients/Linbik.Mobil/`).
- **Mobil teknoloji**: Hem SDK hem Linbik.Mobil app KMP / Compose Multiplatform.
- **Caller doğrulama**: Android = package + SHA-256 (DB'de saklı), iOS = bundle id + redirect URI + Universal Link domain (DB'de saklı). Doğrulama Linbik.Mobil app'i içinden değil, server'a `verify-caller` çağrısı ile (DB merkezi tutulur, app rolü thin).
- **Onboarding gate**: Linbik.Mobil oturumsuz veya mail-doğrulanmamış kullanıcıyı consent ekranına almaz; tam login/register + mail verify akışından geçirir. Misafir/incognito mod yok. Server-side `issue-code` endpoint'i de aynı kuralı zorunlu kılar (defense in depth).

**Kapsam dışı**: 
- App Attest (DeviceCheck) entegrasyonu — V2'de değerlendirilebilir, ilk sürümde redirect URI + bundle white-list yeterli.
- Refresh token'ın mobile app'te saklanması — bu akış sadece auth code → token; refresh stratejisi 3rd-party backend'in sorumluluğunda (mevcut `JwtAuthManager` zaten yönetiyor).
- Multi-account UX — kullanıcı Linbik.Mobil'de tek hesapla giriş varsayılır.

## Further Considerations

1. **`MobileSessionStore` için Redis zorunluluğu mu opsiyonel mi?** — Production scale-out senaryolarında IMemoryCache yeterli değil. Öneri: default IMemoryCache, doc'ta production için Redis öner.
2. **`access_code` TTL'i kaç saniye?** — Öneri: 60 saniye, tek kullanım. (Mevcut `AuthorizationCode.IsValid` davranışıyla uyumlu).
3. **Onboarding sırasında `sessionToken` TTL'i tükenirse?** — Kullanıcı uzun süre register/mail verify akışında kalırsa initiate sırasında verilen 5 dk TTL aşılabilir. Öneri: onboarding gate'i geçtikten sonra Linbik.Mobil sunucudan `sessionToken`'ı yeniler (yeni endpoint: `POST /api/oauth/mobile/refresh-session`) veya TTL onboarding-bound senaryoda 30 dk'ya çıkarılır. Karar bekliyor.
4. **Mail doğrulama linki uygulamadan çıkış gerektirirse?** — Mail içindeki linke tıklamak Linbik.Mobil'i tekrar açmalı (Universal Link), kaldığı `sessionToken` ile akışa devam etmeli. Deep-link state restoration testte doğrulanmalı.
