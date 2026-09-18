# Linbik Web SDK — Nuxt örneği

`src/Web/Linbik.PasetoAuthManager.Web` paketini yerel bağımlılık olarak kullanır.
Giriş, sayfa açılışında oturum yenileme, kullanıcı/entegrasyon gösterimi, korunan API
çağrısı ve çıkış örneklerini içerir. Tokenlar backend'in HttpOnly çerezlerinde kalır.

## Başlatma

```sh
cd examples/nuxt
pnpm install
cp .env.example .env
pnpm dev --https
```

Yerel HTTPS sertifikalarını tarayıcıda güvenilir yapın. `.env` içindeki backend
adresini ve kayıtlı client adını kendi ortamınıza göre ayarlayın. Keyless modda
backend'in ilk istemcisini kullanmak için client adını boş bırakabilirsiniz.
Örnekte web adresi `https://localhost:3000`, backend `https://localhost:7020` kabul edilir.
Her iki uçta aynı hostname ve HTTPS kullanmak SameSite çerez akışını korur.

## Backend ayarları

Mevcut `examples/AspNet/AspNet/Program.cs` JWT ile çalışıyor. PASETO örneği için
`.AddLinbikJwtAuth()` yerine `.AddLinbikPasetoAuth()` ve `UseLinbikJwtAuth()` yerine
`UseLinbikPasetoAuth()` kullanın; PASETO anahtar ayarlarını backend dokümanına göre yapın.
SDK'nin kullandığı çerez/endpoint sözleşmesi mevcut JWT moduyla da aynıdır.

Backend `Linbik:Clients` listesinde web istemcisini yapılandırın (ClientId kayıtlı
web istemcinizin kimliğidir):

```json
{
  "Name": "Web",
  "ClientId": "YOUR_REGISTERED_WEB_CLIENT_ID",
  "RedirectUrl": "https://localhost:3000",
  "ActionResultType": "Redirect"
}
```

Linbik'teki yetkilendirme callback adresi backend'in
`https://localhost:7020/api/Linbik/callback` adresidir. `RedirectUrl` ise callback
tamamlandıktan sonra gidilecek Nuxt adresidir. `Json` modundaki Mobile istemcisini kullanmayın.
PKCE backend tarafından yönetilir; özel anahtar/API anahtarı Nuxt public config'e konmaz.

Farklı portlar farklı origin olduğu için backend'e CORS ekleyin:

```csharp
// builder.Build() öncesi
builder.Services.AddCors(options => options.AddPolicy("NuxtExample", policy =>
    policy.WithOrigins("https://localhost:3000")
        .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// app.UseRouting() sonrası, authentication/authorization öncesi
app.UseCors("NuxtExample");
```

Üretimde origin listesini gerçek web adresiyle sınırlandırın. Aynı site üzerinde
HTTPS kullanın; farklı sitelerde SameSite/üçüncü taraf çerez kuralları ayrıca geçerlidir.

## Kodun yerleşimi

- `app/plugins/linbik.client.ts`: tarayıcıya özel SDK örneği.
- `app/composables/useLinbikAuth.ts`: kullanıcı, bekleme/hata durumu ve oturum işlemleri.
- `app/app.vue`: düğmeler ve `/Test/Protected` çağrısı; uygulama açılırken bir kez refresh yapar.
- `.env.example`: `NUXT_PUBLIC_LINBIK_*` ayarları. API yolu kendi backend'iniz için değiştirilebilir.

401 yanıtı giriş gerektiğini gösterir. Ağ/CORS hatası oturum yokmuş gibi gizlenmez.
Korunan API otomatik tekrar çağrılmaz; başarısız bir yazma isteğini tekrarlamak uygulamanın kararıdır.
Bu örnek SSR sırasında oturum sorgulamaz; kullanıcı durumu tarayıcı açıldıktan sonra yüklenir.

## Doğrulama

```sh
node --test ../../src/Web/Linbik.PasetoAuthManager.Web/test/client.test.js
pnpm build
```

Canlı akış: giriş yapın → Nuxt'a dönüldüğünü ve kullanıcıyı doğrulayın → korunan API'yi
çağırın → yenileyin → çıkış yapın → sayfayı yenileyerek oturumun kapanmasını kontrol edin.

[SDK ayrıntıları](../../src/Web/Linbik.PasetoAuthManager.Web/README.md) ·
[Nuxt runtime config](https://nuxt.com/docs/4.x/guide/going-further/runtime-config)
