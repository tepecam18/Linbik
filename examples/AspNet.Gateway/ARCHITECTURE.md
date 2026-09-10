# Linbik Gateway Mimarisi

Bu doküman, `examples/AspNet.Gateway` referans uygulamasının çalışma şeklini,
güvenlik invariantlarını ve mevcut kararları tek noktada toplar. Kod ve
appsettings güncellendiğinde bu dosyayı da güncelleyin — "tek doğruluk
kaynağı" yaklaşımı.

---

## 1. Genel akış: üç flow

Gateway, downstream servisleri tek bir yüzeyden 3 farklı **flow** ile sunar.

| Flow          | Path öneki                | Auth şeması                         | Token taşıyıcı                  | Kullanım amacı                                       |
| ------------- | ------------------------- | ----------------------------------- | -------------------------------- | ---------------------------------------------------- |
| `Self`        | `/{service}/...`          | `LinbikAuthorize` (cookie)          | `authToken` çerezi               | Kullanıcının kendi adına çağrı (web tarayıcı)        |
| `Delegated`   | `/delegated/{service}/...`| `LinbikDelegatedAuthorize` (bearer) | `Authorization: Bearer <PASETO>` | Üçüncü taraf app, kullanıcı adına                    |
| `Application` | `/apps/{service}/...`     | `LinbikApplicationAuthorize`        | `Authorization: Bearer <PASETO>` | Server-to-Server, kullanıcı yok                      |

YARP rotaları, downstream `Address`'e ulaşmadan önce `PathPattern`
`/api/{service}/{**catch-all}` transformasyonunu uygular. Yani downstream
servisler her zaman `/api/...` görür; flow'a göre URL yeniden yazılmaz.

`Linbik-Flow` header'ı normalde gelen istekten **koşulsuz silinir** (header
sanitization middleware), sonra YARP route metadata'sından gateway tarafından
yazılır — istemci spoof'u bu tasarımda imkansız olmalıdır.

> ⚠️ **Bilinen kısıt:** `ApiGateway/Program.cs`'te
> `app.UseMiddleware<LinbikHeaderSanitizationMiddleware>();` satırı şu an
> **yorum satırı** (devre dışı). Yani bu örnek uygulamada gelen isteğin
> `Linbik-Flow`/`Linbik-*` header'ları **şu anda silinmiyor** — spoofing
> koruması aktif değil. Bkz. §9 ve §10.

## 2. Servis-otorite invariantı

> **`[LFlowAuthorize]` servis tarafında authoritative'dir. Gateway yalnızca
> kullanıcı deneyimi (ön-kapısı) ve dokümantasyon için flow bilgisini taşır.**

Yani gateway hatalı flow router (örn. test ortamında yanlış konfig) yaparsa
veya bir servisi bypass eden ikinci bir yol açılırsa bile, servis kendi
kontrolünü kendi yapar:

- `[LFlowAuthorize]` parametresiz → "herhangi bir authenticated flow".
- `[LFlowAuthorize(Flows.Self)]` → yalnız `Linbik-Flow: Self`.
- `[LFlowAuthorize(Flows.Self, Flows.Delegated)]` → bu ikisinden biri.

> **Önemli kısıt:** `[LFlowAuthorize]` / `[LFlow]`'un "servis kendi kontrolünü
> yapar" iddiası yalnız header'ın **formatını** doğrular, header'ın **gerçekten**
> Gateway tarafından authenticate edilmiş bir istekten geldiğini değil. Servis,
> Gateway olmadan doğrudan erişilebilirse (network izolasyonu yoksa) herhangi
> bir istemci `Linbik-Flow: Application` header'ı göndererek bu kontrolü
> tamamen atlatabilir. Bu kontrol yalnız (a) Gateway'in istemciden gelen
> `Linbik-*` header'larını sildiği ve (b) servisin ağ seviyesinde yalnız
> Gateway'den erişilebilir olduğu varsayımı altında güvenlidir. Gateway
> olmayan mimariler (servisin doğrudan erişilebilir olduğu durumlar) için
> `Linbik.Slices`'taki `[LAuthorizeFlow(...)]` kullanılmalı — bu, header'a
> güvenmek yerine gerçek authentication scheme'ini (`RequireAuthorization`)
> doğrular (bkz. `Linbik.Slices/README.md`).

Hardening kuralları (`Linbik.Core/Attributes/LFlowAuthorizeAttribute.cs`):

