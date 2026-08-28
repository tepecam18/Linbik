# Tasarım: Linbik Mikroservis — Vertical Slice + 3 Katman Konvansiyonları

Üretildi: /office-hours · Tarih: 2026-05-31
Repo: tepecam18/Messtick · Branch: master
Durum: TASLAK
Mod: Builder (intrapreneurship / iç platform)

> Bu doküman **taşınabilir** olacak şekilde yazıldı. Bu workspace asıl projenin bir
> kopyası; `Linbik.*` paketlerinin kaynağı asıl projede. Konvansiyonları ve planı asıl
> projede uygula. Kod örnekleri **illüstratiftir** (paterni gösterir), birebir API
> imzaları asıl `Linbik.Core` ile hizalanmalıdır.

---

## Problem Tanımı

Messtick.Api altında YARP tabanlı `ApiGateway` + örnek mikroservisler (Arithmetic,
Aggregation) var. Servisler şu an **klasik controller + record Request/Response +
`LBaseResponse<T>`** kullanıyor. İki şey netleştirilmek isteniyor:

1. **3 katmanın (Self / Delegated / Apps) ne zaman, nasıl kullanılacağına dair kurallar.**
2. Servis içine **CQRS benzeri, Request + Response + Handler'ın tek dosyada olduğu**
   bir vertical-slice paterni eklemek.

## 3 Katman (auth flow) Modeli — Mevcut Durum

| Flow | Anahtar / Taşıyıcı | Kim kullanır | Gateway doc |
|---|---|---|---|
| **Self** | PASETO, Local SharedKey, **cookie** | Mobil + Web (first-party) | `/openapi/self.json`, `/docs/self` |
| **Delegated** | Ed25519 public key, **Bearer** (kullanıcı token'ı) | Third-party'nin **kullanıcı adına** eriştiği akışlar | `/openapi/delegated.json` |
| **Application** (Apps) | Ed25519 public key, **Bearer** (S2S token) | Third-party **entegrasyon yazılımları** (kullanıcı yok) | `/openapi/apps.json` |

- Endpoint, hangi katmana açık olduğunu downstream serviste `[LFlowAuthorize(...)]`
  ile işaretler. **Asıl güvenlik kapısı downstream servistir**; gateway sadece OpenAPI
  filtreleme + claim→header taşıma + `Linbik-*` header sanitizasyonu yapar.
- Gateway `linbik-flows` OpenAPI extension'ını `AddLinbikFlowExtension()` ile üretir;
  şu an bu **controller action metadata'sı** üzerinden çalışıyor.

### Katman kullanım kuralları (governance)

1. **Self = first-party.** Yalnız kendi mobil/web istemcilerinin ihtiyacı olan
   endpoint'ler. Cookie tabanlı olduğu için CSRF yüzeyine dikkat (gateway CORS allow-list'i
   bu yüzden dar tutulmalı).
2. **Delegated = third-party + kullanıcı bağlamı.** Bir kullanıcının verisine üçüncü
   parti uygulama adına erişim. Kullanıcı claim'i her zaman mevcut olmalı; handler
   `Linbik-*` header'larından kullanıcı bağlamını okur.
3. **Application = third-party + kullanıcı yok (S2S).** Makine-makine. Kullanıcıya özel
   veri **dönmemeli**; sadece uygulama düzeyi (tenant/app) bağlamı.
4. Bir endpoint **birden çok flow'a** açılabilir (örn. `Self + Application`). Açık liste
   ver, "hepsi" demek için bile bilinçli ol.
5. **Deny-by-default** (aşağıda P2). Public bir endpoint istiyorsan **açıkça** işaretle.

---

## Kararlar (bu oturumda alındı)

| # | Konu | Karar | Neden |
|---|---|---|---|
| K1 | Dispatch mekanizması | **Kendi hafif dispatcher** (`ILinbikSender`) | Sıfır bağımlılık, MediatR lisans riski yok, Linbik konvansiyonlarına tam oturur |
| K2 | Endpoint yeri | **Minimal API, slice dosyasının içinde** | Saf vertical-slice; her şey tek dosyada (⚠️ P1 doğrulanmalı) |
| K3 | Varsayılan katman | **Deny-by-default** | Unutulan attribute kazara public endpoint açmasın |
| K4 | Klasör yapısı | `Features/{Feature}/{Operation}.cs` | İş alanına göre grupl, en yaygın slice düzeni |
| K5 | Validation + hata | **Pipeline + `Result<T>` → `LBaseResponse` mapper** | Tekrarsız, saf handler, kolay test |
| K6 | Hayata geçirme | **Source generator + analyzer** (otomasyon) | En az tekrar; slice deklaratif, altyapı üretilir |

