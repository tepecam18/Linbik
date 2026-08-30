# Linbik.PasetoAuthManager.Android

Backend'i **Linbik.PasetoAuthManager** (ASP.NET) kullanan Android uygulamalarının,
kullanıcının Linbik hesabıyla oturum açmasını sağlayan native bir kütüphane +
bu kütüphaneyi kullanan bir örnek uygulama.

```
Linbik.PasetoAuthManager.Android/
  linbikauth/   # Yayınlanabilir Android kütüphanesi (com.linbik.pasetoauth)
  sample/       # Kütüphaneyi kullanan minimal örnek uygulama
```

## Bu, "Sign in with Linbik" akışının web/redirect sürümünden FARKLI

Bu kütüphane, `Linbik.JwtAuthManager`/`Linbik.PasetoAuthManager`'ın web istemcileri için
kullandığı **302 redirect** akışını değil, `ActionResultType: "Json"` ile işaretlenmiş
**mobil client** akışını hedefler. Token değişimi sizin backend'inizde (PASETO cookie
olarak) tutulur — uygulama, Linbik'in yetkilendirme/onay sayfasını (RFC 8252 uyarınca bir
WebView değil, **Custom Tabs**) açar; kullanıcı onayladıktan sonra Linbik, uygulamanıza
özel bir deep link'e (`{applicationId}://oauth/callback?code=...`) yönlendirir. Uygulama bu
kodu alıp **sizin** backend'inizin `/api/Linbik/callback` endpoint'ine ileterek oturumu tamamlar.

## Backend Yapılandırması (zorunlu ön koşul)

Backend'inizin `appsettings.json`'ında, `Linbik:Clients` altına `ActionResultType: "Json"`
ile işaretli, `Name` alanı verilmiş bir client eklemeniz gerekir. `GetClientConfig`
**sadece `Name` alanına** bakar; `ClientType` diye bir alan **yoktur** (yazsanız da
yoksayılır):

```jsonc
{
  "Linbik": {
    // ...
    "Clients": [
      {
        "Name": "Mobile",
        "ClientId": "...",
        "ActionResultType": "Json"
      }
    ]
  }
}
```