- `Linbik-Flow` header'ı **0 veya 1** değer içermelidir; `Count > 1` → 401.
- Boş/whitespace → 401.
- `,`, `;`, boşluk, `\t`, `\r`, `\n` içeren → 401 (CSV/inject korunması).
- `AllowedFlows` dolu ve eşleşme yok → 403.
- Tüm hatalarda `LBaseResponse<object>` ile yapılandırılmış cevap döner.
- `AllowMultiple = false`, `Inherited = true` — class + method aynı anda
  kullanıldığında method kazanır (tek attribute kuralı).

## 3. `linbik-flows` OpenAPI extension

`Linbik.Core/OpenApi/LFlowOpenApiOperationTransformer.cs`, `MapOpenApi()`'nin
ürettiği dokümana her operation için **`linbik-flows`** uzantısını ekler:

| Senaryo                                               | Emit                  |
| ----------------------------------------------------- | --------------------- |
| Attribute yok                                         | `["*"]` (anonim)      |
| `[LFlowAuthorize]` parametresiz                       | `["authenticated"]`   |
| `[LFlowAuthorize(Flows.Self, Flows.Delegated)]`       | `["Self","Delegated"]`|

Notlar:

- OpenAPI 3.x spec'i tüm vendor uzantıları için `x-` prefiksini **zorunlu**
  kılar. `Linbik-` ile başlayan başka HTTP header / claim isimleri kullanıyor
  olsak da, OpenAPI ext key'i kuralı gereği `linbik-flows` formundadır.
- Bu uzantı, gateway'in filtreleme kararlarındaki **tek doğruluk kaynağıdır**.
  Geçmişte hayata geçirilmiş `/.well-known/linbik/flow-manifest` endpoint'i ve
  ilgili `LinbikFlowManifestExtensions` sınıfı kaldırılmıştır.
- Eksik (`linbik-flows` yok) operation'lar gateway tarafında **fail-closed**
  davranır: ne self/delegated/apps dokümanına ne de UI'a düşer.

## 4. Gateway OpenAPI aggregator

`ApiGateway/Docs/OpenApiAggregatorService.cs` arka plan servisi:

- Konfigürasyon: `LinbikGateway:Sources` (her source `ServicePrefix` + `Address`
  + opsiyonel `DisplayName` içerir).
- `RefreshIntervalSeconds` (varsayılan 60) aralıkla her downstream'in
  `{Address}{DownstreamOpenApiPath}` adresinden OpenAPI dokümanını çeker.
- **ETag** ile koşullu GET (`If-None-Match`); 304 alındığında cache korunur.
- **ETag TTL** (`EtagMaxAgeSeconds`, varsayılan 600): son 200 OK'tan bu kadar
  saniye geçtiyse `If-None-Match` gönderilmez ve tam fetch yapılır. Amaç:
  downstream ETag üretimi bozulsa veya sahte/stale ETag dönse bile cache'in
  süresiz olarak kalıcılaşmaması.
- **Fail-open**: fetch hata verirse önceki snapshot korunur; istemciye eski
  doküman servis edilir.
- **Lazy first-load**: ilk istek geldiğinde cache boşsa `SemaphoreSlim` ile
  serileştirilen tek-seferlik tetikleme yapılır; gateway başlangıcı yavaş
  downstream nedeniyle bloke olmaz.

Cache (`OpenApiCache`) `ServicePrefix` ile anahtarlanır; filtered doc cache'i
her `Upsert`'te invalidate edilir.

## 5. Filtrelenmiş dokümanlar + Scalar UI

`ApiGateway/Docs/FilteredDocumentBuilder.cs` her snapshot dokümanı taranır:

- Yol önekini `/{prefix}` (Self), `/delegated/{prefix}` veya `/apps/{prefix}`
  olarak yeniden yazar.
- `linbik-flows` yok → **fail-closed** (operation drop).
- Convention dışı path (`/api/{prefix}` öneki taşımayan) → drop.
- Aktif flow listede yoksa operation drop; tüm operation'ları drop edilen path
  drop edilir.
- Şema/component birleştirir; çakışma olduğunda `{prefix}_{name}` ile yeniden
  adlandırır.

Endpoint'ler (`LinbikGatewayExtensions.cs`):

