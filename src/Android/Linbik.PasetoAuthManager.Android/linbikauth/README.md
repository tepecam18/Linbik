# Linbik Paseto Auth Manager for Android

Bu kütüphane, Linbik.PasetoAuthManager (ASP.NET) kullanan backend'inize karşı Android uygulamanızda "Linbik ile Giriş Yap" akışını kolayca entegre etmenizi sağlar.

## Özellikler

- **Custom Tabs Desteği:** Giriş akışı, güvenli ve App Link uyumlu Custom Tabs üzerinden yürütülür.
- **Persistent Cookie Management:** Giriş sırasında alınan çerezler `CookieManager` üzerinden saklanır ve uygulamanızın diğer kısımlarıyla (WebView/OkHttp) paylaşılabilir.
- **Activity Result API:** Modern `ActivityResultLauncher` yapısı ile kolay entegrasyon.
- **Refresh Token Desteği:** Arka planda oturum yenileme özelliği.

## Kurulum

### 1. Bağımlılığı Ekleme

Kütüphaneyi yayınladığınız yönteme göre (JitPack veya Maven Central) `build.gradle.kts` dosyanıza ekleyin:

```kotlin
dependencies {
    implementation("com.linbik:paseto-auth:1.0.0")
}
```

### 2. AndroidManifest Yapılandırması

Kütüphane, giriş sonrası uygulamanıza geri dönebilmek için bir URI şeması kullanır. Bu şema varsayılan olarak `applicationId` değerinizdir. Linbik Dashboard üzerinden Redirect URI olarak `{applicationId}://oauth/callback` adresini kaydettiğinizden emin olun.

## Kullanım

### Giriş Akışını Başlatma

`onCreate` içinde launcher'ı kaydedin ve ardından akışı başlatın:

```kotlin
class MainActivity : AppCompatActivity() {
    private val authClient = LinbikPasetoAuthClient()
    private lateinit var launcher: ActivityResultLauncher<LinbikPasetoAuthOptions>

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        // 1. Launcher'ı kaydet
        launcher = authClient.registerLauncher(this) { result ->
            when (result) {
                is LinbikAuthResult.Success -> {
                    // Kullanıcı başarıyla giriş yaptı
                    println("Hoş geldin, ${result.displayName}")
                }
                is LinbikAuthResult.Error -> {
                    // Bir hata oluştu
                    println("Hata: ${result.message}")
                }
                LinbikAuthResult.Cancelled -> {
                    // Kullanıcı iptal etti
                }
            }
        }

        // 2. Akışı başlat
        signInButton.setOnClickListener {
            val options = LinbikPasetoAuthOptions(
                backendBaseUrl = "https://your-backend.com",
                clientName = "MobileApp"
            )
            launcher.launch(options)
        }
    }
}
```

### OkHttp ile Oturumu Paylaşma

Uygulamanızın kendi API isteklerinde Linbik oturumunu kullanması için `LinbikSharedCookieJar`'ı ekleyin:

```kotlin
val okHttpClient = OkHttpClient.Builder()
    .cookieJar(LinbikSharedCookieJar()) // VEYA LinbikPasetoAuthClient.cookieJar()
    .build()
```

### Çıkış Yapma ve Refresh

```kotlin
lifecycleScope.launch {
    // Çıkış yap
    authClient.signOut(options)
    
    // Oturumu yenile
    val success = authClient.refreshToken(options)
}
```

## Backend Yapılandırması

Bu kütüphanenin çalışması için backend tarafında `Linbik.PasetoAuthManager` kurulu olmalı ve mobil client için `ActionResultType = "Json"` olarak ayarlanmalıdır.