---

## Onaylanmış Varsayımlar (Premises)

- **P1 — Minimal API + flow extension DOĞRULANMALI (spike).** `AddLinbikFlowExtension`
  şu an controller metadata'sından `linbik-flows` üretiyor. Minimal API endpoint'lerinde
  aynı extension'ın flow metadata'sını OpenAPI'ye yazdığı **kanıtlanmadan** tüm servisleri
  çevirmek riskli. → **Adım 0: tek endpoint spike.**
- **P2 — Deny-by-default bir zorlayıcı mekanizma ister.** `Linbik.Core`, attribute yoksa
  `"*"` (public) üretiyor ve değiştiremiyoruz. Güvenli varsayılan için **Roslyn analyzer**
  (build'i kıran) + açık `Public` işareti konvansiyonu gerekir.
- **P3 — Altyapı bir iç kütüphanede paketlenmeli** (`Linbik.Slices`) ki tüm servisler
  aynı `ILinbikSender` / `Result<T>` / mapper'ı paylaşsın, kopya-yapıştır olmasın.
- **P4 — `LBaseResponse<T>` zarfı korunur.** Mapper `Result`→`LBaseResponse` çevirir;
  mevcut mobil/web istemcileri bozulmaz.
- **P5 — Önce 1 örnek servis referans olarak dönüştürülür**, diğeri eski stilde kalıp
  kıyas örneği olur.

---

## Yaklaşımlar (değerlendirildi)

### Approach A — Minimal (önce kanıtla)
- **Özet:** Doküman + tek endpoint spike + yalın `ILinbikSender`/`Result<T>` + 1 feature dönüşümü.
- **Efor:** S/M · **Risk:** Düşük
- **Artı:** Hızlı ışık, P1 riski erken görülür, geri dönüşü kolay.
- **Eksi:** Otomasyon yok; tekrar elle yazılır, ölçeklenince yorucu.

### Approach B — İdeal altyapı
- **Özet:** `Linbik.Slices` kütüphanesi (`ILinbikSender`, validation behavior, `Result<T>`,
  `Result→LBaseResponse` mapper, deny-by-default analyzer, `dotnet new` template).
- **Efor:** L · **Risk:** Orta
- **Artı:** Sağlam, paylaşımlı, tutarlı; analyzer güvenliği garanti eder.
- **Eksi:** Önden yatırım yüksek; değer ilk slice'tan sonra gelir.

### Approach C — Yaratıcı otomasyon (SEÇİLEN)
- **Özet:** Slice'ı **source generator** ile endpoint mapping + DI kaydı + flow metadata'ya
  dönüştür; **analyzer** konvansiyonları build'de zorlar. Slice tamamen deklaratif.
- **Efor:** XL · **Risk:** Orta-Yüksek
- **Artı:** En az tekrar; geliştirici sadece Request/Response/Handler/Validator + flow
  attribute yazar, gerisi üretilir. Deny-by-default ve isim/şablon kuralları derleme
  zamanında zorlanır.
- **Eksi:** En yüksek karmaşıklık ve bakım yükü; source-gen debugging zor; P1 riski
  source-gen'in ürettiği endpoint metadata'sının flow extension ile uyumuna bağlı.

**SEÇİLEN: Approach C** — uzun vadede en az tekrar, en güçlü zorlama. Ancak **Adım 0
spike (P1)** otomasyon yazılmadan önce yapılmalı; spike olumsuzsa K2 (minimal API)
"ince controller + slice" fallback'ine döner.

---

## Önerilen Slice Anatomisi (illüstratif)

`ArithmeticService/Features/Calculations/Divide.cs`:

```csharp
namespace ArithmeticService.Features.Calculations;

[LinbikSlice]                                  // source-gen: endpoint + DI üretir
[LFlow(LinbikFlow.Self,                        // analyzer: deny-by-default zorlar
       LinbikFlow.Application)]
public static partial class Divide
{
    public sealed record Request(double A, double B) : ILinbikRequest<Response>;

    public sealed record Response(double Result);

    public sealed class Validator : ILinbikValidator<Request>
    {
        public ValueTask<LValidationResult> ValidateAsync(Request r, CancellationToken ct)
            => LValidation.For(r)
                .Ensure(x => x.B != 0, field: "B", message: "Bölen sıfır olamaz.")
                .BuildAsync();
    }

    public sealed class Handler : ILinbikHandler<Request, Response>
    {
        public ValueTask<Result<Response>> HandleAsync(Request req, CancellationToken ct)
            => Result.Ok(new Response(req.A / req.B)).AsValueTask();
    }

    // Endpoint mapping + flow metadata: source generator tarafından üretilir.
    // Üretilen kod kabaca:
    //   app.MapPost("/api/arithmetic/divide", LinbikEndpoint.Handle<Divide.Request, Divide.Response>)
    //      .WithFlows(Self, Application)        // <-- AddLinbikFlowExtension bunu okumalı (P1)
    //      .WithTags("Arithmetic");
}
```

### Çekirdek soyutlamalar (`Linbik.Slices` içinde)

```csharp
public interface ILinbikRequest<TResponse>;

public interface ILinbikHandler<TRequest, TResponse>
    where TRequest : ILinbikRequest<TResponse>
{
    ValueTask<Result<TResponse>> HandleAsync(TRequest request, CancellationToken ct);
}

public interface ILinbikValidator<TRequest>
{
    ValueTask<LValidationResult> ValidateAsync(TRequest request, CancellationToken ct);
}

public interface ILinbikSender
{
    ValueTask<Result<TResponse>> Send<TResponse>(
        ILinbikRequest<TResponse> request, CancellationToken ct = default);
}
```

### Pipeline (sender içinde)

1. İlgili `ILinbikValidator<TRequest>` çözülür → çalışır.
   - Geçersizse: handler **çağrılmaz**, `Result.Fail(validationError)` döner.
2. (Opsiyonel behavior'lar: logging, timing, idempotency — sıralı.)
3. `ILinbikHandler<TRequest,TResponse>.HandleAsync` çalışır → `Result<TResponse>`.

### `Result<T>` → `LBaseResponse` mapper (endpoint katmanı)

```csharp
public static IResult ToHttp<T>(this Result<T> result) =>
    result.IsSuccess
        ? Results.Ok(new LBaseResponse<T>(result.Value))
        : Results.Json(
            new LBaseResponse<T>(
                title:   result.Error.Title,
                message: result.Error.Message,
                isSuccess: false),
            statusCode: result.Error.Status);   // 400/403/404/409...
```

> Mevcut `divide`'daki elle `BadRequest(LBaseResponse...)` mantığı bu mapper'a taşınır;
> handler artık sadece `Result.Fail(...)` döner. `LGatewayAuthContext.FromRequest` ile
> doldurulan `Gateway` bağlamı, ortak bir `LBaseResponse` zarflama adımında eklenebilir.

---

## Klasör Yapısı (servis başına)

```
ArithmeticService/
  Program.cs                       // AddLinbikSlices() + MapLinbikSlices() (source-gen genişletir)
  Features/
    Calculations/
      Add.cs                       // [LFlow(Public)]  — bilinçli açık
      Subtract.cs                  // [LFlow(Self)]
      Multiply.cs                  // [LFlow(Self, Delegated)]
      Divide.cs                    // [LFlow(Self, Application)]
  (Controllers/ kaldırılır — minimal API'ye geçişte)
```

`Linbik.Slices` (paylaşımlı, asıl projede):

```
Linbik.Slices/
  Abstractions/    ILinbikRequest, ILinbikHandler, ILinbikValidator, ILinbikSender
  Results/         Result, Result<T>, LError
  Pipeline/        LinbikSender, ValidationBehavior
  Endpoints/       LinbikEndpoint.Handle, WithFlows, ToHttp
  SourceGen/       [LinbikSlice]/[LFlow] generator
  Analyzers/       Deny-by-default + naming analyzer
```

---

## Deny-by-default Zorlaması (P2)

`Linbik.Core` attribute yoksa `"*"` ürettiği için, güvenli varsayılan **build zamanında**
zorlanmalı:

- **Analyzer kuralı `LINBIK001` (Error):** `[LinbikSlice]` taşıyan bir tip `[LFlow(...)]`
  (en az bir flow **veya** açık `Public`) deklare etmiyorsa derleme **kırılır**.
- `[LFlow]` parametreleri `string` değil `LinbikFlow` enum'ıdır (`Self`/`Delegated`/`Application`) —
  yazım hatasına kapalı ve IDE'de otomatik tamamlanır. Public bilinçli olmalı: `[LFlowPublic]`.
- Böylece "attribute koymayı unuttum → kazara public" sınıfı hata imkânsızlaşır.

---

## Dağıtım / Distribution Planı

Bu bir iç platform; yeni artefakt dağıtımı yok. `Linbik.Slices` asıl projede **iç NuGet
paketi veya proje referansı** olarak yayınlanır. CI: mevcut build pipeline'a (1) analyzer
testleri, (2) source-gen snapshot testleri, (3) gateway OpenAPI smoke testi (her flow
doc'u beklenen endpoint setini içeriyor mu) eklenir.

---

## Hayata Geçirme Planı (asıl projede çalıştır)

**Adım 0 — SPIKE (P1, bloklayıcı, ~yarım gün):**
1. `arithmetic` servisinde `add` endpoint'ini tek başına minimal API'ye çevir.
2. Minimal API'ye flow metadata'sını ekle (`AddLinbikFlowExtension`'ın okuduğu mekanizma —
   `[LFlowAuthorize]` metadata veya `.WithMetadata(new LFlowAuthorizeAttribute(...))`).
3. `/openapi/v1.json`'da o operation'da `linbik-flows` çıkıyor mu **doğrula**.
4. Gateway'in filtreli doc'larında (self/delegated/apps) endpoint beklenen yerde mi bak.
   - ✅ Çıkıyorsa → Approach C devam.
   - ❌ Çıkmıyorsa → K2'yi "ince controller + slice"e çevir (slice yine tek dosya;
     sadece ince bir controller action `[LFlowAuthorize]` + `sender.Send` taşır).

**Adım 1 — Çekirdek altyapı:** `Linbik.Slices` — `ILinbikSender`, `Result<T>`,
`ValidationBehavior`, `ToHttp` mapper. Source-gen/analyzer henüz yok; elle bir slice yaz.

**Adım 2 — Source generator:** `[LinbikSlice]`/`[LFlow]` → endpoint mapping + DI kaydı +
flow metadata üret. Snapshot testleriyle kilitle.

**Adım 3 — Analyzer:** `LINBIK001` deny-by-default + isim/şablon kuralları.

**Adım 4 — Referans dönüşüm:** `ArithmeticService`'i tamamen slice'a çevir (P5). Tüm
4 endpoint'i flow attribute'larıyla. `AggregationService`'i eski stilde bırak (kıyas).

**Adım 5 — Doğrulama:** Gateway OpenAPI smoke testi + her flow için Scalar doc'ta
endpoint setini gözle doğrula + auth matris testi (Self-only endpoint Delegated token'la
reddediliyor mu).

---

## Açık Sorular

- `AddLinbikFlowExtension` minimal API endpoint metadata'sını okuyor mu? (Adım 0 cevaplar.)
- `Result<T>`/`LError` Linbik.Core'da zaten var mı, yoksa `Linbik.Slices`'ta mı tanımlanacak?
- Validation için FluentValidation mı, yalın `ILinbikValidator` mı? (Lisans/bağımlılık
  açısından yalın tercih ediliyor; FluentValidation isteğe bağlı adapter olabilir.)
- Source generator hangi hedef framework? (Roslyn analyzer/gen `netstandard2.0` ister.)

## Başarı Kriterleri

- Yeni bir endpoint yazmak = tek dosya (Request/Response/Handler/Validator + `[LFlow]`).
  Endpoint mapping ve DI **elle yazılmaz**.
- `[LFlow]` olmayan slice **derlenmez** (deny-by-default kanıtlı).
- Gateway'in self/delegated/apps doc'ları beklenen endpoint setini gösterir.
- Mevcut mobil/web istemcileri için response şekli (`LBaseResponse<T>`) değişmez.

## Atama (sıradaki somut iş)

**Adım 0 spike'ı asıl projede çalıştır:** `arithmetic/add`'i minimal API'ye çevirip
`/openapi/v1.json`'da `linbik-flows`'un çıktığını kanıtla. Bu tek test, Approach C'nin
tüm temelini ya onaylar ya da seni "ince controller + slice" fallback'ine yönlendirir.
Otomasyon yazmadan önce bu cevabı al.

## Süreçte fark ettiklerim

- "Asıl güvenlik kapısı downstream `[LFlowAuthorize]`" diye yorum satırlarına yazmışsın —
  güvenlik sınırını gateway'e değil servise koyman doğru tasarım; deny-by-default tercihin
  bununla tutarlı.
- `add`'in attribute'suz public olmasını fark edince hemen deny-by-default'a geçtin;
  kolay yolu (mevcut davranış) değil güvenli yolu seçtin.
- Source-gen + analyzer'ı seçtin (Approach C) — en hızlısı değil, en az tekrar edeni.
  Ölçek düşünen bir tercih; ama Adım 0 spike'ı atlamadan gitmek riski erken kapatır.
```