`ActionResultType: "Redirect"` (varsayılan/web client'ları) ile bu kütüphaneyi
kullanmayı denemeyin — `/api/Linbik/login` bir HTML/redirect sayfası döner, JSON değil,
ve kütüphane bunu ayrıştıramaz (anlaşılır bir hata mesajıyla başarısız olur).

**KeylessMode kullanıyorsanız** (varsayılan, `.linbik/credentials.json` ile otomatik
provision edilen tek bir `ClientId`): Linbik sunucusunda ayrıca elle bir "mobil client"
kaydetmenize **gerek yok**. `Clients` listesindeki her giriş sadece **sizin backend'inizin**
belirli bir `name` isteği için Json mu Redirect mi döneceğini belirler — Linbik'in kendi
tarafında farklı bir uygulama/redirect URI kaydı anlamına gelmez. Yani aynı `ClientId`'yi
hem `"Name": "Default"` (web, Redirect) hem `"Name": "Mobile"` (Json) girişlerinde
tekrar kullanmanız **tamamen normaldir ve amaçlanan kullanımdır** — dashboard'da ayrı bir
URL/redirect URI girmeniz istenmez, çünkü henüz bir dashboard kaydı yoktur (provisioning
backend'in kendi HTTP isteğinden otomatik yapılır).

`clientName` boş bırakılırsa (veya `"Name"` alanı hiç verilmezse, varsayılanı `"Default"`
dır) backend, KeylessMode'da `Clients` listesindeki **ilk** girişi kullanır — bu ilk giriş
`ActionResultType: Json` değilse (örn. bir web client'sa) mobil akış çalışmaz. Bu yüzden
mobil için ayrı, açıkça adlandırılmış (`"Name": "Mobile"` gibi) bir giriş eklemeniz ve
`LinbikPasetoAuthOptions.clientName` alanına aynı adı vermeniz önerilir.

**Linbik.App'te (veya Linbik.Api'de) bu uygulama için bir Client oluştururken**, `RedirectUri`
alanına uygulamanızın `applicationId`'sine dayanan özel URI şemasını yazın:

```
{applicationId}://oauth/callback
```

Örn. `sample` moduü için: `com.linbik.pasetoauth.sample://oauth/callback`. Bu, kod (`code`)
alındıktan sonra Linbik'in tarayıcıyı yönlendirdiği adrestir — backend'inizin kendi
callback URL'i **değildir** (bkz. aşağıdaki "Nasıl Çalışır"). Kendi uygulamanızda bu şemayı
[`linbikauth`'ın manifest'i](linbikauth/src/main/AndroidManifest.xml) `${applicationId}`
placeholder'ıyla otomatik oluşturur; ekstra bir manifest değişikliği yapmanıza gerek yoktur.
Oluşturulan Client'ın `clientId`'sini kopyalayıp appsettings.json'daki `"Mobile"` girişinin
`ClientId` alanına yazın.

## Nasıl Çalışır

1. Kütüphane kendi OkHttp istemcisiyle (arka planda, herhangi bir UI olmadan)
   `{backendBaseUrl}/api/Linbik/login?name=Mobile` adresine istek atar. Backend, PKCE
   `code_verifier`'ı bir `Set-Cookie` ile döner ve JSON gövdesinde Linbik'in gerçek
   giriş/onay sayfasının adresini (`redirectPath`) verir.
2. Bu adres bir **Custom Tabs** sekmesinde açılır (WebView'da DEĞİL — RFC 8252 gereği;
   ayrıca yalnızca Custom Tabs/harici tarayıcı App Links aracılığıyla yüklřyse
   Linbik.Mobil uygulamasına doğru yönlendirme yapabilir; WebView bunu desteklemez).
3. Kullanıcı Linbik'te oturum açar/onaylar. Linbik, tarayıcıyı bu uygulama için Linbik.App'te
   kayıtlı `RedirectUri`'ye, yani `{applicationId}://oauth/callback?code=...`'a yönlendirir.
4. Bu özel URI şeması [`LinbikRedirectActivity`](linbikauth/src/main/kotlin/com/linbik/pasetoauth/LinbikRedirectActivity.kt)
   tarafından yakalanır ve az önce açılmış olan `LinbikAuthActivity`'ye iletilir
   (`onNewIntent`). `code` sorgu parametresi buradan alınır.
5. Kütüphane, aynı `code` ile backend'inizin `{backendBaseUrl}/api/Linbik/callback?code=...`
   endpoint'ine (yine kendi OkHttp istemcisiyle) istek atar. Backend, PKCE doğrulamasını
   1. adımda yazdığı çerezle yapar, token değişimini tamamlar ve kullanıcı bilgisini
   (`LoginCallbackResponse`) JSON olarak döner.
6. Sonuç uygulamanıza döndürülür. Backend'in `Set-Cookie` ile yazdığı oturum çerezleri
   (`authToken`, `linbik_refresh` gibi `HttpOnly` çerezler dahil) `android.webkit.CookieManager`
   üzerinde kalıcı olur — bkz. [`LinbikWebViewCookieJar`](linbikauth/src/main/kotlin/com/linbik/pasetoauth/LinbikWebViewCookieJar.kt)
   (adı tarihi nedenlerle böyle kaldı — artık herhangi bir WebView'a bağlı değildir, sadece
   başlı başına, kalıcı bir OkHttp `CookieJar` implementasyonudur).

1. ve 5. adımlar **aynı** `LinbikWebViewCookieJar` örneğini kullandığı için PKCE
`code_verifier` çerezi ikisi arasında korunur ve backend'in `PkceService.GetVerifier(...)`
doğrulaması normal şekilde çalışır. Custom Tabs'ın kendi çerezleri (Linbik'in oturum açma
 sayfasına ait) bunlardan tamamen ayrıdır ve uygulamanız tarafından hiç görülmez/kullanılmaz.

## Kurulum

### 1. JitPack ile Ekleme

Projenizin `settings.gradle.kts` dosyasına JitPack repository'sini ekleyin:

```kotlin
dependencyResolutionManagement {
    repositories {
        google()
        mavenCentral()
        maven { url = uri("https://jitpack.io") }
    }
}
```

Ardından uygulamanızın `build.gradle.kts` dosyasına bağımlılığı ekleyin:

```kotlin
dependencies {
    implementation("com.github.tepecam18.Linbik:linbikauth:1.0.0")
}
```

## Kullanım

Aşağıdaki örnekte temel giriş akışı gösterilmektedir. Daha detaylı teknik bilgi ve ileri seviye kullanım (Refresh Token, Cookie yönetimi vb.) için [linbikauth/README.md](linbikauth/README.md) dosyasını inceleyin.

```kotlin
class MyActivity : ComponentActivity() {
    private val authClient = LinbikPasetoAuthClient()
    private lateinit var launcher: ActivityResultLauncher<LinbikPasetoAuthOptions>

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        // 1. Launcher'ı Kaydedin (onCreate içinde olmalı!)
        launcher = authClient.registerLauncher(this) { result ->
            when (result) {
                is LinbikAuthResult.Success -> {
                    // Kullanıcı başarıyla giriş yaptı: result.displayName, result.userId vb.
                }
                is LinbikAuthResult.Error -> { /* Hata mesajı: result.message */ }
                LinbikAuthResult.Cancelled -> { /* Kullanıcı iptal etti */ }
            }
        }

        signInButton.setOnClickListener {
            // 2. Akışı Başlatın
            launcher.launch(
                LinbikPasetoAuthOptions(
                    backendBaseUrl = "https://your-backend.com",
                    clientName = "Mobile"
                )
            )
        }
    }
}
```

## Teknik Detaylar ve Geliştirme

Kütüphanenin iç yapısı, Custom Tabs entegrasyonu ve katkıda bulunma rehberi için lütfen kütüphane dizinindeki [README](linbikauth/README.md) dosyasına göz atın.
