# Delegated API belgelerini kendi OpenAPI belgenize ekleme

Mevcut `AddLinbikYarp(...)` kaydı belge cache'ini ve birleştirmeyi otomatik ekler:

```csharp
using Linbik.YARP.OpenApi;

// İsteğe bağlı: varsayılan yenileme/cache ayarlarını değiştirmek için.
builder.Services.Configure<LinbikDelegatedOpenApiOptions>(options =>
{
    options.RefreshInterval = TimeSpan.FromMinutes(15);
    options.CacheDirectory = ".linbik/openapi";
});
builder.Services.AddOpenApi("v1");

var app = builder.Build();
app.UseLinbikYarp();
app.MapOpenApi(); // Mevcut ortam/RequireAuthorization ayarlarınızı burada uygulayın.
app.Run();
```

`AddLinbikYarp` tüm kayıtlı OpenAPI belgelerine birleştirmeyi ekler; `AddOpenApi`
çağrısının önce veya sonra olması fark etmez. `AddLinbikDelegatedDocuments()`
ve `AddLinbikDelegatedOpenApi()` çağrıları zorunlu değildir. Eski açık kayıt
çağrıları kullanılabilir; aynı transformer iki kez eklenmez.
Kütüphane OpenAPI veya Scalar endpoint'i açmaz ve erişim politikası
eklemez. Mevcut Swagger/Scalar arayüzünüz birleşik belgeyi kullanmaya devam eder.

`IntegrationServices` yapılandırması:

```json
{
  "messtick": {
    "SourcePath": "/api/messtick",
    "TargetBaseUrl": "https://api.messtick.com",
    "TargetPath": "/api/delegated",
    "DelegatedDocumentPath": "/openapi/delegated.json"
  }
}
```

`DelegatedDocumentPath` varsayılanı `/openapi/delegated.json`; bir servisi içe
aktarmamak için `null` yapın. Application istemci üretimi için kullanılan
`DocumentPath` (`/openapi/apps.json`) ayrı kalır.

## Proxy yolları ve geçiş

`SourcePath` yerel proxy önekidir; `TargetPath` hedef servisin yol önekidir.
Doküman birleştirilirken `TargetPath`, `SourcePath` ile değiştirilir.
Proxy isteği iletilirken bunun tersi yapılır.

Örnek: uzaktaki belge `/api/delegated/integrations/settings` içeriyorsa:

- Birleşik belge ve tarayıcı isteği: `/api/messtick/integrations/settings`
- Proxy hedefi: `https://api.messtick.com/api/delegated/integrations/settings`

`TargetPath` boşsa hedef belge yolu aynen korunup başına `SourcePath` eklenir.
Önek eşleştirmesi segment sınırında yapılır; `/api/delegated-other` eşleşmez.
Belgede yapılandırılan hedef öneki dışında yollar varsa belge uyarı ile reddedilir;
yanlış proxy yolları yayımlanmaz. `UseLinbikApplication` da hedef URL'ye
`TargetPath` ekler. Query parametreleri korunur.

Belge indirilirken hedef paket adına ait Application token sunucu tarafında
kullanılır. Token cache dosyasına veya birleşik dokümana eklenmez. Proxy API
çağrısında entegrasyon cookie'si; yoksa istemcinin Delegated Bearer token'ı
kullanılır. Application token belgeyi okumak içindir, kullanıcı adına API çağrısı
yapmak için kullanılmaz.

## Güncelleme ve hatalar

Başlangıçta belge indirilir; ilk OpenAPI isteği başlangıç yüklemesini bekleyebilir.
Sonrasında varsayılan 15 dakikalık arka plan yenilemesi yapılır. Yenileme sırasında
mevcut belge sunulur. İndirme/token/JSON hatasında son başarılı belge korunur ve
uyarı loglanır. İlk yüklemede başarılı kopya yoksa yalnız uygulamanın kendi
endpoint'leri ve erişilebilen servisler görünür.

Disk cache yeniden başlatmalarda da kullanılır. Dizin kalıcı bir volume olabilir;
`CacheDirectory=null` disk cache'i kapatır. Cache anahtarı hedef adresini, paket
adını ve proxy önekini içerir. Güncelleme atomik dosya değişimiyle yapılır.
Manuel yenileme gerekirse DI'dan `DelegatedDocumentCache` alıp
`RefreshAsync(false, cancellationToken)` çağrılabilir.

Şema, component, security scheme ve operationId adları paket bazında ayrılır;
yerel referanslar güncellenir. Host'un mevcut yolları ve bileşenleri üzerine
yazılmaz; çakışan servis loglanıp atlanır. JSON OpenAPI 3 belgeleri desteklenir.
Harici dosya/URL referansları ve çözülemeyen yerel referanslar kabul edilmez;
üretici bunları tek bir belgede toplamalıdır. İçe aktarılan operasyonlar yerel
proxy sunucusunu (`/`) kullanır; uygulama bir PathBase altında barındırılıyorsa
sonraki bir document transformer ile bu server adresini PathBase'e uyarlayın.

Doküman erişimi host'un politikalarına bağlıdır. Uzak API yetkilendirmesi ise
korunur; belgeyi okuyabilmek API işlem yetkisi vermez.

## Flow uzantısıyla birlikte kullanım

```csharp
builder.Services.AddOpenApi(options =>
    options.AddLinbikFlowExtension());
```

Linbik'in dış belge sözleşmesindeki alan adı `linbik-flows` olarak korunur.
Ayrıştırma sırasında bu metadata ayrı tutulur ve aynı adla belgeye geri eklenir.
İkinci bir alan adı kullanılmaz. Mevcut Linbik belge filtrelerini değiştirmeniz gerekmez.
Başarısız bir import host'un kendi belgesini bozmaz; parser hataları JSON
konumlarıyla birlikte loglanır.