| Endpoint                    | Self erişimi                                 | Dev davranışı (delegated/apps) | Prod davranışı (delegated/apps)                          |
| --------------------------- | --------------------------------------------- | -------------------------------- | ------------------------------------------------------- |
| `/openapi/self.json`        | `Docs:SelfAccess` kuralına göre (bkz. aşağı)  | —                                 | —                                                         |
| `/openapi/delegated.json`   | —                                              | anonim                           | `LinbikSelfOrApplicationAuthorize` (cookie **veya** Application bearer) |
| `/openapi/apps.json`        | —                                              | anonim                           | `LinbikSelfOrApplicationAuthorize` (cookie **veya** Application bearer) |
| `/docs/self`                | `Docs:SelfAccess` kuralına göre (bkz. aşağı)  | —                                 | —                                                         |
| `/docs/delegated`           | —                                              | anonim                           | `LinbikAuthorize` (cookie) — UI sayfası                  |
| `/docs/apps`                | —                                              | anonim                           | `LinbikAuthorize` (cookie) — UI sayfası                  |

`/openapi/self.json` ve `/docs/self`, `LinbikGateway:Docs:SelfAccess` (string)
konfigürasyonuyla yönetilir — ortamdan (Dev/Prod) bağımsız, tek bir kuraldır
(`LinbikDocsAuthOptions.IsSelfAccessAllowed`, karşılaştırmalar
`OrdinalIgnoreCase`):

| `SelfAccess` değeri         | Anlamı                                                          |
| ----------------------------- | ------------------------------------------------------------------ |
| boş / yok (**varsayılan**)   | **kapalı** — kimse erişemez                                      |
| `"anonim"`                   | herkese açık, oturum gerekmez                                    |
| `"*"`                         | oturum açmış (cookie ile authenticate) herhangi bir kullanıcı    |
| `"ali,veli,mehmet"`          | virgülle ayrılmış kullanıcı adı allow-list'i (yalnız bunlar)     |

Kullanıcı adı, JWT'deki `preferred_username` claim'inden okunur (fallback:
`Identity.Name` → `sub` claim). Bu repo'da `appsettings.json` varsayılanı boş
(kapalı, Prod-safe); `appsettings.Development.json` bunu `"anonim"` ile
override ederek eski Dev deneyimini korur.

> `LinbikGateway:Docs:RequireAuthInDevelopment=true` yalnızca **delegated/apps**
> doc endpoint'lerini etkiler — Dev'de de Prod davranışına geçirir. `self.json`/
> `/docs/self` bu flag'den etkilenmez, yalnızca `SelfAccess` tarafından yönetilir.

Karar mantığı: **JSON endpoint'leri programmatic consumer'lar (curl, codegen, CI) için
standart Authorization bearer ister** — `Linbik.YARP` token transform pattern'iyle birebir.
UI sayfaları browser tabanlı olduğu için cookie ile açılır; yetkisiz ziyaretçi
`/api/linbik/login`'e yönlendirilir. UI'ın alttaki JSON'u çağırması için kullanıcı,
Scalar'ın "Authentication" panelinden bearer token girer. (Cookie + bearer hibriti.)

Self için de aynı cookie scheme'i (`ClientScheme`) tetiklenir: authenticate
olmayan ziyaretçi login'e yönlendirilir (challenge), authenticate olmuş ama
kural reddedeni (allow-list'te değil veya mod "kapalı") 403 alır (forbid).
"Kapalı" modda anonim ziyaretçi de bu yüzden önce login'e yönlendirilip sonra
403 alır — bilinçli bir tasarım kararı, özel bir "erken 403" kısayolu yoktur.

Audit: doc isteklerinde `Information` seviyesinde `flow`, `user` (Name /
`sub` claim / `(anon)`), `ip`, `path` loglanır.

## 6. YARP otomatik konfigürasyonu

Eskiden `appsettings.json` içinde her source için 3 route + 1 cluster elle
yazılıyordu. Artık `LinbikGateway:Sources` tek girişten **3 route + 1 cluster**
üretilir (`ApiGateway/Docs/LinbikRouteBuilder.cs`):

- Cluster: `{prefix}-cluster` → tek destination `{prefix}` @ `Address`.
- Route'lar (her biri **optional** policy + `Linbik-Flow` metadata + PathPattern):
  - `self-{prefix}` → `/{prefix}/{**catch-all}` (policy `LinbikSelfOptional` — cookie scheme)
  - `delegated-{prefix}` → `/delegated/{prefix}/{**catch-all}` (policy `LinbikDelegatedOptional` — PASETO bearer)
  - `apps-{prefix}` → `/apps/{prefix}/{**catch-all}` (policy `LinbikApplicationOptional` — PASETO bearer)

**Optional policy** ne demek? İlgili auth scheme'i çalıştırır (geçerli kimlik
varsa `ClaimsPrincipal` doldurulur, transform `Linbik-{Claim}` header'larını
yazar), ama **anonim isteği reddetmez** (`RequireAssertion(_ => true)`). Aslında
bu, "servis-otoritesi" invariantının gereği: `linbik-flows: ["*"]` ile işaretli
anonim op'lar (örn. `/arithmetic/add`) gateway tarafında reddedilmemeli. Anonim
istek gateway'i geçer, ancak `Linbik-Flow` header'ı transform tarafından
yazılmaz — servis tarafındaki `[LFlowAuthorize]` attribute'lu op'lar header
eksikliğinden 401 döner. Attribute taşımayan anonim op'lar düzgün geçer.

