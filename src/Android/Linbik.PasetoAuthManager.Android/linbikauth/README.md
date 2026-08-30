# Linbik Paseto Auth Library (Internal & Technical)

Bu doküman, `linbikauth` kütüphanesinin iç yapısını, teknik tasarım kararlarını ve geliştiriciler için mimari detayları içerir.

## Mimari Yapı

Kütüphane, **RFC 8252 (OAuth 2.0 for Native Apps)** standartlarını temel alır. Temel bileşenler şunlardır:

### 1. LinbikAuthActivity
Giriş akışının kalbidir. Şu adımları yönetir:
- **Backend Handshake:** `/api/Linbik/login` üzerinden `redirectPath` ve PKCE verilerini alır.
- **Custom Tabs:** Kullanıcıyı güvenli bir şekilde tarayıcıya yönlendirir.
- **Callback Handling:** Deep link (`onNewIntent`) üzerinden gelen `code` değerini yakalar ve backend'e onay için gönderir.

### 2. LinbikSharedCookieJar
Android'in sistem düzeyindeki `CookieManager`'ı ile OkHttp arasında bir köprü görevi görür.
- **Persistence:** Oturum çerezleri uygulama kapatılsa bile korunur.
- **Sharing:** OkHttp ile alınan çerezler, uygulama içindeki WebView'larda da otomatik olarak geçerli olur.

### 3. Activity Result API Entegrasyonu
`SignInContract` sınıfı, modern Android `ActivityResultContract` yapısını kullanarak, giriş sonucunun (Success/Error/Cancelled) güvenli ve tip güvenli (type-safe) bir şekilde dönmesini sağlar.

## Geliştirici Rehberi

### Yeni Bir Özellik Ekleme
1. **API Katmanı:** Eğer yeni bir endpoint (örn. profil bilgisi çekme) eklenecekse, `LinbikPasetoAuthClient` içine yeni bir `suspend` metod eklenmelidir.
2. **Hata Yönetimi:** Ağ istekleri için `LinbikAuthActivity.getJson` metodu kullanılmalı veya benzer bir hata yakalama mekanizması kurulmalıdır.
3. **ProGuard:** Eğer yeni bir veri modeli (`data class`) eklerseniz, `consumer-rules.pro` dosyasına gerekli `-keep` kuralını eklemeyi unutmayın.

### Yerel Test (Local Development)
Kütüphane üzerinde değişiklik yaparken `sample` modülünü kullanarak test edebilirsiniz. `sample` modülü kütüphaneye doğrudan proje referansı ile bağlıdır.

## Backend Gereksinimleri
- `Linbik.PasetoAuthManager` (ASP.NET)
- `ActionResultType: "Json"` ayarlı bir Client tanımı.
- PKCE desteği aktif olmalıdır.