YARP `LoadFromMemory` ile çalışır. `appsettings.json` içindeki eski
`ReverseProxy` bölümü **kaldırılmıştır**. Yeni servis eklemek için yalnızca
`LinbikGateway:Sources` listesine bir giriş eklemek yeterli.

### Gateway'in kendisi bir kaynak

`LinbikGatewaySource.IsGateway = true` bayrağı işaretli kaynak gateway'in
kendi controller'larını (örn. `Linbik.PasetoAuthManager`'ın `/api/linbik/login`,
`/api/linbik/callback`, `/api/linbik/refresh`, `/api/linbik/logout`) doc'a
dahil etmek için kullanılır:

- `LinbikRouteBuilder` bu kaynak için route üretmez (gateway zaten kendi
  endpoint'lerine serves eder).
- `FilteredDocumentBuilder` bu kaynağın path'lerini **oldukları gibi** self
  doc'a koyar (rewrite yok) — dolayısıyla `/api/linbik/login` self doc'ta
  aynı path ile görünür. Delegated/apps doc'larında yer almaz.
- Gateway'in `Program.cs`'i `AddOpenApi(opt => opt.AddLinbikFlowExtension())`
  çağrır; böylece gateway'in kendi `/openapi/v1.json`'ı da `linbik-flows`
  uzantısı ile etiketlenir (PasetoAuthManager endpoint'lerinde `[LFlowAuthorize]`
  bulunmadığı için `["*"]` emit edilir, anonim olarak self doc'a düşer).

## 7. Auth & header transformları

- Self: `Linbik.PasetoAuthManager` cookie scheme'i (`authToken`).
  `LinbikAuthorize` policy'si bu scheme'i ister.
- Delegated/Application: `LinbikGatewayAuthExtensions.AddLinbikGatewayAuth()`
  iki PASETO bearer scheme kayıtlar (`LinbikDelegated`, `LinbikApplication`).
  Application scheme `applicationToken` claim'inin varlığını ister.
- YARP route'ları için **optional** karşılıklar (`LinbikSelfOptional`,
  `LinbikDelegatedOptional`, `LinbikApplicationOptional`): aynı scheme'leri
  tetikler ama anonim isteği reddetmez — service-authoritative invariant.
- Tüm flow'larda `LinbikClaimsHeaderTransform`:
  - **Tüm `Authorization` ve `Cookie` header'larını upstream'e geçmeden
    siler** (downstream'ler token görmez).
  - `ClaimsPrincipal`'dan downstream'e `Linbik-{ClaimType}` header'ları yazar
    (yalnız authenticated istekler için).
  - `Linbik-Flow` header'ını **yalnız authenticated** istekler için YARP route
    metadata'sndan doldurur. Anonim isteklere flow header inject etmez — bu sayede
    `[LFlowAuthorize]` taşıyan downstream op'lar anonim isteği 401'le reddeder,
    `["*"]` op'lar header eksikliğini sorun yapmadan geçer.

Tasarımda önce bir sanitization middleware çalışır: `LinbikHeaderSanitizationMiddleware`
(routing'den önce) gelen istekteki bütün `Linbik-*` ve `Linbik-Flow` header'larını
siler, sonra routing → auth → transform sırası gelir. **Bu örnekte şu an devre
dışı** (`ApiGateway/Program.cs`'te yorum satırı — bkz. §1, §9, §10); yeniden
etkinleştirilmeden bu adım fiilen atlanır.

## 8. `[JsonIgnore]` disiplini

Gateway üzerinden downstream servisten gelen modeller doğrudan istemciye
yansıyabildiğinden, **public modellerde hassas alanlar `[JsonIgnore]` ile
işaretlenmelidir.** Örnekler:

- Dahili anahtarlar: `InternalCustomerId`, `TenantSecret`
- Kimlik bilgileri: `PasswordHash`, `PasetoSharedKey`, `RefreshTokenHash`
- Audit/sistem alanları: `CreatedByIp`, `LastLoginIp` (eğer doğrudan
  istemciye dönmesi tasarımsal değilse)
- Pure internal: `RowVersion`, `ConcurrencyToken`

Kural:

> **Public DTO oluştururken, her alana "bu istemciye gitsin mi?" diye sor.
> Şüphe varsa `[JsonIgnore]`. İstisnayı belgelemek, yanlışlıkla sızdırmaktan
> daha ucuzdur.**

OpenAPI doc'ları da bu modellere referans verdiğinden, `[JsonIgnore]`
filtreleme hem JSON cevabını hem de doc şemasını korur.

## 9. Güvenlik analizi

| Tehdit                                          | Mitigation                                                                                                                 |
| ----------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Header spoofing (`Linbik-Flow` enjeksiyon)      | Tasarım: routing öncesi sanitization + route metadata'sından yazma. **Bu örnekte şu an mitigasyon devre dışı** — bkz. §10. |
| Authorization sızıntısı downstream'e            | `LinbikClaimsHeaderTransform` `Authorization` + `Cookie` header'larını siler.                                              |
| `linbik-flows` yokluğu                        | Fail-closed: operation drop. Bilinçsizce expose etmek imkansız.                                                            |
| Bayat / poison ETag                             | `EtagMaxAgeSeconds` (600 sn) sonrası full fetch.                                                                           |
| Downstream OpenAPI anonim                       | Network segmentasyonu varsayımı: downstream'ler **yalnızca gateway'den** erişilebilir olmalı. Production'da firewall/VPC.  |
| Doc UI üzerinden bilgi sızıntısı                | `delegated`/`apps` doc + UI **cookie auth zorunlu**. Self varsayılan **kapalı**, `Docs:SelfAccess` ile açılır.             |
| Convention dışı path drop'larının görünmezliği  | Şimdilik sessizce drop; ileride `Debug` log eklenebilir (TODO).                                                            |
| `Microsoft.OpenApi` namespace kırılması (2.x)   | Yalnız `using Microsoft.OpenApi;` kullanılır (`Microsoft.OpenApi.Models` namespace'i 2.x'te yok).                          |

## 10. Bilinen kısıtlar / yapılacaklar

- **⚠️ Header sanitization middleware şu an devre dışı.**
  `ApiGateway/Program.cs`'teki `app.UseMiddleware<LinbikHeaderSanitizationMiddleware>();`
  satırı yorumda. Bu örnek uygulamada gelen isteğin `Linbik-*`/`Linbik-Flow`
  header'ları **silinmiyor** — §1 ve §9'daki "spoofing imkansız" tasarım hedefi
  bu haliyle **sağlanmıyor**. Roadmap: bu satırı tekrar aktif edip
  (a) network izolasyonu olmayan ortamlarda servis-otorite invariantının
  gerçekten korunduğunu, (b) mevcut testlerin/örneklerin middleware aktifken de
  çalıştığını doğrulamak.
- **Downstream OpenAPI endpoint'leri auth'suz.** Production'da bunların
  internet'e açılmaması, ya da downstream üzerinde de PASETO auth'a
  alınması beklenir. Şu an "trusted internal network" varsayımı.
- **Convention dışı path drop'ları sessizdir.** Aggregator'a `Debug` log
  eklenecek (TODO).
- **Manifest endpoint'i kaldırıldı.** OpenAPI extension tek source of truth.
  Eski `MapLinbikFlowManifest()` ve `LinbikFlowManifestExtensions` artık
  yok. Konfig kafa karışıklığını azaltmak için bilinçli karar.
- **Gateway preflight enforcement (`linbik-flows`'a göre gateway'de
  pre-check) kapsam dışı.** Servis-otorite invariantı bunu redundant yapar.

---

## Hızlı referans

- Yeni servis eklemek: `appsettings.json` →
  `"LinbikGateway:Sources"` listesine `{ "ServicePrefix": "x", "Address":
  "http://localhost:PORT/" }` ekle. YARP route + cluster + 3 flow otomatik.
- Yeni operation'ı bir flow'a kapatmak: controller action'a
  `[LFlowAuthorize(Flows.Self)]` yaz. Aggregator bir sonraki cycle'da
  yeni `linbik-flows` ile günceller; cache 60s + ETag TTL içinde
  görünür.
- Doküman/UI manuel refresh: 60 sn beklemek istemiyorsanız aggregator'ı
  yeniden başlatmak yeterli (BackgroundService restart).
